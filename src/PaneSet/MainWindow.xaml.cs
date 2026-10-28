using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PaneSet;

public partial class MainWindow : Window
{
    private readonly WindowManager manager;
    private DisplayArea display;
    private readonly ObservableCollection<WindowRow> rows = [];
    private readonly List<WindowRow> selected = [];
    private readonly ICollectionView view;
    private readonly DispatcherTimer timer;
    private readonly DispatcherTimer themeTimer;
    private readonly string settingsPath;
    private AppearanceMode themeMode;
    private PanelShortcuts? shortcuts;
    private SettingsWindow? settingsWindow;
    private TrayIcon? tray;
    private QuickSelectWindow? selectionWindow;
    private ArrangementKind quickLayout = ArrangementKind.Automatic;
    private bool updating, busy, arranging, exitRequested, layoutUsable, closed;
    private readonly bool trayEnabled;
    private sealed record WindowGroup(WindowTarget[] Targets, DisplayArea Destination, ArrangementKind Layout);
    private WindowGroup? recentGroup, requestedGroup;
    private bool? compactLayout;
    private ArrangementKind Kind => LayoutPicker.SelectedItem is ComboBoxItem { Tag: ArrangementKind kind } ? kind : ArrangementKind.Automatic;

    public MainWindow(WindowManager manager, DisplayArea display, IReadOnlyList<WindowTarget> candidates, nint foreground = 0, bool enableShortcuts = true, string? settingsPath = null, bool? enableTray = null)
    {
        this.manager = manager; this.display = display;
        trayEnabled = enableTray ?? enableShortcuts;
        this.settingsPath = settingsPath ?? ShortcutSettings.DefaultPath;
        themeMode = ShortcutSettings.Load(this.settingsPath, out _).Theme;
        ThemeSupport.EnsureControls();
        UiPalette.Apply(ThemeSupport.ResolveDark(themeMode));
        InitializeComponent();
        ThemedChrome.Install(this);
        view = CollectionViewSource.GetDefaultView(rows);
        view.Filter = item => item is WindowRow row && (row.Target.Title.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase)
            || row.Target.Application.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase));
        WindowList.ItemsSource = view;
        LoadDisplays(display.Handle);
        SetCandidates(candidates, foreground);
        if (enableShortcuts) StartDragPlacementMonitor();
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, async (_, _) =>
        {
            if (!busy && manager.CanCollect)
            {
                busy = true;
                try { ShowReport(await manager.CompletePendingAsync()); }
                catch (Exception e) { requestedGroup = null; StatusText.Text = e.Message; }
                finally { busy = false; UpdateControls(); }
            }
        }, Dispatcher);
        themeTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background,
            (_, _) => { if (themeMode == AppearanceMode.System) ApplyTheme(); }, Dispatcher);
        themeTimer.Start();
        SourceInitialized += (_, _) =>
        {
            if (enableShortcuts) shortcuts = new(new WindowInteropHelper(this).Handle, DispatchShortcut,
                settingsPath, Enum.GetValues<ShortcutAction>());
            UpdateShortcutHint();
            CreateTray();
            PlaceOnDisplay();
        };
        Closing += (_, e) =>
        {
            if (!exitRequested) { e.Cancel = true; CollapseToTray(); return; }
            if ((busy || manager.HasPending) && MessageBox.Show(this,
                "目标窗口仍未响应。退出后无法恢复这次排布，目标稍后仍可能被移动。是否退出？",
                "退出 PaneSet", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            { e.Cancel = true; exitRequested = false; }
        };
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        Closed += (_, _) => { closed = true; dragFeedback?.Dispose(); dragMonitor?.Dispose(); tray?.Dispose(); selectionWindow?.Close(); settingsWindow?.Close(); recentGroup = requestedGroup = null; shortcuts?.Dispose(); timer.Stop(); themeTimer.Stop(); manager.Dispose(); };
        PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (TrySelectLayout(key, Keyboard.Modifiers)) e.Handled = true;
            else if (key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
            else if (key == Key.S && Keyboard.Modifiers == ModifierKeys.Alt && SwapButton.IsEnabled) { SwapClick(this, new()); e.Handled = true; }
            else if (key == Key.Escape) { CollapseToTray(); e.Handled = true; }
        };
        StatusText.Text = "可选择各屏幕上被遮挡的窗口。收起后仍在运行，退出会清除恢复记录。";
    }

    private void UpdateResponsiveLayout()
    {
        bool compact = ActualWidth < 760;
        BodyGrid.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 16);
        BodyGrid.ColumnDefinitions[2].Width = new GridLength(compact ? 0 : 244);
        BodyGrid.RowDefinitions[1].Height = compact ? GridLength.Auto : new GridLength(0);
        Grid.SetColumn(SettingsPane, compact ? 0 : 2);
        Grid.SetRow(SettingsPane, compact ? 1 : 0);
        Grid.SetColumnSpan(SettingsPane, compact ? 3 : 1);
        SettingsPane.Margin = compact ? new Thickness(0, 10, 0, 0) : new Thickness(0);
        SettingsFields.ColumnDefinitions[1].Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetRow(LayoutField, compact ? 0 : 1);
        Grid.SetColumn(LayoutField, compact ? 1 : 0);
        DisplayField.Margin = compact ? new Thickness(0, 0, 12, 0) : new Thickness(0, 0, 0, 16);
        if (compactLayout != compact) { PreviewExpander.IsExpanded = !compact; compactLayout = compact; }
        PreviewView.Height = ActualHeight < 680 ? 48 : 100;
        bool shortWindow = ActualHeight < 660;
        HeaderPanel.Padding = shortWindow && compact ? new Thickness(12, 8, 12, 8) : new Thickness(18, 14, 18, 14);
        HeaderPanel.Margin = shortWindow && compact ? new Thickness(0, 0, 0, 9) : new Thickness(0, 0, 0, 14);
        BrandIcon.Width = BrandIcon.Height = shortWindow && compact ? 36 : 42;
        BrandTitle.FontSize = shortWindow && compact ? 21 : 24;
        BrandSubtitle.Visibility = shortWindow && compact ? Visibility.Collapsed : Visibility.Visible;
        SettingsPane.Padding = shortWindow && compact ? new Thickness(8) : new Thickness(15);
        SettingsHeading.Visibility = shortWindow && compact ? Visibility.Collapsed : Visibility.Visible;
        LayoutHint.Visibility = shortWindow && compact ? Visibility.Collapsed : Visibility.Visible;
        PreviewExpander.Visibility = shortWindow && compact ? Visibility.Collapsed : Visibility.Visible;
        ShortcutText.Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        StatusView.MaxHeight = shortWindow ? 20 : 24;
    }

    private bool TrySelectLayout(Key key, ModifierKeys modifiers)
    {
        if (modifiers != ModifierKeys.Alt || busy || manager.HasPending || Picking) return false;
        ArrangementKind? kind = key switch { Key.D1 => ArrangementKind.Columns, Key.D2 => ArrangementKind.MainAndOthers, Key.D3 => ArrangementKind.Grid, _ => null };
        if (kind == null) return false;
        LayoutPicker.SelectedItem = LayoutPicker.Items.Cast<ComboBoxItem>().First(item => Equals(item.Tag, kind.Value));
        return true;
    }

    private void LoadDisplays(nint preferred, bool allowFallback = true)
    {
        updating = true;
        try
        {
            var displays = WindowManager.ListDisplays();
            DisplayPicker.ItemsSource = displays;
            var target = displays.FirstOrDefault(d => d.Handle == preferred) ?? (allowFallback ? displays.FirstOrDefault() : null);
            if (target != null) display = target;
            DisplayPicker.SelectedItem = target;
        }
        finally { updating = false; }
    }

    private static bool SameIdentity(WindowTarget a, WindowTarget b) => a.Handle == b.Handle && a.Generation == b.Generation
        && a.ProcessId == b.ProcessId && a.ThreadId == b.ThreadId && a.Started == b.Started && a.ClassName == b.ClassName;

    private void SetCandidates(IReadOnlyList<WindowTarget> candidates, nint foreground = 0, bool preserve = false, IReadOnlyList<WindowTarget>? chosen = null)
    {
        var previous = chosen?.ToArray() ?? (preserve ? selected.Select(r => r.Target).ToArray() : []);
        updating = true;
        try
        {
            selected.Clear(); rows.Clear();
            var displays = WindowManager.ListDisplays();
            foreach (var candidate in candidates)
            {
                var source = WindowManager.DisplayFor(candidate.Handle);
                string name = displays.FirstOrDefault(d => d.Handle == source.Handle)?.Name ?? "显示器";
                rows.Add(new WindowRow(candidate, SelectionChanged) { Subtitle = candidate.Application + " · " + name });
            }
            foreach (var target in previous)
            {
                var row = rows.FirstOrDefault(r => SameIdentity(r.Target, target));
                if (row != null) { row.Selected = true; selected.Add(row); }
            }
            if (chosen == null && !preserve && rows.FirstOrDefault(r => r.Target.Handle == foreground) is { } initial)
            { initial.Selected = true; selected.Add(initial); }
        }
        finally { updating = false; UpdatePreview(); }
    }

    public void OpenForForeground(nint foreground, bool toggle = true)
    {
        if (selectionWindow != null) { selectionWindow.Activate(); return; }
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        var own = new WindowInteropHelper(this).Handle;
        if (toggle && foreground == own && WindowState != System.Windows.WindowState.Minimized)
        { CollapseToTray(); return; }
        if (!IsVisible) Show();
        if (foreground != own && !busy && !manager.HasPending)
        {
            try
            {
                var candidates = manager.ListWindows();
                LoadDisplays(WindowManager.DisplayFor(candidates.Any(t => t.Handle == foreground) ? foreground : 0).Handle);
                SearchBox.Clear();
                SetCandidates(candidates, foreground);
                PlaceOnDisplay();
                StatusText.Text = "已选当前窗口。继续选择窗口，确认目标屏幕后按 Enter。";
            }
            catch (Exception e) { StatusText.Text = e.Message; }
        }
        WindowState = System.Windows.WindowState.Normal;
        Activate(); Native.SetForegroundWindow(own);
        SearchBox.Focus();
    }

    private void PlaceOnDisplay()
    {
        nint handle = new WindowInteropHelper(this).Handle;
        double scale = Native.GetDpiForWindow(handle) / 96d;
        int width = (int)(Width * scale), height = (int)(Height * scale);
        var work = display.WorkArea;
        Native.SetWindowPos(handle, 0, work.X + Math.Max(0, (work.Width - width) / 2),
            work.Y + Math.Max(0, (work.Height - height) / 2), 0, 0, 0x15);
    }

    private void SelectionChanged(WindowRow row)
    {
        if (updating) return;
        if (row.Selected) selected.Add(row); else selected.Remove(row);
        UpdatePreview();
    }

    private void UpdateControls()
    {
        if (closed) return;
        bool available = !busy && !manager.HasPending && !Picking && settingsWindow == null;
        SettingsButton.IsEnabled = available;
        ArrangeScreenButton.IsEnabled = QuickScreenButton.IsEnabled = available;
        RecentButton.IsEnabled = available && recentGroup != null;
        ApplyButton.IsEnabled = available && selected.Count >= 2 && layoutUsable;
        UndoButton.IsEnabled = available && manager.HasUndo;
        RefreshButton.IsEnabled = DisplayPicker.IsEnabled = LayoutPicker.IsEnabled = available;
        SwapButton.IsEnabled = available && selected.Count >= 2;
        SwapButton.Content = selected.Count > 2 ? "轮换顺序" : "交换左右";
        AutomationProperties.SetName(SwapButton, selected.Count > 2 ? "轮换顺序" : "交换左右");
        WindowList.IsEnabled = available;
        ChosenCount.Text = $"已选 {selected.Count} 扇";
    }

    private void UpdatePreview()
    {
        foreach (var row in rows) row.Order = selected.IndexOf(row) + 1;
        PreviewCanvas.Width = display.WorkArea.Width;
        PreviewCanvas.Height = display.WorkArea.Height;
        PreviewCanvas.Children.Clear();
        layoutUsable = false;
        try
        {
            if (DisplayPicker.SelectedItem is not DisplayArea) return;
            var layout = LayoutEngine.Arrange(display.WorkArea, Math.Max(2, selected.Count), Kind);
            layoutUsable = layout.All(r => r.Width >= 240 && r.Height >= 160);
            for (int i = 0; i < layout.Length; i++)
            {
                var rect = layout[i];
                var cell = new Border
                {
                    Width = rect.Width, Height = rect.Height, CornerRadius = new CornerRadius(18),
                    Background = i < selected.Count ? UiPalette.PreviewPane : UiPalette.Panel,
                    Child = new TextBlock { Text = (i + 1).ToString(), FontSize = Math.Min(rect.Width, rect.Height) * .35,
                        Foreground = UiPalette.IsDark ? UiPalette.HeaderInk : UiPalette.Primary, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                Canvas.SetLeft(cell, rect.X - display.WorkArea.X); Canvas.SetTop(cell, rect.Y - display.WorkArea.Y);
                PreviewCanvas.Children.Add(cell);
            }
            if (!layoutUsable) StatusText.Text = "窗口尺寸过小，请减少选择或改用网格。";
        }
        catch (ArgumentOutOfRangeException e) { StatusText.Text = e.Message; }
        finally { UpdateSelectionSummary(); }
    }

    private void UpdateSelectionSummary()
    {
        SelectionLabel.Text = string.Join("  ·  ", selected.Select((r, i) => $"{i + 1} · {(r.Target.Title.Length > 20 ? r.Target.Title[..20] : r.Target.Title)}"));
        SelectionLabel.ToolTip = string.Join("\n", selected.Select((r, i) => $"{i + 1} · {r.Target.Title}"));
        EmptyLabel.Visibility = view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        EmptyLabel.Text = rows.Count == 0 ? "没有可选窗口，请打开普通窗口后刷新。" : "没有匹配的窗口，试试其他标题或应用名称。";
        UpdateControls();
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (view == null) return;
        view.Refresh(); UpdatePreview();
    }
    private void LayoutChanged(object sender, SelectionChangedEventArgs e) { if (view != null && !updating) UpdatePreview(); }
    private void DisplayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (view == null || updating) return;
        if (DisplayPicker.SelectedItem is DisplayArea target) display = target;
        UpdatePreview();
    }
    private void SwapClick(object sender, RoutedEventArgs e)
    {
        if (selected.Count < 2) return;
        var first = selected[0]; selected.RemoveAt(0); selected.Add(first); UpdatePreview();
    }
    private void RecentClick(object sender, RoutedEventArgs e)
    {
        if (closed || busy || manager.HasPending || Picking || recentGroup == null) return;
        try
        {
            var group = recentGroup;
            var candidates = manager.ListWindows();
            LoadDisplays(group.Destination.Handle, allowFallback: false);
            SearchBox.Clear();
            LayoutPicker.SelectedItem = LayoutPicker.Items.Cast<ComboBoxItem>().First(item => Equals(item.Tag, group.Layout));
            SetCandidates(candidates, chosen: group.Targets);
            var missing = group.Targets.Where(t => !selected.Any(row => SameIdentity(row.Target, t))).ToArray();
            StatusText.Text = "已载入最近组，请检查成员、顺序和目标后排布。"
                + (missing.Length > 0 ? " 不可用：" + string.Join("、", missing.Select(t => t.Title)) + "。" : "")
                + (selected.Count < 2 ? " 请补选至少两扇窗口。" : "")
                + (DisplayPicker.SelectedItem == null ? " 原目标屏幕不可用，请重新选择。" : "");
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private void RefreshClick(object sender, RoutedEventArgs e)
    {
        try { LoadDisplays((DisplayPicker.SelectedItem as DisplayArea)?.Handle ?? 0, allowFallback: false); SetCandidates(manager.ListWindows(), preserve: true); StatusText.Text = DisplayPicker.SelectedItem == null ? "已刷新，请明确选择目标屏幕。" : "已刷新，仍可用的选择与顺序已保留。"; }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }
    private async void ApplyClick(object sender, RoutedEventArgs e)
    {
        if (busy || manager.HasPending || Picking || selected.Count < 2 || !layoutUsable || DisplayPicker.SelectedItem is not DisplayArea destination) return;
        await ApplyGroupAsync(new(selected.Select(r => r.Target).ToArray(), destination, Kind), false);
    }

    private async Task ApplyGroupAsync(WindowGroup request, bool currentScreenOnly)
    {
        if (closed || busy || manager.HasPending || Picking || settingsWindow != null) return;
        if (request.Targets.Length < 2)
        {
            StatusText.Text = "本屏至少需要两扇合格窗口。";
            Native.ShowWindow(new WindowInteropHelper(this).Handle, 4); return;
        }
        requestedGroup = request;
        busy = true; arranging = true; UpdateControls();
        try { ShowReport(await (currentScreenOnly ? manager.ApplyOnScreenAsync(request.Targets, request.Destination, request.Layout)
            : manager.ApplyAsync(request.Targets, request.Destination, request.Layout))); }
        catch (Exception exception) { requestedGroup = null; StatusText.Text = exception.Message; }
        finally { busy = false; UpdateControls(); }
    }

    internal async Task ArrangeCurrentScreenAsync(nint foreground, IReadOnlyList<WindowTarget>? candidates = null)
    {
        if (closed || busy || manager.HasPending || settingsWindow != null || Picking) return;
        try
        {
            candidates ??= manager.ListWindows();
            var destination = WindowManager.DisplayFor(candidates.Any(t => t.Handle == foreground) ? foreground : 0);
            var targets = candidates.Where(t => manager.TryReadOnScreen(t, destination, out _))
                .Select((t, index) => (Target: t, Index: index, Bounds: manager.ReadState(t).Bounds))
                .OrderBy(t => t.Target.Handle == foreground ? 0 : 1).ThenBy(t => t.Bounds.Y).ThenBy(t => t.Bounds.X).ThenBy(t => t.Index)
                .Select(t => t.Target).ToArray();
            await ApplyGroupAsync(new(targets, destination, ArrangementKind.Automatic), true);
        }
        catch (Exception e) { StatusText.Text = e.Message; if (!closed) Native.ShowWindow(new WindowInteropHelper(this).Handle, 4); }
    }
    private async void ArrangeScreenClick(object sender, RoutedEventArgs e) => await ArrangeCurrentScreenAsync(WindowManager.Foreground);
    internal QuickSelectWindow? OpenQuickSelection(nint foreground, IReadOnlyList<WindowTarget>? candidates = null)
    {
        if (closed || busy || manager.HasPending || settingsWindow != null) return null;
        selectionWindow?.Close();
        try
        {
            candidates ??= manager.ListWindows();
            var destination = WindowManager.DisplayFor(candidates.Any(t => t.Handle == foreground) ? foreground : 0);
            var scoped = candidates.Where(t => manager.TryReadOnScreen(t, destination, out _)).ToArray();
            var picker = new QuickSelectWindow(this, manager, destination, scoped, foreground, quickLayout);
            selectionWindow = picker; UpdateControls();
            CollapseToTray();
            picker.Completed += result =>
            {
                if (selectionWindow == picker) selectionWindow = null;
                quickLayout = result.Layout;
                if (closed) return;
                UpdateControls();
                if (result.Confirmed) _ = ApplyGroupAsync(new(result.Targets, destination, result.Layout), true);
                else
                {
                    StatusText.Text = result.Message;
                    if (result.Failed) Native.ShowWindow(new WindowInteropHelper(this).Handle, 4);
                }
            };
            picker.Show();
            if (picker.IsVisible)
            {
                nint handle = new WindowInteropHelper(picker).Handle;
                picker.Activate();
                if (Native.GetForegroundWindow() != handle) Native.SetForegroundWindow(handle);
            }
            return picker;
        }
        catch (Exception e)
        {
            selectionWindow?.Close(); selectionWindow = null; StatusText.Text = e.Message; UpdateControls();
            if (!closed) Native.ShowWindow(new WindowInteropHelper(this).Handle, 4);
            return null;
        }
    }
    private void QuickScreenClick(object sender, RoutedEventArgs e) => OpenQuickSelection(WindowManager.Foreground);

    private async void UndoClick(object sender, RoutedEventArgs e)
    {
        if (busy || manager.HasPending || Picking || !manager.HasUndo) return;
        busy = true; arranging = false; requestedGroup = null; UpdateControls();
        try { ShowReport(await manager.UndoAsync()); }
        catch (Exception exception) { StatusText.Text = exception.Message; WindowState = System.Windows.WindowState.Normal; }
        finally { busy = false; UpdateControls(); }
    }
    private void ShowReport(OperationReport report)
    {
        if (closed) return;
        if (report.Pending)
        {
            if (dragPlacing) ShowDragNote("正在等待目标窗口响应，完成前不能再次投放。");
            else StatusText.Text = "正在等待目标窗口响应，完成前暂停排布和恢复。";
            return;
        }
        if (report.Success && requestedGroup != null) recentGroup = requestedGroup;
        requestedGroup = null;
        StatusText.Text = string.Join("\n", report.Windows.Select(w => w.Target.Title + "：" + w.Message))
            + (report.Notice.Length > 0 ? "\n" + report.Notice : "");
        if (dragPlacing)
        {
            dragPlacing = false;
            if (report.Success && report.Notice.Length == 0) ShowDragFeedback(dragZone);
            else if (!report.Success || report.Notice.Length > 0)
                ShowDragNote(string.Join(" ", report.Windows.Select(w => w.Message)) + (report.Notice.Length > 0 ? " " + report.Notice : ""));
        }
        else if (report.Success && arranging)
        {
            StatusText.Text = "已排布。从托盘图标或快捷键打开 PaneSet，使用恢复键或按钮恢复。" + report.Notice;
            CollapseToTray();
        }
        else if (!report.Success) { WindowState = System.Windows.WindowState.Normal; Activate(); }
    }
    private void UpdateShortcutHint() => ShortcutText.Text = (shortcuts?.Hint ?? "快捷键未启用") + $" · 按住 {ModifierLabel()} 拖动窗口投放 · Enter 排布 · Alt+S 轮换";
    private string ModifierLabel()
    {
        uint mask = shortcuts?.DragModifiers ?? ShortcutSettings.DefaultDragModifiers;
        return string.Join("+", new[] { (mask & 1) != 0 ? "Alt" : null, (mask & 2) != 0 ? "Ctrl" : null,
            (mask & 4) != 0 ? "Shift" : null, (mask & 8) != 0 ? "Win" : null }.Where(s => s != null));
    }
    private void ApplyTheme()
    {
        bool dark = ThemeSupport.ResolveDark(themeMode);
        if (UiPalette.IsDark != dark)
        {
            UiPalette.Apply(dark);
            UpdatePreview();
        }
    }
    private string ChangeTheme(AppearanceMode mode)
    {
        if (!Enum.IsDefined(mode)) return "无效的外观选项。";
        try
        {
            if (shortcuts != null)
            {
                string error = shortcuts.ChangeTheme(mode);
                if (error.Length > 0) return error;
            }
            else
            {
                var settings = ShortcutSettings.Load(settingsPath, out _);
                settings.Theme = mode;
                settings.Save(settingsPath);
            }
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { return "外观未保存：" + e.Message; }
        themeMode = mode;
        ApplyTheme();
        return "";
    }
    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        if (closed || busy || manager.HasPending || settingsWindow != null || Picking) return;
        settingsWindow = new SettingsWindow(this, shortcuts, themeMode, ChangeTheme);
        UpdateControls();
        try { settingsWindow.ShowDialog(); }
        finally { settingsWindow = null; UpdateShortcutHint(); UpdateControls(); }
    }
    private void CloseClick(object sender, RoutedEventArgs e) => CollapseToTray();
    private void ExitClick(object sender, RoutedEventArgs e) => Exit();
    public void Exit() { exitRequested = true; Close(); }

    private void CreateTray()
    {
        if (closed || tray != null || !trayEnabled) return;
        try
        {
            tray = new TrayIcon(new WindowInteropHelper(this).Handle, OpenFromTray, ShowTrayMenu);
            if (tray.Created)
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(InterceptMinimizeCommand);
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception) { tray = null; }
    }
    // Caption clicks and the system menu arrive as WM_SYSCOMMAND SC_MINIMIZE; collapsing before
    // the system minimizes avoids a visible restore flicker.
    private nint InterceptMinimizeCommand(nint window, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == 0x112 && (wparam & 0xFFF0) == 0xF020 && tray?.Created == true)
        { handled = true; Hide(); }
        return 0;
    }
    /// Collapsing hides the window completely when the tray icon exists; without one the taskbar
    /// entry stays so a usable entry remains.
    internal void CollapseToTray()
    {
        if (tray?.Created == true) { dragFeedback?.Dispose(); Hide(); }
        else WindowState = System.Windows.WindowState.Minimized;
    }
    private void OpenFromTray()
    {
        if (closed) return;
        if (IsVisible && WindowState != System.Windows.WindowState.Minimized && IsActive) { CollapseToTray(); return; }
        OpenForForeground(WindowManager.Foreground, toggle: false);
    }
    private void ShowTrayMenu()
    {
        if (closed) return;
        Native.SetForegroundWindow(new WindowInteropHelper(this).Handle);
        var open = new MenuItem { Header = "打开面板" };
        open.Click += (_, _) => OpenFromTray();
        var settings = new MenuItem { Header = "设置" };
        settings.Click += (_, _) => { if (!IsVisible) OpenFromTray(); SettingsClick(this, new()); };
        var exit = new MenuItem { Header = "退出" };
        exit.Click += (_, _) => Exit();
        var menu = new ContextMenu { PlacementTarget = this, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint, StaysOpen = false };
        menu.Items.Add(open); menu.Items.Add(settings); menu.Items.Add(exit);
        menu.IsOpen = true;
    }
}

public sealed class WindowRow(WindowTarget target, Action<WindowRow> changed) : INotifyPropertyChanged
{
    public WindowTarget Target { get; private set; } = target;
    public string Subtitle { get; init; } = target.Application;
    public void RefreshTarget(WindowTarget value)
    {
        Target = value;
        PropertyChanged?.Invoke(this, new(nameof(Target)));
        PropertyChanged?.Invoke(this, new(nameof(DisplayTitle)));
    }
    public string DisplayTitle => Target.Title.Length > 64 ? Target.Title[..64] : Target.Title;
    private bool selected;
    private int order;
    public bool Selected
    {
        get => selected;
        set { if (selected == value) return; selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); changed(this); }
    }
    public int Order
    {
        get => order;
        set { if (order == value) return; order = value; PropertyChanged?.Invoke(this, new(nameof(OrderText))); }
    }
    public string OrderText => order > 0 ? order.ToString() : "·";
    public event PropertyChangedEventHandler? PropertyChanged;
}
