using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PaneSet;

internal sealed record QuickSelectionResult(WindowTarget[] Targets, ArrangementKind Layout, bool Confirmed, string Message, bool Failed = false);

internal sealed class QuickSelectWindow : Window
{
    private readonly WindowManager manager;
    private readonly DisplayArea display;
    private readonly WindowTarget[] captured;
    private readonly List<WindowRow> rows = [], selected = [];
    private readonly HashSet<WindowRow> invalid = [];
    private sealed class Card
    {
        public required CheckBox Check;
        public required Border Slot;
        public required TextBlock Note, Fallback;
        public WindowThumbnail? Thumbnail;
    }
    private readonly Dictionary<WindowRow, Card> controls = [];
    private readonly WrapPanel candidates = new();
    private readonly StackPanel selection = new();
    private readonly Dictionary<WindowRow, (TextBlock Title, Button Up, Button Down)> selectedControls = [];
    private readonly ScrollViewer candidateScroll, selectedScroll;
    private readonly Canvas preview = new() { Height = 120, IsHitTestVisible = false, ClipToBounds = true };
    private readonly TextBox search = new() { MinHeight = 36, Padding = new Thickness(8), Margin = new Thickness(0, 8, 0, 12) };
    private readonly TextBlock summary = new(), status = new() { TextWrapping = TextWrapping.Wrap }, empty = new() { Text = "没有匹配的窗口。可尝试应用名或另一段标题。", Margin = new Thickness(8) };
    private readonly ComboBox layout = new() { MinHeight = 36, Margin = new Thickness(0, 8, 0, 8) };
    private readonly Button apply;
    private readonly DispatcherTimer refresh;
    private bool finished, updating, layoutUsable, thumbnailUpdate;
    public bool IsReady { get; private set; }
    public event Action<QuickSelectionResult>? Completed;
    public ArrangementKind Kind => layout.SelectedItem is ArrangementKind kind ? kind : ArrangementKind.Automatic;

    public QuickSelectWindow(Window reference, WindowManager manager, DisplayArea display, IReadOnlyList<WindowTarget> targets,
        nint foreground, ArrangementKind initialKind)
    {
        this.manager = manager; this.display = display;
        // Capture and sort once. Refreshes may update titles, never reshuffle a user's targets.
        captured = targets.Select((target, index) => (target, index,
                bounds: manager.TryReadOnScreen(target, display, out var state) ? state!.Bounds : new WindowRect(int.MaxValue, int.MaxValue, 0, 0)))
            .OrderBy(x => x.target.Handle == foreground ? 0 : 1).ThenBy(x => x.bounds.Y).ThenBy(x => x.bounds.X).ThenBy(x => x.index)
            .Select(x => x.target).ToArray();
        Title = "PaneSet · 本屏窗口总览"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; WindowStartupLocation = WindowStartupLocation.Manual;
        Background = new SolidColorBrush(Color.FromRgb(244, 245, 241)); FontFamily = reference.FontFamily; FontSize = 14;
        Resources[typeof(Button)] = reference.FindResource(typeof(Button));
        Resources[typeof(CheckBox)] = reference.FindResource(typeof(CheckBox));
        var root = new Grid { Margin = new Thickness(24) }; Content = root;
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new());
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = "选择要一起看的窗口", FontSize = 25, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "被遮挡的窗口也在这里。编号对应排布顺序。", Margin = new Thickness(0, 6, 0, 0) });
        AutomationProperties.SetName(search, "搜索本屏窗口"); search.ToolTip = "搜索应用名或窗口标题 · Ctrl+F";
        search.SetValue(AutomationProperties.HelpTextProperty, "按应用名或窗口标题搜索；筛选不取消已选窗口。");
        var searchRow = new DockPanel();
        searchRow.Children.Add(new TextBlock { Text = "搜索", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 4) });
        searchRow.Children.Add(search); heading.Children.Add(searchRow); root.Children.Add(heading);
        var body = new Grid(); Grid.SetRow(body, 1); root.Children.Add(body);
        body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = new GridLength(300) });
        var candidateArea = new Grid { Margin = new Thickness(0, 0, 20, 0) }; body.Children.Add(candidateArea);
        candidateScroll = new ScrollViewer { Content = candidates, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false };
        candidateArea.Children.Add(candidateScroll); candidateArea.Children.Add(empty);
        var side = new Grid { Background = new SolidColorBrush(Color.FromRgb(233, 238, 230)), Margin = new Thickness(0) };
        Grid.SetColumn(side, 1); body.Children.Add(side);
        side.RowDefinitions.Add(new() { Height = GridLength.Auto }); side.RowDefinitions.Add(new()); side.RowDefinitions.Add(new() { Height = GridLength.Auto });
        summary.FontSize = 18; summary.Margin = new Thickness(10); side.Children.Add(summary);
        selectedScroll = new ScrollViewer { Content = selection, Margin = new Thickness(12, 0, 12, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(selectedScroll, 1); side.Children.Add(selectedScroll);
        var actions = new StackPanel { Margin = new Thickness(14) }; Grid.SetRow(actions, 2); side.Children.Add(actions);
        actions.Children.Add(new TextBlock { Text = "排布方式" });
        layout.ItemsSource = Enum.GetValues<ArrangementKind>(); layout.SelectedItem = initialKind;
        var label = new FrameworkElementFactory(typeof(TextBlock)); label.SetBinding(TextBlock.TextProperty, new Binding { Converter = new LayoutLabelConverter() });
        layout.ItemTemplate = new DataTemplate { VisualTree = label };
        AutomationProperties.SetName(layout, "快速选窗布局"); actions.Children.Add(layout); actions.Children.Add(preview);
        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        apply = MakeButton("排布 · Enter", "确认本屏选窗", (_, _) => Submit()); buttons.Children.Add(apply);
        buttons.Children.Add(MakeButton("取消 · Esc", "取消本屏选窗", (_, _) => Finish(false, "已取消，已有布局与恢复记录保留。")));
        actions.Children.Add(buttons); status.FontSize = 12; actions.Children.Add(status);
        updating = true;
        foreach (var target in captured) AddRow(target);
        if (rows.FirstOrDefault(r => r.Target.Handle == foreground) is { } first) first.Selected = true;
        updating = false;
        refresh = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => RefreshCandidates(), Dispatcher);
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            // Move the hidden HWND onto the target monitor before reading its effective DPI.
            Native.SetWindowPos(handle, 0, display.WorkArea.X, display.WorkArea.Y, 0, 0, 0x15);
            var bounds = InitialBounds(display.WorkArea, captured.Length, Native.GetDpiForWindow(handle) / 96d);
            Native.SetWindowPos(handle, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x14);
        };
        SizeChanged += (_, _) =>
        {
            body.ColumnDefinitions[1].Width = new GridLength(ActualWidth < 850 ? 260 : 300);
            preview.Height = ActualHeight < 650 ? 60 : 120;
            preview.Visibility = ActualHeight < 600 ? Visibility.Collapsed : Visibility.Visible;
            if (IsReady) RenderPreview();
        };
        candidateScroll.SizeChanged += (_, _) => SizeCards();
        candidateScroll.ScrollChanged += (_, _) => UpdateThumbnails();
        LayoutUpdated += (_, _) => UpdateThumbnails();
        search.TextChanged += (_, _) => Filter();
        ContentRendered += (_, _) =>
        {
            if (!ContainsForeground()) { Finish(false, "未取得键盘焦点，请从任务栏打开 PaneSet 后重试。", failed: true); return; }
            search.Focus();
            if (!IsKeyboardFocusWithin) { Finish(false, "未取得键盘焦点，请从任务栏打开 PaneSet 后重试。", failed: true); return; }
            IsReady = true; RefreshCandidates(); refresh.Start();
        };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        { if (IsReady && !finished && !ContainsForeground()) Finish(false, "已离开本屏选窗，未移动窗口。"); }));
        Closed += (_, _) => { refresh.Stop(); ReleaseThumbnails(); if (!finished) Finish(false, "已关闭本屏选窗，未移动窗口。", false); };
        layout.SelectionChanged += (_, _) => { if (!updating) RenderPreview(); };
        layout.DropDownOpened += (_, _) => UpdateThumbnails(); layout.DropDownClosed += (_, _) => UpdateThumbnails();
        PreviewKeyDown += (_, e) =>
        {
            if (!IsReady || finished) return;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape) { e.Handled = true; Finish(false, "已取消，已有布局与恢复记录保留。"); }
            else if (Keyboard.Modifiers == ModifierKeys.Control && key == Key.F) { search.Focus(); search.SelectAll(); e.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.D1 or Key.D2 or Key.D3)
            { layout.SelectedItem = key == Key.D1 ? ArrangementKind.Columns : key == Key.D2 ? ArrangementKind.MainAndOthers : ArrangementKind.Grid; e.Handled = true; }
            else if (key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && !layout.IsDropDownOpen
                && !search.IsKeyboardFocusWithin && Keyboard.FocusedElement is not Button)
            { e.Handled = true; Submit(); }
        };
    }

    internal static WindowRect InitialBounds(WindowRect work, int count, double scale)
    {
        if (work.Width <= 0 || work.Height <= 0 || !double.IsFinite(scale) || scale <= 0 || count < 0)
            throw new ArgumentOutOfRangeException(nameof(work));
        // 290 per card including margin; 388 reserves the rail, padding, gap and scrollbar.
        double maxWidth = Math.Max(1, work.Width / scale - 48);
        double maxHeight = Math.Max(1, work.Height / scale - 48);
        int desiredColumns = count <= 3 ? Math.Max(1, count) : Math.Min(3, (int)Math.Ceiling(Math.Sqrt(count)));
        double width = Math.Min(maxWidth, 388 + desiredColumns * 290);
        int columns = Math.Max(1, (int)((width - 388) / 290));
        int visibleRows = Math.Min(3, (int)Math.Ceiling(Math.Max(1, count) / (double)columns));
        double height = Math.Min(maxHeight, Math.Min(900, Math.Max(560, 164 + visibleRows * 280)));
        int physicalWidth = Math.Min(work.Width, Math.Max(1, (int)Math.Round(width * scale)));
        int physicalHeight = Math.Min(work.Height, Math.Max(1, (int)Math.Round(height * scale)));
        return new(work.X + (work.Width - physicalWidth) / 2, work.Y + (work.Height - physicalHeight) / 2, physicalWidth, physicalHeight);
    }

    private Button MakeButton(string text, string name, RoutedEventHandler click)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 6, 8, 6) };
        AutomationProperties.SetName(button, name); button.Click += click; return button;
    }
    private bool ContainsForeground()
    {
        nint own = new WindowInteropHelper(this).Handle, foreground = Native.GetForegroundWindow();
        var seen = new HashSet<nint>();
        while (foreground != 0 && seen.Add(foreground))
        { if (foreground == own) return true; foreground = Native.GetWindow(foreground, 4); }
        return false;
    }
    private void AddRow(WindowTarget target)
    {
        var row = new WindowRow(target, Changed); rows.Add(row);
        var checkbox = new CheckBox { DataContext = row, Width = 280, Height = 270, Margin = new Thickness(5), Padding = new Thickness(8),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Background = Brushes.White };
        checkbox.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(WindowRow.Selected)) { Mode = BindingMode.TwoWay });
        var content = new Grid(); content.RowDefinitions.Add(new() { Height = new GridLength(150) }); content.RowDefinitions.Add(new()); content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var fallback = new TextBlock { Text = "内容预览不可用\n仍可按标题选择", TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        var slot = new Border { Background = new SolidColorBrush(Color.FromRgb(230, 235, 228)), Child = fallback, IsHitTestVisible = false };
        content.Children.Add(slot);
        var title = new TextBlock { TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 8, 0, 4) };
        title.SetBinding(TextBlock.TextProperty, new Binding("Target.Title")); Grid.SetRow(title, 1); content.Children.Add(title);
        var note = new TextBlock { FontSize = 12, Foreground = Brushes.DarkSlateGray, TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(note, 2); content.Children.Add(note);
        checkbox.Content = content; candidates.Children.Add(checkbox); controls[row] = new Card { Check = checkbox, Slot = slot, Note = note, Fallback = fallback };
    }
    private void SizeCards()
    {
        double width = Math.Max(1, candidateScroll.ViewportWidth);
        int columns = Math.Max(1, (int)(width / 290));
        double cardWidth = Math.Max(1, width / columns - 10);
        foreach (var control in controls.Values) control.Check.Width = cardWidth;
        UpdateThumbnails();
    }
    private void Filter()
    {
        foreach (var row in rows)
        {
            bool match = (row.Target.Title + " " + row.Target.Application).Contains(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase);
            controls[row].Check.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
        }
        empty.Visibility = controls.Values.Any(c => c.Check.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
        UpdateThumbnails();
    }
    private void Changed(WindowRow row)
    {
        if (row.Selected) selected.Add(row); else selected.Remove(row);
        if (!updating) RefreshCandidates();
    }
    private void Move(WindowRow row, int delta)
    {
        int index = selected.IndexOf(row), next = index + delta;
        if (index < 0 || next < 0 || next >= selected.Count) return;
        var focused = Keyboard.FocusedElement as UIElement;
        selected.RemoveAt(index); selected.Insert(next, row); RefreshCandidates();
        if (IsReady && ContainsForeground() && focused?.IsEnabled == true) focused.Focus();
    }
    private void RefreshCandidates()
    {
        if (finished) return;
        if (IsReady && !ContainsForeground()) { Finish(false, "已离开本屏选窗，未移动窗口。"); return; }
        try
        {
            if (!WindowManager.ListDisplays().Any(d => d.Handle == display.Handle && d.WorkArea == display.WorkArea))
            { Finish(false, "目标屏幕或工作区域已改变，请重新进入选窗。", failed: true); return; }
            var current = manager.ListWindows(display).Where(t => captured.Any(c => SameIdentity(c, t))).ToArray();
            invalid.Clear(); updating = true;
            try
            {
                foreach (var row in rows)
                {
                    var value = current.FirstOrDefault(t => SameIdentity(row.Target, t));
                    if (value != null) row.RefreshTarget(value); else invalid.Add(row);
                }
            }
            finally { updating = false; }
            foreach (var row in rows)
            {
                var control = controls[row]; row.Order = selected.IndexOf(row) + 1;
                AutomationProperties.SetName(control.Check, row.Target.Title); control.Check.ToolTip = row.Target.Title;
                control.Check.IsEnabled = !invalid.Contains(row) || row.Selected;
                control.Note.Text = (row.Selected ? row.Order + " · " : "") + row.Target.Application + (invalid.Contains(row) ? " · 已失效" : "");
                control.Check.Background = row.Selected ? new SolidColorBrush(Color.FromRgb(212, 228, 208)) : Brushes.White;
                if (invalid.Contains(row)) { control.Thumbnail?.Dispose(); control.Thumbnail = null; control.Fallback.Text = "窗口已失效"; }
                else
                {
                    control.Fallback.Text = "内容预览不可用\n仍可按标题选择";
                    control.Thumbnail ??= new WindowThumbnail(new WindowInteropHelper(this).Handle, row.Target.Handle);
                }
            }
            RenderSelection(); Filter(); SizeCards(); RenderPreview(); UpdateThumbnails();
            if (IsReady && ContainsForeground() && !IsKeyboardFocusWithin) search.Focus();
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { Finish(false, "无法刷新选窗：" + e.Message, failed: true); }
    }
    private void RenderSelection()
    {
        foreach (var removed in selectedControls.Keys.Where(r => !selected.Contains(r)).ToArray()) selectedControls.Remove(removed);
        foreach (var row in selected)
        {
            if (!selectedControls.ContainsKey(row))
            {
                var title = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6) };
                var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10), Tag = row }; panel.Children.Add(title);
                var actions = new WrapPanel(); panel.Children.Add(actions);
                var up = MakeButton("前移", "前移 · " + row.Target.Title, (_, _) => Move(row, -1)); actions.Children.Add(up);
                var down = MakeButton("后移", "后移 · " + row.Target.Title, (_, _) => Move(row, 1)); actions.Children.Add(down);
                actions.Children.Add(MakeButton("取消选择", "取消选择 · " + row.Target.Title, (_, _) => row.Selected = false));
                selectedControls[row] = (title, up, down); selection.Children.Add(panel);
            }
            var controls = selectedControls[row]; controls.Title.Text = $"{row.Order} · {row.Target.Title}" + (invalid.Contains(row) ? " · 已失效，请取消该成员" : "");
            controls.Up.IsEnabled = selected.IndexOf(row) > 0; controls.Down.IsEnabled = selected.IndexOf(row) < selected.Count - 1;
            AutomationProperties.SetName(controls.Up, "前移 · " + row.Target.Title); AutomationProperties.SetName(controls.Down, "后移 · " + row.Target.Title);
        }
        foreach (var panel in selection.Children.OfType<StackPanel>().ToArray())
            if (!selected.Contains((WindowRow)panel.Tag)) selection.Children.Remove(panel);
        for (int i = 0; i < selected.Count; i++)
        {
            var panel = selection.Children.OfType<StackPanel>().Single(p => p.Tag == selected[i]);
            if (selection.Children.IndexOf(panel) != i) { selection.Children.Remove(panel); selection.Children.Insert(i, panel); }
        }
    }
    private void UpdateThumbnails()
    {
        if (!IsReady || finished || thumbnailUpdate || !candidateScroll.IsArrangeValid) return;
        thumbnailUpdate = true;
        try
        {
            var scale = VisualTreeHelper.GetDpi(this);
            var presenter = FindPresenter(candidateScroll);
            if (presenter == null) return;
            Rect Physical(FrameworkElement element) { var p = element.TranslatePoint(new Point(), this); return new Rect(p.X * scale.DpiScaleX, p.Y * scale.DpiScaleY, element.ActualWidth * scale.DpiScaleX, element.ActualHeight * scale.DpiScaleY); }
            var viewport = Physical(presenter);
            foreach (var pair in controls)
            {
                var card = pair.Value;
                bool available = card.Thumbnail?.Update(Physical(card.Slot), viewport,
                    card.Check.IsVisible && !invalid.Contains(pair.Key) && !layout.IsDropDownOpen) == true;
                card.Fallback.Visibility = available ? Visibility.Hidden : Visibility.Visible;
            }
        }
        finally { thumbnailUpdate = false; }
    }
    private static ScrollContentPresenter? FindPresenter(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollContentPresenter presenter) return presenter;
            if (FindPresenter(child) is { } found) return found;
        }
        return null;
    }
    private void ReleaseThumbnails() { foreach (var card in controls.Values) card.Thumbnail?.Dispose(); }
    private void RenderPreview()
    {
        preview.Children.Clear(); layoutUsable = false;
        bool invalidSelection = selected.Any(invalid.Contains);
        summary.Text = $"已选 {selected.Count} 扇 / 本屏 {rows.Count} 扇";
        status.Text = invalidSelection ? "成员已失效，请明确取消或重新选择。" : selected.Count < 2 ? "至少选择两扇窗口。" : "编号对应顺序。Enter 确认，Esc 取消。";
        try
        {
            var areas = LayoutEngine.Arrange(display.WorkArea, Math.Max(2, selected.Count), Kind);
            layoutUsable = selected.Count >= 2 && !invalidSelection && areas.All(r => r.Width >= 240 && r.Height >= 160);
            if (selected.Count >= 2 && !areas.All(r => r.Width >= 240 && r.Height >= 160)) status.Text = "目标窗口尺寸过小，请减少选择或改用网格。";
            double factor = Math.Min(Math.Max(1, preview.ActualWidth) / display.WorkArea.Width, preview.Height / display.WorkArea.Height);
            for (int i = 0; i < selected.Count && !invalidSelection; i++)
            {
                var rect = areas[i];
                var cell = new Border { Width = rect.Width * factor, Height = rect.Height * factor,
                    Background = new SolidColorBrush(Color.FromRgb(185, 208, 174)), BorderBrush = Brushes.SeaGreen, BorderThickness = new Thickness(1),
                    Child = new TextBlock { Text = (i + 1).ToString(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                Canvas.SetLeft(cell, (rect.X - display.WorkArea.X) * factor); Canvas.SetTop(cell, (rect.Y - display.WorkArea.Y) * factor); preview.Children.Add(cell);
            }
        }
        catch (ArgumentOutOfRangeException e) { status.Text = e.Message; }
        apply.IsEnabled = IsReady && layoutUsable;
    }
    private void Submit() { RefreshCandidates(); if (!finished && IsReady && layoutUsable) Finish(true, ""); }
    private void Finish(bool confirmed, string message, bool close = true, bool failed = false)
    {
        if (finished) return;
        failed = failed && (!IsReady || ContainsForeground()); finished = true; IsReady = false; refresh.Stop(); ReleaseThumbnails();
        var result = new QuickSelectionResult(selected.Select(r => r.Target).ToArray(), Kind, confirmed, message, failed);
        if (close) Close(); Completed?.Invoke(result);
    }
    private static bool SameIdentity(WindowTarget a, WindowTarget b) => a.Handle == b.Handle && a.Generation == b.Generation
        && a.ProcessId == b.ProcessId && a.ThreadId == b.ThreadId && a.Started == b.Started && a.ClassName == b.ClassName;
    private sealed class LayoutLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value switch {
            ArrangementKind.Automatic => "自动布局", ArrangementKind.Columns => "等宽 · Alt+1", ArrangementKind.MainAndOthers => "主辅 · Alt+2", _ => "网格 · Alt+3" };
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}
