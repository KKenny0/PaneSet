using System.IO;
using System.Windows;
using System.Windows.Controls;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static readonly string TestSettingsPath = Path.Combine(Path.GetTempPath(), "PaneSet.Checks-" + Guid.NewGuid().ToString("N"), "settings.json");

    private static async Task CheckSettingsUi()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = manager.ListWindows().Where(t => fixture.Handles.Contains(t.Handle)).ToArray();
        var window = new MainWindow(manager, display, targets, settingsPath: TestSettingsPath);
        window.Show(); await Task.Delay(120);
        try
        {
            Equal(true, ButtonNamed(window, "设置").IsEnabled);
            Invoke(ButtonNamed(window, "设置"));
            await Until(() => Application.Current.Windows.Cast<Window>().Any(w => w is SettingsWindow));
            var settings = Application.Current.Windows.Cast<Window>().OfType<SettingsWindow>().Single();
            try
            {
                Equal(true, Elements<TextBlock>(settings).Any(t => t.Text.Contains("面板内固定键")));
                foreach (var size in new[] { (700d, 700d), (600d, 450d) })
                { settings.Width = size.Item1; settings.Height = size.Item2; await Task.Delay(60); settings.UpdateLayout(); Screenshot(settings, "paneset-settings-" + (int)size.Item2); }
                var scroll = Elements<ScrollViewer>(settings).First(); scroll.ScrollToBottom();
                await Until(() => scroll.VerticalOffset >= scroll.ScrollableHeight);
                settings.UpdateLayout();
                var bottom = ButtonNamed(settings, "SelectScreen 恢复默认");
                var point = bottom.TranslatePoint(new Point(0, 0), settings);
                Equal(true, point.Y >= 0 && point.Y + bottom.ActualHeight <= ClientSize(settings).Height);
                Screenshot(settings, "paneset-settings-450-bottom");
            }
            finally { settings.Close(); }
        }
        finally { window.Exit(); }
    }

    private static async Task CheckScreenGuard()
    {
        var displays = WindowManager.ListDisplays();
        if (displays.Count < 2) throw new UnavailableException("Needs two connected displays.");
        await using var fixture = await Fixture.Start();
        await fixture.Send($"position 1 {displays[1].WorkArea.X + 150} {displays[1].WorkArea.Y + 100}");
        using var manager = new WindowManager();
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var originals = targets.Select(manager.ReadState).ToArray();
        Equal(true, (await manager.ApplyOnScreenAsync(targets.Take(2).ToArray(), displays[0])).NeedsRefresh);
        Equal(false, manager.HasUndo);
        for (int i = 0; i < targets.Length; i++) Equal(originals[i], manager.ReadState(targets[i]));
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 40)]
    private struct MouseInput
    {
        [System.Runtime.InteropServices.FieldOffset(0)] public uint Type;
        [System.Runtime.InteropServices.FieldOffset(20)] public uint Flags;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, MouseInput[] input, int size);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint handle);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetAncestor(nint handle, uint flag);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetThreadDpiAwarenessContext();
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(nint context);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint WindowFromPoint(Native.Point point);
    private static async Task ClickOwned(Window window, FrameworkElement control)
    {
        window.UpdateLayout();
        await Until(() => control.IsMeasureValid && control.IsArrangeValid && control.ActualWidth > 0 && control.ActualHeight > 0 && control.IsVisible);
        var point = control.PointToScreen(new Point(control.ActualWidth / 2, control.ActualHeight / 2));
        var native = new Native.Point { X = (int)point.X, Y = (int)point.Y };
        await ClickNativeOwned(new System.Windows.Interop.WindowInteropHelper(window).Handle, native);
    }
    private static async Task ClickNativeOwned(nint handle, Native.Point native)
    {
        nint actual = WindowFromPoint(native);
        if (actual != handle)
        {
            Native.GetWindowRect(handle, out var rect); Native.GetWindowThreadProcessId(actual, out uint actualPid);
            throw new Exception($"Mouse target is not the fixture-owned window; injection refused. expected={handle}, actual={actual}, actualPid={actualPid}, actualClass={Native.Text(actual, true)}, actualRoot={GetAncestor(actual, 2)}, foreground={GetForegroundWindow()}, point={native.X},{native.Y}, bounds={rect.Value}, visible={Native.IsWindowVisible(handle)}, minimized={Native.IsIconic(handle)}, enabled={IsWindowEnabled(handle)}, dpi={Native.GetDpiForWindow(handle)}, awareness={GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext())}");
        }
        if ((GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0)
            throw new Exception("Mouse input precondition failed: a physical button is already pressed; injection refused.");
        bool swapped = GetSystemMetrics(23) != 0;
        uint down = swapped ? 8u : 2u, up = swapped ? 16u : 4u;
        Native.GetCursorPos(out var original);
        try
        {
            Equal(true, SetCursorPos(native.X, native.Y));
            var inputs = new[] { new MouseInput { Flags = down }, new MouseInput { Flags = up } };
            uint inserted = SendInput(2, inputs, 40); int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (inserted == 1) SendInput(1, new[] { new MouseInput { Flags = up } }, 40);
            if (inserted != 2) throw new Exception($"Mouse input inserted {inserted}/2, error={error}, swapped={swapped}");
            await Task.Delay(80);
        }
        finally { SetCursorPos(original.X, original.Y); }
    }

    private static async Task OverlayClick(QuickSelectWindow picker)
    {
        bool down = false, up = false;
        picker.AddHandler(System.Windows.Input.Mouse.PreviewMouseDownEvent, new System.Windows.Input.MouseButtonEventHandler((_, e) => down |= e.ChangedButton == System.Windows.Input.MouseButton.Left), true);
        picker.AddHandler(System.Windows.Input.Mouse.PreviewMouseUpEvent, new System.Windows.Input.MouseButtonEventHandler((_, e) => up |= e.ChangedButton == System.Windows.Input.MouseButton.Left), true);
        await ClickOwned(picker, Elements<TextBlock>(picker).Single(t => t.Text == "选择要一起看的窗口"));
        await Until(() => down && up);
    }
    private static async Task PreparePickerHost(MainWindow panel)
    {
        string before = WindowEvidence(panel);
        panel.WindowState = System.Windows.WindowState.Normal;
        bool topmost = panel.Topmost; panel.Topmost = true;
        try
        {
            await ClickOwned(panel, Elements<TextBlock>(panel).Single(t => t.Text == "PaneSet"));
            await Until(() => GetForegroundWindow() == new System.Windows.Interop.WindowInteropHelper(panel).Handle);
        }
        catch (Exception e)
        {
            throw new Exception($"Picker host preparation failed. Before: {before}\nAfter: {WindowEvidence(panel)}", e);
        }
        finally { panel.Topmost = topmost; }
    }
    private static async Task<QuickSelectWindow> OpenPickerForFixture(MainWindow panel, nint foreground, WindowTarget[] targets)
    {
        await PreparePickerHost(panel);
        var picker = panel.OpenQuickSelection(foreground, targets)!;
        await Ready(picker); return picker;
    }
    private static async Task Ready(QuickSelectWindow picker)
    {
        await Until(() => picker.IsReady || !picker.IsVisible);
        if (!picker.IsReady)
            throw new Exception("Quick picker did not become ready: " + string.Join(" | ", Application.Current.Windows.Cast<Window>().OfType<MainWindow>()
                .Select(w => ((TextBlock)w.FindName("StatusText")).Text)));
    }
    private static async Task ShowCandidates(QuickSelectWindow picker)
    {
        await Until(() => Elements<CheckBox>(picker).Any(c => c.IsVisible));
    }
    private static CheckBox Candidate(QuickSelectWindow picker, string title) => Elements<CheckBox>(picker)
        .Single(c => System.Windows.Automation.AutomationProperties.GetName(c) == title);

    private static async Task CheckQuickFlow()
    {
        await using var fixture = await Fixture.Start(4);
        var displays = WindowManager.ListDisplays(); var destination = displays[0];
        if (displays.Count > 1) await fixture.Send($"position 3 {displays[1].WorkArea.X + 120} {displays[1].WorkArea.Y + 100}");
        await fixture.Send("max 0");
        using var manager = new WindowManager();
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var original = targets.Select(manager.ReadState).ToArray();
        var panel = new MainWindow(manager, destination, targets, enableShortcuts: false); panel.Show();
        try
        {
            var picker = await OpenPickerForFixture(panel, targets[0].Handle, targets);
            await OverlayClick(picker);
            Equal(targets[0].Title, manager.ListWindows(destination).Single(t => t.Handle == targets[0].Handle).Title);
            await ShowCandidates(picker);
            await Until(() => Elements<CheckBox>(picker).Count(c => c.IsChecked == true) == 1);
            Equal(1, Elements<CheckBox>(picker).Count(c => c.IsChecked == true));
            Toggle(Candidate(picker, targets[1].Title));
            Equal(true, RoutedKey(picker, Candidate(picker, targets[1].Title), System.Windows.Input.Key.D3, System.Windows.Input.ModifierKeys.Alt, system: true));
            foreach (int i in Enumerable.Range(0, 4)) Equal(original[i], manager.ReadState(targets[i]));
            Screenshot(picker, "paneset-native-selection");
            Equal(true, RoutedKey(picker, Candidate(picker, targets[1].Title), System.Windows.Input.Key.Escape, System.Windows.Input.ModifierKeys.None));
            Equal(false, picker.IsVisible); Equal(false, manager.HasUndo);
            picker = await OpenPickerForFixture(panel, targets[0].Handle, targets);
            Equal(ArrangementKind.Grid, picker.Kind);
            await ShowCandidates(picker);
            await Until(() => Elements<CheckBox>(picker).Count(c => c.IsChecked == true) == 1);
            Equal(1, Elements<CheckBox>(picker).Count(c => c.IsChecked == true));
            Toggle(Candidate(picker, targets[1].Title));
            Invoke(ButtonNamed(picker, "确认本屏选窗")); await Until(() => !picker.IsVisible && !manager.HasPending && manager.HasUndo);
            Equal(false, manager.ReadState(targets[0]).Maximized);
            Equal(original[2], manager.ReadState(targets[2])); Equal(original[3], manager.ReadState(targets[3]));
            var applied = targets.Select(manager.ReadState).ToArray();
            picker = await OpenPickerForFixture(panel, targets[0].Handle, targets);
            await ShowCandidates(picker); Toggle(Candidate(picker, targets[2].Title));
            Equal(true, RoutedKey(picker, Candidate(picker, targets[2].Title), System.Windows.Input.Key.D2, System.Windows.Input.ModifierKeys.Alt, system: true));
            Invoke(ButtonNamed(picker, "取消本屏选窗")); await Until(() => !picker.IsVisible);
            for (int i = 0; i < 4; i++) Equal(applied[i], manager.ReadState(targets[i]));
            Equal(true, manager.HasUndo); Equal(true, (await manager.UndoAsync()).Success);
            for (int i = 0; i < 4; i++) Equal(original[i], manager.ReadState(targets[i]));
        }
        finally { panel.Exit(); }
    }

    private static async Task CheckQuickInvalid()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager(); var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
        var panel = new MainWindow(manager, display, targets, enableShortcuts: false); panel.Show();
        try
        {
            var picker = await OpenPickerForFixture(panel, targets[0].Handle, targets);
            await ShowCandidates(picker);
            var combo = Elements<ComboBox>(picker).Single(); combo.IsDropDownOpen = true;
            await Task.Delay(250); Equal(true, picker.IsReady); combo.IsDropDownOpen = false;
            Equal(false, ButtonNamed(picker, "确认本屏选窗").IsEnabled);
            Toggle(Candidate(picker, targets[0].Title)); Equal(false, ButtonNamed(picker, "确认本屏选窗").IsEnabled);
            Toggle(Candidate(picker, targets[0].Title)); Toggle(Candidate(picker, targets[1].Title));
            await fixture.Send("min 1");
            await Until(() => Elements<TextBlock>(picker).Any(t => t.Text.Contains("已失效")));
            Equal(true, Candidate(picker, targets[1].Title).IsChecked); Equal(false, ButtonNamed(picker, "确认本屏选窗").IsEnabled);
            Screenshot(picker, "paneset-native-selection-invalid");
            Toggle(Candidate(picker, targets[1].Title)); Toggle(Candidate(picker, targets[2].Title));
            Equal(true, ButtonNamed(picker, "确认本屏选窗").IsEnabled);
            await fixture.Send("long");
            await Until(() => Elements<TextBlock>(picker).Any(t => t.Text.Length > 200 && t.ActualHeight > 40));
            Screenshot(picker, "paneset-native-selection-long-title");
            Equal(true, SetForegroundWindow(fixture.Handles[0])); await Until(() => !picker.IsVisible);
            Equal(fixture.Handles[0], GetForegroundWindow());
            Equal(false, manager.HasUndo);
        }
        finally { panel.Exit(); }
    }

    private static async Task CheckQuickInput()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager(); var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
        var originals = targets.Select(manager.ReadState).ToArray();
        var panel = new MainWindow(manager, display, targets, enableShortcuts: false); panel.Show();
        try
        {
            var picker = await OpenPickerForFixture(panel, targets[0].Handle, targets);
            await ShowCandidates(picker); var second = Candidate(picker, targets[1].Title);
            second.Focus(); await Until(() => System.Windows.Input.Keyboard.FocusedElement == second);
            await SendKeys(0x20); await Until(() => second.IsChecked == true);
            await SelectColumnsByInput(picker);
            for (int i = 0; i < 3; i++) Equal(originals[i], manager.ReadState(targets[i]));
            var combo = Elements<ComboBox>(picker).Single(); combo.Focus(); combo.IsDropDownOpen = true;
            await Until(() => combo.IsDropDownOpen);
            await SendKeys(0x0D); await Until(() => !combo.IsDropDownOpen);
            Equal(true, picker.IsVisible); Equal(false, manager.HasUndo);
            for (int i = 0; i < 3; i++) Equal(originals[i], manager.ReadState(targets[i]));
            await SendKeys(0x0D); await Until(() => !picker.IsVisible && !manager.HasPending && manager.HasUndo);
            Equal(true, (await manager.UndoAsync()).Success);
            for (int i = 0; i < 3; i++) Equal(originals[i], manager.ReadState(targets[i]));
            picker = await OpenPickerForFixture(panel, targets[0].Handle, targets);
            await SendKeys(0x1B); await Until(() => !picker.IsVisible); Equal(false, manager.HasUndo);
        }
        finally { panel.Exit(); }
    }

    private static async Task CheckGlobalQuickInput()
    {
        string path = Path.Combine(Path.GetTempPath(), "PaneSet.B-" + Guid.NewGuid().ToString("N"), "settings.json");
        new ShortcutSettings { Shortcuts = new() {
            [ShortcutAction.OpenPanel] = null, [ShortcutAction.Restore] = null,
            [ShortcutAction.ArrangeScreen] = new(3, 0x80), [ShortcutAction.SelectScreen] = new(3, 0x81) } }.Save(path);
        await using var fixture = await Fixture.Start();
        var display = WindowManager.ListDisplays().Last();
        for (int i = 0; i < 3; i++) await fixture.Send($"position {i} {display.WorkArea.X + 100 + i * 100} {display.WorkArea.Y + 100 + i * 50}");
        await fixture.Send("max 0");
        using var manager = new WindowManager(); var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var originals = targets.Select(manager.ReadState).ToArray();
        var panel = new MainWindow(manager, WindowManager.ListDisplays()[0], targets, settingsPath: path); panel.Show();
        var trace = new List<string>();
        void Observe(string stage)
        {
            var foreground = GetForegroundWindow();
            trace.Add($"{stage}: target={targets[0].Handle}, foreground={foreground}, foregroundClass={Native.Text(foreground, true)}, state={manager.ReadState(targets[0])}, pending={manager.HasPending}, undo={manager.HasUndo}; panel={WindowEvidence(panel)}");
        }
        try
        {
            Observe("before-initial-top");
            await fixture.Send("top 0");
            var bounds = manager.ReadState(targets[0]).Bounds;
            await ClickNativeOwned(targets[0].Handle, new Native.Point { X = bounds.X + 100, Y = bounds.Y + 12 });
            Observe("after-initial-click");
            await Until(() => GetForegroundWindow() == fixture.Handles[0]);
            await SendKeys(0x11, 0x12, 0x81);
            await Until(() => Application.Current.Windows.Cast<Window>().OfType<QuickSelectWindow>().Any());
            var picker = Application.Current.Windows.Cast<Window>().OfType<QuickSelectWindow>().Single(); await Ready(picker);
            CheckPickerBounds(picker, display);
            await OverlayClick(picker);
            Equal(targets[0].Title, manager.ListWindows(display).Single(t => t.Handle == targets[0].Handle).Title);
            await ShowCandidates(picker);
            Equal(true, Candidate(picker, targets[0].Title).IsChecked);
            var second = Candidate(picker, targets[1].Title); second.Focus();
            await Until(() => System.Windows.Input.Keyboard.FocusedElement == second);
            await SendKeys(0x20); await Until(() => second.IsChecked == true);
            await SelectColumnsByInput(picker);
            await SendKeys(0x0D); await Until(() => !picker.IsVisible && !manager.HasPending && manager.HasUndo);
            Equal(originals[2], manager.ReadState(targets[2]));
            var applied = targets.Select(manager.ReadState).ToArray();
            Observe("after-first-arrange");
            await fixture.Send("untop 0");
            Observe("after-untop");
            if (GetForegroundWindow() != targets[0].Handle) Equal(true, SetForegroundWindow(targets[0].Handle));
            Observe("after-foreground-request");
            await Until(() => GetForegroundWindow() == targets[0].Handle);
            Observe("before-second-entry");
            await SendKeys(0x11, 0x12, 0x81);
            await Until(() => Application.Current.Windows.Cast<Window>().OfType<QuickSelectWindow>().Any());
            picker = Application.Current.Windows.Cast<Window>().OfType<QuickSelectWindow>().Single(); await Ready(picker);
            CheckPickerBounds(picker, display);
            await SendKeys(0x1B); await Until(() => !picker.IsVisible); Equal(true, manager.HasUndo);
            for (int i = 0; i < 3; i++) Equal(applied[i], manager.ReadState(targets[i]));
            Equal(true, (await manager.UndoAsync()).Success);
            for (int i = 0; i < 3; i++) Equal(originals[i], manager.ReadState(targets[i]));
        }
        catch (Exception e)
        {
            Observe("failure");
            throw new Exception("Global B input failed. Stage observations:\n" + string.Join("\n", trace), e);
        }
        finally { panel.Exit(); Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    private static async Task CheckQuickActivationFailure()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager(); var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
        Equal(true, (await manager.ApplyOnScreenAsync(targets.Take(2).ToArray(), display)).Success);
        var applied = targets.Select(manager.ReadState).ToArray();
        var panel = new MainWindow(manager, display, targets, enableShortcuts: false); panel.Show();
        try
        {
            await fixture.Send("top 0");
            var bounds = manager.ReadState(targets[0]).Bounds;
            await ClickNativeOwned(targets[0].Handle, new Native.Point { X = bounds.X + 100, Y = bounds.Y + 12 });
            await Until(() => GetForegroundWindow() == targets[0].Handle);
            var picker = new QuickSelectWindow(panel, manager, display, targets, targets[0].Handle, ArrangementKind.Automatic) { ShowActivated = false };
            QuickSelectionResult? result = null; picker.Completed += value => result = value;
            picker.Show(); await Until(() => result != null);
            Equal(true, result!.Failed); Equal(false, result.Confirmed); Equal(true, result.Message.Contains("未取得"));
            Equal(false, picker.IsVisible); Equal(true, manager.HasUndo);
            for (int i = 0; i < 3; i++) Equal(applied[i], manager.ReadState(targets[i]));
            Equal(true, (await manager.UndoAsync()).Success);
        }
        finally { panel.Exit(); }
    }

    private static async Task CheckMouseRouting()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager();
        var target = manager.ListWindows().Single(t => t.Handle == fixture.Handles[0]);
        await fixture.Send("top 0");
        var bounds = manager.ReadState(target).Bounds;
        await ClickNativeOwned(target.Handle, new Native.Point { X = bounds.X + 100, Y = bounds.Y + 12 });
        await Until(() => GetForegroundWindow() == target.Handle);
        await ClickNativeOwned(target.Handle, new Native.Point { X = bounds.X + 100, Y = bounds.Y + 100 });
        await Until(() => manager.ListWindows().Single(t => t.Handle == target.Handle).Title == "PaneSet fixture received a mouse click");
    }
    private static async Task CheckQuickEntry()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = manager.ListWindows(display).Where(t => fixture.Handles.Contains(t.Handle)).ToArray();
        var panel = new MainWindow(manager, display, targets, enableShortcuts: false); panel.Show();
        try
        {
            Equal(true, ButtonNamed(panel, "本屏快速选窗").IsEnabled);
            await PreparePickerHost(panel);
            await ClickOwned(panel, ButtonNamed(panel, "本屏快速选窗"));
            await Until(() => Application.Current.Windows.Cast<Window>().OfType<QuickSelectWindow>().Any());
            var picker = Application.Current.Windows.Cast<Window>().OfType<QuickSelectWindow>().Single(); await Ready(picker);
            Invoke(ButtonNamed(picker, "取消本屏选窗")); await Until(() => !picker.IsVisible);
            Equal(false, manager.HasUndo);
        }
        finally { panel.Exit(); }
    }

    private static async Task CheckScreenArrange()
    {
        var displays = WindowManager.ListDisplays();
        if (displays.Count < 2) throw new UnavailableException("Needs two connected displays.");
        foreach (var destination in displays.Take(2))
        {
            await using var fixture = await Fixture.Start(4);
            for (int i = 0; i < 3; i++) await fixture.Send($"position {i} {destination.WorkArea.X + 80 + i * 100} {destination.WorkArea.Y + 90 + i * 50}");
            var outside = displays.First(d => d.Handle != destination.Handle);
            await fixture.Send($"position 3 {outside.WorkArea.X + 100} {outside.WorkArea.Y + 100}");
            await fixture.Send("max 0");
            using var manager = new WindowManager();
            var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
            var original = targets.Select(manager.ReadState).ToArray();
            var panel = new MainWindow(manager, destination, targets, enableShortcuts: false); panel.Show();
            try
            {
                await panel.ArrangeCurrentScreenAsync(targets[0].Handle, targets);
                if (manager.HasPending) await Until(() => !manager.HasPending);
                Equal(destination.WorkArea.X, manager.ReadState(targets[0]).Bounds.X);
                Equal(false, manager.ReadState(targets[0]).Maximized);
                CheckGeometry(destination.WorkArea, targets.Take(3).Select(t => manager.ReadState(t).Bounds).ToArray());
                Equal(original[3], manager.ReadState(targets[3]));
                var applied = targets.Select(manager.ReadState).ToArray();
                await panel.ArrangeCurrentScreenAsync(targets[0].Handle, []);
                Equal(System.Windows.WindowState.Normal, panel.WindowState);
                await panel.ArrangeCurrentScreenAsync(targets[0].Handle, [targets[0]]);
                Equal(true, ((TextBlock)panel.FindName("StatusText")).Text.Contains("至少需要两扇"));
                Equal(true, manager.HasUndo);
                for (int i = 0; i < 4; i++) Equal(applied[i], manager.ReadState(targets[i]));
                Equal(true, (await manager.ApplyOnScreenAsync(targets.Take(3).ToArray(), destination with { WorkArea = destination.WorkArea with { Width = destination.WorkArea.Width - 1 } })).NeedsRefresh);
                Equal(true, manager.HasUndo);
                Equal(true, (await manager.UndoAsync()).Success);
                for (int i = 0; i < 4; i++) Equal(original[i], manager.ReadState(targets[i]));
            }
            finally { panel.Exit(); }
        }
    }

    private static async Task CheckScreenMemberMove()
    {
        var displays = WindowManager.ListDisplays();
        if (displays.Count < 2) throw new UnavailableException("Needs two connected displays.");
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager(); var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var outside = displays.First(d => d.Handle != display.Handle);
        var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
        var original = targets.Select(manager.ReadState).ToArray();
        await fixture.Send($"linkscreen {outside.WorkArea.X + 100} {outside.WorkArea.Y + 100}");
        var report = await manager.ApplyOnScreenAsync(targets, display);
        if (report.Pending) report = await manager.CompletePendingAsync();
        Equal(true, report.Windows[0].Success); Equal(false, report.Windows[1].Success);
        Equal(outside.Handle, WindowManager.DisplayFor(targets[1].Handle).Handle);
        var moved = manager.ReadState(targets[1]);
        Equal(false, (await manager.UndoAsync()).Success); Equal(true, manager.HasUndo);
        Equal(original[0], manager.ReadState(targets[0])); Equal(moved, manager.ReadState(targets[1]));
        await fixture.Send($"position 1 {original[1].Bounds.X} {original[1].Bounds.Y}");
        Equal(true, (await manager.UndoAsync()).Success); Equal(original[1], manager.ReadState(targets[1]));
    }

    private static async Task CheckScreenEligibility()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager(); var display = WindowManager.DisplayFor(fixture.Handles[0]);
        await fixture.Send("max 1");
        var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
        var original = targets.Select(manager.ReadState).ToArray();
        await fixture.Send("linkmin");
        var report = await manager.ApplyOnScreenAsync(targets, display);
        if (report.Pending) report = await manager.CompletePendingAsync();
        Equal(false, report.Success); Equal(true, report.Windows[0].Success); Equal(false, report.Windows[1].Success);
        Equal(true, Native.IsIconic(targets[1].Handle)); Equal(true, manager.HasUndo);
        Equal(true, (await manager.UndoAsync()).Success);
        for (int i = 0; i < 2; i++) Equal(original[i], manager.ReadState(targets[i]));
    }

    private static async Task CheckScreenPartial()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
        var originals = targets.Select(manager.ReadState).ToArray();
        await fixture.Send("linkclose");
        var result = await manager.ApplyOnScreenAsync(targets, display);
        if (result.Pending) result = await manager.CompletePendingAsync();
        Equal(true, result.Windows[0].Success); Equal(false, result.Windows[1].Success); Equal(true, result.Windows[2].Success);
        Equal(false, manager.HasPending); Equal(true, manager.HasUndo);
        Equal(true, (await manager.UndoAsync()).Success);
        Equal(originals[0], manager.ReadState(targets[0])); Equal(originals[2], manager.ReadState(targets[2]));
    }

    private static async Task CheckShortcutSettings()
    {
        string path = Path.Combine(Path.GetTempPath(), "PaneSet.Settings-" + Guid.NewGuid().ToString("N"), "settings.json");
        var open = new ShortcutBinding(3, 0x80); var restore = new ShortcutBinding(3, 0x81); var replacement = new ShortcutBinding(3, 0x82);
        new ShortcutSettings { Theme = AppearanceMode.Dark, Shortcuts = new() { [ShortcutAction.OpenPanel] = open, [ShortcutAction.Restore] = restore } }.Save(path);
        var owner = new Window { Width = 300, Height = 200 }; var blocker = new Window { Width = 250, Height = 180 };
        owner.Show(); blocker.Show(); int dispatched = 0;
        var handle = new System.Windows.Interop.WindowInteropHelper(owner).Handle;
        var other = new System.Windows.Interop.WindowInteropHelper(blocker).Handle;
        try
        {
            using (var shortcuts = new PanelShortcuts(handle, (action, _) => { if (action == ShortcutAction.OpenPanel) dispatched++; }, path, Enum.GetValues<ShortcutAction>()))
            {
                Equal("", shortcuts.Change(ShortcutAction.OpenPanel, replacement));
                Equal(replacement, shortcuts.BindingFor(ShortcutAction.OpenPanel));
                Equal(replacement, ShortcutSettings.Load(path, out _).For(ShortcutAction.OpenPanel));
                Equal(AppearanceMode.Dark, ShortcutSettings.Load(path, out _).Theme);
                Equal(true, RegisterHotKey(other, 20, 0x4003, open.Key)); UnregisterHotKey(other, 20);
                PostMessage(handle, 0x312, shortcuts.RegisteredId(ShortcutAction.OpenPanel), 0);
                await Until(() => dispatched == 1);
                Equal(true, RegisterHotKey(other, 21, 0x4003, 0x83));
                Equal(true, shortcuts.Change(ShortcutAction.OpenPanel, new(3, 0x83)).Contains("注册失败"));
                Equal(replacement, shortcuts.BindingFor(ShortcutAction.OpenPanel)); UnregisterHotKey(other, 21);
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Equal(true, shortcuts.Change(ShortcutAction.OpenPanel, open).Contains("未保存"));
                    Equal(replacement, shortcuts.BindingFor(ShortcutAction.OpenPanel));
                    Equal(replacement, ShortcutSettings.Load(path, out _).For(ShortcutAction.OpenPanel));
                    Equal(true, shortcuts.ChangeTheme(AppearanceMode.System).Contains("未保存"));
                    Equal(AppearanceMode.Dark, ShortcutSettings.Load(path, out _).Theme);
                    Equal(true, RegisterHotKey(other, 22, 0x4003, open.Key)); UnregisterHotKey(other, 22);
                }
                Equal("", shortcuts.ChangeTheme(AppearanceMode.Light));
                Equal(AppearanceMode.Light, ShortcutSettings.Load(path, out _).Theme);
                Equal(true, shortcuts.Change(ShortcutAction.OpenPanel, restore).Contains("其他动作"));
                Equal("", shortcuts.Change(ShortcutAction.OpenPanel, replacement));
                Equal("", shortcuts.Change(ShortcutAction.SelectScreen, new(3, 0x86)));
                var defaultB = ShortcutSettings.Default(ShortcutAction.SelectScreen);
                Equal(true, RegisterHotKey(other, 25, 0x4000 | defaultB.Modifiers, defaultB.Key));
                Equal(true, shortcuts.Change(ShortcutAction.SelectScreen, defaultB).Contains("注册失败"));
                Equal(new ShortcutBinding(3, 0x86), shortcuts.BindingFor(ShortcutAction.SelectScreen));
                UnregisterHotKey(other, 25);
                Equal("", shortcuts.Change(ShortcutAction.SelectScreen, defaultB));
                Equal(defaultB, ShortcutSettings.Load(path, out _).For(ShortcutAction.SelectScreen));
                Equal(AppearanceMode.Light, ShortcutSettings.Load(path, out _).Theme);
                Equal("", shortcuts.Change(ShortcutAction.OpenPanel, null));
                Equal(null, ShortcutSettings.Load(path, out _).For(ShortcutAction.OpenPanel));
                Equal(true, RegisterHotKey(other, 23, 0x4003, replacement.Key)); UnregisterHotKey(other, 23);
            }
            using (var restarted = new PanelShortcuts(handle, (_, _) => { }, path)) Equal(null, restarted.BindingFor(ShortcutAction.OpenPanel));
            Equal(true, RegisterHotKey(other, 24, 0x4003, restore.Key)); UnregisterHotKey(other, 24);
            File.WriteAllText(path, "{broken");
            Equal(ShortcutSettings.Default(ShortcutAction.OpenPanel), ShortcutSettings.Load(path, out string warning).For(ShortcutAction.OpenPanel));
            Equal(true, warning.Length > 0); Equal("{broken", File.ReadAllText(path));
            var upgrade = new ShortcutSettings(); upgrade.Shortcuts[ShortcutAction.OpenPanel] = null; upgrade.Save(path);
            Equal(ShortcutSettings.Default(ShortcutAction.ArrangeScreen), ShortcutSettings.Load(path, out _).For(ShortcutAction.ArrangeScreen));
        }
        finally
        {
            for (int i = 20; i <= 25; i++) UnregisterHotKey(other, i);
            owner.Close(); blocker.Close(); Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    private static async Task CheckConfiguredInput()
    {
        string path = Path.Combine(Path.GetTempPath(), "PaneSet.Keys-" + Guid.NewGuid().ToString("N"), "settings.json");
        new ShortcutSettings { Shortcuts = new() { [ShortcutAction.OpenPanel] = new(3, 0x80), [ShortcutAction.Restore] = null } }.Save(path);
        var owner = new Window { Width = 300, Height = 200 }; owner.Show(); int count = 0;
        try
        {
            using var shortcuts = new PanelShortcuts(new System.Windows.Interop.WindowInteropHelper(owner).Handle, (_, _) => count++, path, Enum.GetValues<ShortcutAction>());
            Equal("", shortcuts.Change(ShortcutAction.OpenPanel, new(3, 0x82)));
            Equal("", shortcuts.Change(ShortcutAction.ArrangeScreen, new(3, 0x84)));
            Equal("", shortcuts.Change(ShortcutAction.SelectScreen, new(3, 0x85)));
            owner.Activate(); SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(owner).Handle);
            await Until(() => GetForegroundWindow() == new System.Windows.Interop.WindowInteropHelper(owner).Handle);
            await Chord(0x82); await Until(() => count == 1);
            await Chord(0x80); await Task.Delay(120); Equal(1, count);
            await Chord(0x84); await Until(() => count == 2);
            await Chord(0x85); await Until(() => count == 3);
        }
        finally { owner.Close(); Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
