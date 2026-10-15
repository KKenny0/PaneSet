using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PaneSet;

internal sealed class PlacementInput
{
    public bool Armed { get; private set; }
    public void Update(bool focused, bool chordReleased)
    {
        if (!focused) Armed = false;
        else if (chordReleased) Armed = true;
    }
    public int? PositionKey(Key key, ModifierKeys modifiers, bool repeat)
    {
        if (!Armed || repeat || modifiers != ModifierKeys.None) return null;
        if (key is >= Key.D1 and <= Key.D9) return key - Key.D1;
        if (key is >= Key.NumPad1 and <= Key.NumPad9) return key - Key.NumPad1;
        return null;
    }
}

internal sealed record PlacementChoice(int? Index, string Message, bool Failed = false);
internal sealed class PlacementWindow : Window
{
    private readonly WindowManager manager;
    private readonly PlacementInput input = new();
    private readonly DispatcherTimer refresh;
    private readonly Grid root;
    private readonly ShortcutBinding? chord;
    private bool finished;
    private int ticks;
    public WindowTarget Source { get; }
    public LayoutPositions Positions { get; }
    public bool IsReady => input.Armed && !finished;
    public event Action<PlacementChoice>? Completed;

    public PlacementWindow(Window reference, WindowManager manager, WindowTarget source, LayoutPositions positions, ShortcutBinding? chord)
    {
        this.manager = manager; Source = source; Positions = positions; this.chord = chord;
        Title = "PaneSet · 单窗放置"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.Manual;
        FontFamily = reference.FontFamily; FontSize = 14;
        SetResourceReference(BackgroundProperty, "Canvas"); SetResourceReference(ForegroundProperty, "Ink");
        Resources[typeof(Button)] = reference.FindResource(typeof(Button));
        root = new Grid { Margin = new Thickness(18), Focusable = true }; Content = root;
        var content = new StackPanel();
        root.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false });
        TextBlock Text(string value, string color, double size = 14)
        {
            var text = new TextBlock { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap };
            text.SetResourceReference(TextBlock.ForegroundProperty, color); return text;
        }
        var heading = Text("单窗放置", "HeaderInk", 23); heading.FontWeight = FontWeights.SemiBold;
        content.Children.Add(heading);
        var title = Text(source.Title, "Ink"); title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.ToolTip = source.Application + " · " + source.Title; title.Margin = new Thickness(0, 8, 0, 0); content.Children.Add(title);
        var destination = Text("放到" + positions.Display.Name + " · 上次记住的布局位置", "Ink");
        destination.Margin = new Thickness(0, 8, 0, 0); content.Children.Add(destination);
        var hint = Text("点击位置或独立按 1–9，立即移动并缩放。原窗口留在后面。", "Muted", 12);
        hint.Margin = new Thickness(0, 5, 0, 12); content.Children.Add(hint);
        var work = positions.Display.WorkArea;
        double scale = Math.Max(450d / work.Width, positions.Regions.Max(r => Math.Max(44d / r.Width, 44d / r.Height)));
        var diagram = new Canvas { Width = work.Width * scale, Height = work.Height * scale };
        for (int i = 0; i < positions.Regions.Count; i++)
        {
            int index = i; var rect = positions.Regions[i];
            var button = new Button { Content = (i + 1).ToString(), Width = rect.Width * scale, Height = rect.Height * scale,
                MinHeight = 44, Padding = new Thickness(0), FontSize = 20, FontWeight = FontWeights.SemiBold };
            button.SetResourceReference(BackgroundProperty, "MintPale"); button.SetResourceReference(ForegroundProperty, "Ink");
            AutomationProperties.SetName(button, $"放到位置 {i + 1}");
            button.ToolTip = $"位置 {i + 1} · {rect.Width} × {rect.Height}";
            button.Click += (_, _) => Submit(index);
            Canvas.SetLeft(button, (rect.X - work.X) * scale); Canvas.SetTop(button, (rect.Y - work.Y) * scale);
            diagram.Children.Add(button);
        }
        content.Children.Add(new ScrollViewer { Content = diagram, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false });
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "取消 · Esc", Padding = new Thickness(12, 6, 12, 6) };
        cancel.Click += (_, _) => Cancel(); AutomationProperties.SetName(cancel, "取消单窗放置");
        DockPanel.SetDock(cancel, Dock.Right); footer.Children.Add(cancel);
        var navigation = Text("Tab 选择位置，Enter / Space 放置\n位置 10 及以上请点击或用 Tab 选择", "Muted", 12);
        navigation.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(navigation); content.Children.Add(footer);
        SourceInitialized += (_, _) =>
        {
            nint handle = new WindowInteropHelper(this).Handle;
            var sourceWork = WindowManager.DisplayFor(source.Handle).WorkArea;
            Native.SetWindowPos(handle, 0, sourceWork.X, sourceWork.Y, 1, 1, 0x14);
            double dpi = Native.GetDpiForWindow(handle) / 96d;
            if (!double.IsFinite(dpi) || dpi <= 0) throw new InvalidOperationException("无法读取源屏幕缩放，请重新唤起。");
            int width = Math.Min(sourceWork.Width, (int)Math.Round(540 * dpi));
            int height = Math.Min(sourceWork.Height, (int)Math.Round(470 * dpi));
            Native.SetWindowPos(handle, 0, sourceWork.X + (sourceWork.Width - width) / 2, sourceWork.Y + (sourceWork.Height - height) / 2, width, height, 0x14);
        };
        refresh = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Input, (_, _) => Poll(), Dispatcher);
        ContentRendered += (_, _) =>
        {
            if (!ContainsForeground() || !root.Focus() || !IsKeyboardFocusWithin)
            { Cancel("未取得单窗放置键盘焦点，请从目标窗口重新唤起。", true); return; }
            Poll(); refresh.Start();
        };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        { if (!finished && !ContainsForeground()) Cancel("已离开单窗放置，未移动窗口，原恢复记录保留。"); }));
        Closed += (_, _) => { refresh.Stop(); if (!finished) Finish(new(null, "已关闭单窗放置，未移动窗口。"), false); };
        PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key is Key.Escape or Key.Enter or Key.Space or >= Key.D0 and <= Key.D9 or >= Key.NumPad0 and <= Key.NumPad9)
            {
                if (!IsReady || !ContainsForeground() || !IsKeyboardFocusWithin || e.IsRepeat || Keyboard.Modifiers != ModifierKeys.None)
                { e.Handled = true; return; }
                if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; Cancel(); }
                else if (input.PositionKey(key, Keyboard.Modifiers, e.IsRepeat) is int index) { e.Handled = true; Submit(index); }
                else if (key is Key.Enter or Key.Space && Keyboard.FocusedElement is not Button) e.Handled = true;
            }
        };
    }

    private bool ContainsForeground() => Native.GetForegroundWindow() == new WindowInteropHelper(this).Handle;
    private void Poll()
    {
        if (finished) return;
        if (!ContainsForeground() || !IsKeyboardFocusWithin) { Cancel("已离开单窗放置，未移动窗口。"); return; }
        bool released = new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, (int)(chord?.Key ?? 0) }
            .Where(k => k != 0).All(k => (Native.GetAsyncKeyState(k) & 0x8000) == 0);
        input.Update(true, released);
        if (++ticks % 5 == 0 && (!manager.CanPlace(Source, Positions.Version, out string message)
            || !manager.IsPlacementDesktop(new WindowInteropHelper(this).Handle, Positions.DesktopId)))
            Cancel(message.Length > 0 ? message : "单窗放置所在桌面已改变，请重新唤起。", true);
    }
    private void Submit(int index)
    {
        if (!IsReady || !ContainsForeground() || !IsKeyboardFocusWithin || index < 0 || index >= Positions.Regions.Count) return;
        if (!manager.CanPlace(Source, Positions.Version, out string message)
            || !manager.IsPlacementDesktop(new WindowInteropHelper(this).Handle, Positions.DesktopId))
        { Cancel(message.Length > 0 ? message : "当前桌面已改变，请重新唤起。", true); return; }
        Finish(new(index, ""));
    }
    public void Cancel(string message = "已取消单窗放置，未移动窗口，原恢复记录保留。", bool failed = false) => Finish(new(null, message, failed));
    private void Finish(PlacementChoice choice, bool close = true)
    {
        if (finished) return;
        finished = true; refresh.Stop();
        if (close) Close();
        Completed?.Invoke(choice);
    }
}
