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

internal sealed record PlacementChoice(int? Index, string Message, bool Failed = false, WindowTarget? Reference = null);
internal sealed class PlacementWindow : Window
{
    private readonly WindowManager manager;
    private readonly PlacementInput input = new();
    private readonly DispatcherTimer refresh;
    private readonly Grid root;
    private readonly ShortcutBinding? chord;
    private readonly Guid sourceDesktop;
    private readonly IReadOnlyList<WindowTarget> references;
    private readonly Dictionary<int, (Button Button, TextBlock Detail)> referenceRows = [];
    private readonly StackPanel referenceList = new();
    private readonly Grid referenceBody = new();
    private readonly StackPanel referenceListContent = new(), previewContent = new();
    private readonly ReferencePositionPreview positionPreview = new();
    private readonly TextBlock previewTitle = Text("", "Ink"), previewDetail = Text("", "Muted", 12);
    private int previewIndex = -1;
    private readonly TextBox search = new() { Margin = new Thickness(0, 0, 0, 8), MinHeight = 34 };
    private readonly TextBlock destination;
    private readonly TextBlock hint;
    private readonly FrameworkElement referenceContent;
    private readonly FrameworkElement? layoutContent;
    private readonly Button? referenceModeButton, layoutModeButton;
    private bool referenceMode;
    private bool finished;
    private int ticks;
    public WindowTarget Source { get; }
    public LayoutPositions? Positions { get; }
    public bool IsReady => input.Armed && !finished;
    public bool IsReferenceMode => referenceMode;
    public event Action<PlacementChoice>? Completed;

    public PlacementWindow(Window reference, WindowManager manager, WindowTarget source, LayoutPositions? positions,
        ShortcutBinding? chord, IReadOnlyList<WindowTarget>? referenceWindows = null)
    {
        this.manager = manager; Source = source; Positions = positions; this.chord = chord;
        referenceMode = referenceWindows != null;
        references = Array.AsReadOnly((referenceWindows ?? []).Where(t => t.Handle != source.Handle).ToArray());
        if (!manager.CanSelectReference(source, out sourceDesktop, out string message)) throw new InvalidOperationException(message);
        Title = "PaneSet · 单窗放置"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.Manual;
        FontFamily = reference.FontFamily; FontSize = 14;
        SetResourceReference(BackgroundProperty, "Canvas"); SetResourceReference(ForegroundProperty, "Ink");
        Resources[typeof(Button)] = reference.FindResource(typeof(Button));
        Resources[typeof(TextBox)] = reference.FindResource(typeof(TextBox));
        root = new Grid { Margin = new Thickness(18), Focusable = true }; Content = root;
        var content = new StackPanel();
        root.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false });
        var heading = Text("单窗放置", "HeaderInk", 23); heading.FontWeight = FontWeights.SemiBold;
        content.Children.Add(heading);
        var title = Text(source.Title, "Ink"); title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.ToolTip = source.Application + " · " + source.Title; title.Margin = new Thickness(0, 8, 0, 0); content.Children.Add(title);
        if (referenceWindows != null && positions != null)
        {
            var modes = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(0, 12, 0, 0) };
            referenceModeButton = new Button { Content = "借用窗口位置", Margin = new Thickness(0, 0, 4, 0) };
            layoutModeButton = new Button { Content = "上次布局位置", Margin = new Thickness(4, 0, 0, 0) };
            AutomationProperties.SetName(referenceModeButton, "借用窗口位置"); AutomationProperties.SetName(layoutModeButton, "上次布局位置");
            referenceModeButton.Click += (_, _) => SelectReferenceMode(true);
            layoutModeButton.Click += (_, _) => SelectReferenceMode(false);
            modes.Children.Add(referenceModeButton); modes.Children.Add(layoutModeButton); content.Children.Add(modes);
        }
        destination = Text("", "Ink"); destination.Margin = new Thickness(0, 10, 0, 0); content.Children.Add(destination);
        hint = Text("", "Muted", 12); hint.Margin = new Thickness(0, 5, 0, 12); content.Children.Add(hint);
        var searchLabel = Text("搜索窗口 · 应用名或标题 · Ctrl+F", "Muted", 12);
        searchLabel.Margin = new Thickness(0, 0, 0, 4); referenceListContent.Children.Add(searchLabel);
        AutomationProperties.SetName(search, "搜索参照窗口"); search.ToolTip = "按应用名或窗口标题搜索；数字也可作为搜索文字。";
        search.TextChanged += (_, _) => BuildReferences(); referenceListContent.Children.Add(search);
        referenceListContent.Children.Add(new ScrollViewer { Content = referenceList, MaxHeight = 336, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false });
        referenceBody.ColumnDefinitions.Add(new()); referenceBody.ColumnDefinitions.Add(new() { Width = new GridLength(16) });
        referenceBody.ColumnDefinitions.Add(new() { Width = new GridLength(280) });
        referenceBody.RowDefinitions.Add(new() { Height = GridLength.Auto }); referenceBody.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var previewHeading = Text("目标位置预览", "HeaderInk", 16); previewHeading.FontWeight = FontWeights.SemiBold;
        previewContent.Children.Add(previewHeading);
        previewTitle.TextWrapping = TextWrapping.NoWrap; previewTitle.TextTrimming = TextTrimming.CharacterEllipsis;
        previewTitle.Margin = new Thickness(0, 8, 0, 0); previewContent.Children.Add(previewTitle);
        previewContent.Children.Add(positionPreview); previewContent.Children.Add(previewDetail);
        var previewHint = Text("绿色区域是要借用的位置。\n指向或用 Tab 聚焦窗口可切换预览；\n点击或按编号才会放置。", "Muted", 12);
        previewHint.Margin = new Thickness(0, 12, 0, 0); previewContent.Children.Add(previewHint);
        referenceBody.Children.Add(referenceListContent); referenceBody.Children.Add(previewContent);
        referenceContent = referenceBody; content.Children.Add(referenceContent);
        if (positions != null) { layoutContent = BuildDiagram(positions); content.Children.Add(layoutContent); }
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "取消 · Esc", Padding = new Thickness(12, 6, 12, 6) };
        cancel.Click += (_, _) => Cancel(); AutomationProperties.SetName(cancel, "取消单窗放置");
        DockPanel.SetDock(cancel, Dock.Right); footer.Children.Add(cancel);
        var navigation = Text("Tab 选择，Enter / Space 放置\n编号 10 及以上请点击或用 Tab 选择", "Muted", 12);
        navigation.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(navigation); content.Children.Add(footer);
        BuildReferences(); SelectReferenceMode(referenceMode);
        SizeChanged += (_, _) => UpdateReferenceLayout();
        UpdateReferenceLayout();
        SourceInitialized += (_, _) =>
        {
            nint handle = new WindowInteropHelper(this).Handle;
            var sourceWork = WindowManager.DisplayFor(source.Handle).WorkArea;
            Native.SetWindowPos(handle, 0, sourceWork.X, sourceWork.Y, 1, 1, 0x14);
            double dpi = Native.GetDpiForWindow(handle) / 96d;
            if (!double.IsFinite(dpi) || dpi <= 0) throw new InvalidOperationException("无法读取源屏幕缩放，请重新唤起。");
            int width = Math.Min(sourceWork.Width, (int)Math.Round((referenceWindows != null ? 900 : 540) * dpi));
            int height = Math.Min(sourceWork.Height, (int)Math.Round((referenceWindows != null ? 640 : 470) * dpi));
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
            if (referenceMode && key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && IsReady && ContainsForeground() && IsKeyboardFocusWithin)
            { e.Handled = true; search.Focus(); search.SelectAll(); return; }
            if (key is Key.Escape or Key.Enter or Key.Space or >= Key.D0 and <= Key.D9 or >= Key.NumPad0 and <= Key.NumPad9)
            {
                if (!IsReady || !ContainsForeground() || !IsKeyboardFocusWithin || e.IsRepeat || Keyboard.Modifiers != ModifierKeys.None)
                { e.Handled = true; return; }
                if (key == Key.Escape) { e.Handled = true; Cancel(); }
                else if (Keyboard.FocusedElement == search) { if (key == Key.Enter) e.Handled = true; }
                else if (input.PositionKey(key, Keyboard.Modifiers, e.IsRepeat) is int index) { e.Handled = true; Submit(index); }
                else if (key is Key.Enter or Key.Space && Keyboard.FocusedElement is not Button) e.Handled = true;
            }
        };
    }

    private static TextBlock Text(string value, string color, double size = 14)
    {
        var text = new TextBlock { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap };
        text.SetResourceReference(TextBlock.ForegroundProperty, color); return text;
    }

    internal void SelectReferenceMode(bool value)
    {
        if (!value && Positions == null) return;
        referenceMode = value;
        referenceContent.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        if (layoutContent != null) layoutContent.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        destination.Text = value ? "选择一扇窗口，借用它当前的位置和大小" : "放到" + Positions!.Display.Name + " · 上次记住的布局位置";
        hint.Text = value ? "参照窗口留在后面。列表尺寸仅供参考，提交时重新读取；其他窗口不动。" : "点击位置或独立按 1–9，立即移动并缩放。原窗口留在后面。";
        referenceModeButton?.SetResourceReference(BackgroundProperty, value ? "MintPale" : "Canvas");
        layoutModeButton?.SetResourceReference(BackgroundProperty, value ? "Canvas" : "MintPale");
        input.Update(false, false);
        if (IsVisible) root.Focus();
    }

    private void UpdateReferenceLayout()
    {
        bool narrow = ActualWidth < 780;
        referenceBody.ColumnDefinitions[1].Width = new GridLength(narrow ? 0 : 16);
        referenceBody.ColumnDefinitions[2].Width = new GridLength(narrow ? 0 : 280);
        Grid.SetRow(referenceListContent, narrow ? 1 : 0); Grid.SetColumnSpan(referenceListContent, narrow ? 3 : 1);
        Grid.SetColumn(previewContent, narrow ? 0 : 2); Grid.SetColumnSpan(previewContent, narrow ? 3 : 1);
        previewContent.Margin = new Thickness(0, 0, 0, narrow ? 12 : 0);
        positionPreview.Height = narrow ? 120 : 180;
    }

    private void PreviewReference(int index)
    {
        if (!referenceRows.TryGetValue(index, out var row) || !row.Button.IsVisible) return;
        previewIndex = index; RefreshReferences();
    }

    private void BuildReferences()
    {
        referenceRows.Clear(); referenceList.Children.Clear();
        string query = search.Text.Trim();
        for (int i = 0; i < references.Count; i++)
        {
            int index = i; var target = references[i];
            if (!(target.Application + " " + target.Title).Contains(query, StringComparison.CurrentCultureIgnoreCase)) continue;
            var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = new GridLength(34) }); row.ColumnDefinitions.Add(new());
            var number = Text((i + 1).ToString(), "HeaderInk", 18); number.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(number);
            var labels = new StackPanel(); Grid.SetColumn(labels, 1); row.Children.Add(labels);
            var caption = Text(target.Title, "Ink"); caption.TextWrapping = TextWrapping.NoWrap; caption.TextTrimming = TextTrimming.CharacterEllipsis;
            var detail = Text("", "Muted", 12); detail.TextWrapping = TextWrapping.NoWrap; detail.TextTrimming = TextTrimming.CharacterEllipsis;
            labels.Children.Add(caption); labels.Children.Add(detail);
            var button = new Button { Content = row, MinHeight = 56, Margin = new Thickness(0, 0, 0, 4), Padding = new Thickness(10, 5, 10, 5),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = target.Application + " · " + target.Title };
            AutomationProperties.SetName(button, $"借用窗口 {i + 1} · {target.Application} · {target.Title}"); button.Click += (_, _) => Submit(index);
            button.MouseEnter += (_, _) => PreviewReference(index);
            button.GotKeyboardFocus += (_, _) => PreviewReference(index);
            referenceRows[index] = (button, detail); referenceList.Children.Add(button);
        }
        if (referenceRows.Count == 0) referenceList.Children.Add(Text(references.Count == 0
            ? "没有其他合格窗口。请先打开一扇参照窗口，再重新唤起。" : "没有匹配的窗口。请修改搜索文字。", "Muted"));
        if (!referenceRows.ContainsKey(previewIndex)) previewIndex = -1;
        RefreshReferences();
    }

    private void RefreshReferences()
    {
        DisplayArea[] displays;
        try { displays = WindowManager.ListDisplays().ToArray(); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            foreach (var row in referenceRows.Values) { row.Button.IsEnabled = false; row.Detail.Text = "无法读取显示信息，请重新唤起。"; }
            positionPreview.Clear(); previewTitle.Text = "无法读取位置"; previewDetail.Text = "显示环境已改变，请重新唤起。";
            return;
        }
        positionPreview.Clear(); previewTitle.Text = "没有可预览的位置"; previewTitle.ToolTip = null; previewDetail.Text = "请搜索或选择其他窗口。";
        foreach (var (index, row) in referenceRows)
        {
            var target = references[index];
            try
            {
                bool valid = manager.TryReadReference(target, out var state, out string message);
                row.Button.IsEnabled = valid;
                var display = valid ? WindowManager.DisplayFor(target.Handle) : null;
                string? displayName = displays.FirstOrDefault(d => d.Handle == display?.Handle)?.Name;
                row.Detail.Text = valid ? target.Application + " · " + displayName
                    + $" · {state!.Bounds.Width} × {state.Bounds.Height}" : message;
                if (previewIndex == -1 && valid) previewIndex = index;
                if (index == previewIndex)
                {
                    previewTitle.Text = $"{index + 1} · {target.Title}"; previewTitle.ToolTip = target.Title;
                    previewDetail.Text = valid ? displayName + $"\n目标大小 {state!.Bounds.Width} × {state.Bounds.Height}" : message;
                    if (valid)
                    {
                        positionPreview.ShowPosition(display!.WorkArea, state!.Bounds);
                    }
                }
                row.Button.SetResourceReference(BackgroundProperty, index == previewIndex ? "MintPale" : "Surface");
                AutomationProperties.SetHelpText(row.Button, row.Detail.Text + (valid
                    ? $" · 工作区内距左 {(double)state!.Bounds.X - display!.WorkArea.X:0} 像素，距上 {(double)state.Bounds.Y - display.WorkArea.Y:0} 像素" : ""));
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            { row.Button.IsEnabled = false; row.Detail.Text = "参照窗口或显示环境已改变，请重新唤起。"; }
        }
    }

    private FrameworkElement BuildDiagram(LayoutPositions positions)
    {
        var work = positions.Display.WorkArea;
        double scale = Math.Max(450d / work.Width, positions.Regions.Max(r => Math.Max(44d / r.Width, 44d / r.Height)));
        var diagram = new Canvas { Width = work.Width * scale, Height = work.Height * scale };
        for (int i = 0; i < positions.Regions.Count; i++)
        {
            int index = i; var rect = positions.Regions[i];
            var button = new Button { Content = (i + 1).ToString(), Width = rect.Width * scale, Height = rect.Height * scale,
                MinHeight = 44, Padding = new Thickness(0), FontSize = 20, FontWeight = FontWeights.SemiBold };
            button.SetResourceReference(BackgroundProperty, "MintPale"); button.SetResourceReference(ForegroundProperty, "Ink");
            AutomationProperties.SetName(button, $"放到位置 {i + 1}"); button.ToolTip = $"位置 {i + 1} · {rect.Width} × {rect.Height}";
            button.Click += (_, _) => Submit(index); Canvas.SetLeft(button, (rect.X - work.X) * scale); Canvas.SetTop(button, (rect.Y - work.Y) * scale);
            diagram.Children.Add(button);
        }
        return new ScrollViewer { Content = diagram, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
    }

    private bool ContainsForeground() => Native.GetForegroundWindow() == new WindowInteropHelper(this).Handle;
    private bool Validate(out string message)
    {
        if (!referenceMode) return manager.CanPlace(Source, Positions!.Version, out message);
        if (!manager.CanSelectReference(Source, out var desktop, out message)) return false;
        if (desktop == sourceDesktop) return true;
        message = "源窗口所在桌面已改变，请重新唤起。"; return false;
    }
    private void Poll()
    {
        if (finished) return;
        if (!ContainsForeground() || !IsKeyboardFocusWithin) { Cancel("已离开单窗放置，未移动窗口。"); return; }
        bool released = new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, (int)(chord?.Key ?? 0) }
            .Where(k => k != 0).All(k => (Native.GetAsyncKeyState(k) & 0x8000) == 0);
        input.Update(true, released);
        if (++ticks % 5 == 0)
        {
            if (!Validate(out string message) || !manager.IsPlacementDesktop(new WindowInteropHelper(this).Handle,
                referenceMode ? sourceDesktop : Positions!.DesktopId))
            { Cancel(message.Length > 0 ? message : "单窗放置所在桌面已改变，请重新唤起。", true); return; }
            if (referenceMode) RefreshReferences();
        }
    }
    private void Submit(int index)
    {
        if (!IsReady || !ContainsForeground() || !IsKeyboardFocusWithin || index < 0) return;
        if (!Validate(out string message) || !manager.IsPlacementDesktop(new WindowInteropHelper(this).Handle,
            referenceMode ? sourceDesktop : Positions!.DesktopId))
        { Cancel(message.Length > 0 ? message : "当前桌面已改变，请重新唤起。", true); return; }
        if (referenceMode)
        {
            if (!referenceRows.TryGetValue(index, out var row) || !row.Button.IsVisible || !row.Button.IsEnabled) return;
            var target = references[index];
            if (!manager.TryReadReference(target, out _, out message)) { row.Button.IsEnabled = false; row.Detail.Text = message; return; }
            Finish(new(null, "", Reference: target));
        }
        else if (index < Positions!.Regions.Count) Finish(new(index, ""));
    }
    public void Cancel(string message = "已取消单窗放置，未移动窗口，原恢复记录保留。", bool failed = false) => Finish(new(null, message, failed));
    private void Finish(PlacementChoice choice, bool close = true)
    {
        if (finished) return;
        finished = true; refresh.Stop(); if (close) Close(); Completed?.Invoke(choice);
    }
}
