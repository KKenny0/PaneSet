using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static void RequireInputIdle()
    {
        if (Enumerable.Range(1, 255).Any(key => (GetAsyncKeyState(key) & 0x8000) != 0))
            throw new Exception("Input precondition: a key/button is already pressed; injection refused.");
    }

    private static void RequireForegroundInput(nint handle)
    {
        if (handle == 0 || !Native.IsWindowVisible(handle) || Native.IsIconic(handle) || !IsWindowEnabled(handle) || GetForegroundWindow() != handle)
            throw new Exception("Input target is not the visible enabled foreground window; injection refused.");
        RequireInputIdle();
    }

    private static nint KeyboardTarget(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        Native.GetWindowThreadProcessId(handle, out uint pid);
        if (pid != Environment.ProcessId || !window.IsVisible || window.WindowState != WindowState.Normal || !window.IsKeyboardFocusWithin
            || (window is QuickSelectWindow picker && !picker.IsReady))
            throw new Exception("Input target is not the ready focused check-owned window; injection refused.");
        return handle;
    }

    private static void RequireFixtureTarget(Fixture fixture, nint handle)
    {
        if (!fixture.Owns(handle))
            throw new Exception("Input target is not the live fixture-owned window; injection refused.");
    }

    private static Task SendKeys(Window window, params byte[] keys) => SendKeys(KeyboardTarget(window), keys);

    private static Task SendKeys(Fixture fixture, nint handle, params byte[] keys)
    {
        RequireFixtureTarget(fixture, handle);
        return SendKeys(handle, keys);
    }

    private static string WindowEvidence(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var foreground = GetForegroundWindow();
        Native.GetWindowRect(handle, out var bounds);
        return $"hwnd={handle}, loaded={window.IsLoaded}, visible={window.IsVisible}, state={window.WindowState}, wpfTopmost={window.Topmost}, nativeTopmost={(Native.GetWindowLong(handle, -20).ToInt64() & 8) != 0}, bounds={bounds.Value}, foreground={foreground}, foregroundClass={Native.Text(foreground, true)}, focus={Keyboard.FocusedElement?.GetType().Name}, modifiers={Keyboard.Modifiers}";
    }

    private static async Task SendLocalLayoutKeys(MainWindow window, UIElement target, params byte[] keys)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var source = HwndSource.FromHwnd(handle) ?? throw new Exception("No local input message source.");
        var observedKeys = new[] { 1, 2, 0x10, 0x11, 0x12, 0x5B, 0x5C }.Concat(keys.Select(k => (int)k)).Distinct().ToArray();
        var events = new List<string>();
        nint Observe(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
        {
            if (events.Count < 32 && message is 0x6 or 0x7 or 0x8 or 0x100 or 0x101 or 0x102 or 0x104 or 0x105)
                events.Add($"{message:X}:{wParam}");
            return 0;
        }
        string State()
        {
            var search = (TextBox)window.FindName("SearchBox");
            var foreground = GetForegroundWindow(); Native.GetWindowThreadProcessId(foreground, out uint pid);
            var pressed = observedKeys.Where(k => (GetAsyncKeyState(k) & 0x8000) != 0);
            return $"{WindowEvidence(window)}, foregroundPid={pid}, text={search.Text}, selection={search.SelectionStart},{search.SelectionLength}, pressed={string.Join(',', pressed.Select(k => k.ToString("X")))}";
        }
        source.AddHook(Observe);
        try
        {
            Console.WriteLine($"LOCAL INPUT before keys={string.Join(',', keys.Select(k => k.ToString("X")))} {State()}");
            if (!window.IsVisible || window.WindowState != WindowState.Normal || GetForegroundWindow() != handle || Keyboard.FocusedElement != target)
                throw new Exception("Local layout target is not the focused foreground window; injection refused.");
            if (observedKeys.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0))
                throw new Exception("Local layout precondition: an input key/button is already pressed; injection refused.");
            await SendKeys(window, keys);
        }
        finally
        {
            source.RemoveHook(Observe);
            Console.WriteLine($"LOCAL INPUT after {State()} events={string.Join('|', events)}");
        }
    }

    private static async Task SelectColumnsByInput(QuickSelectWindow picker)
    {
        var events = new List<string>();
        KeyEventHandler key = (_, e) => { if (events.Count < 16) events.Add($"{e.RoutedEvent.Name}:{e.Key}/{e.SystemKey}, modifiers={Keyboard.Modifiers}, handled={e.Handled}"); };
        void Completed(QuickSelectionResult result) => events.Add("session-ended: " + result.Message);
        picker.AddHandler(Keyboard.PreviewKeyDownEvent, key, true);
        picker.AddHandler(Keyboard.PreviewKeyUpEvent, key, true);
        picker.Completed += Completed;
        string before = WindowEvidence(picker);
        try
        {
            if (!picker.IsReady || !picker.IsVisible || !picker.IsKeyboardFocusWithin || GetForegroundWindow() != new WindowInteropHelper(picker).Handle)
                throw new Exception("Layout key target is not the ready foreground picker; injection refused.");
            if (new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, 0x31 }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0))
                throw new Exception("Layout key precondition failed: a key is already pressed; injection refused.");
            await SendKeys(picker, 0x12, 0x31);
            await Until(() => picker.Kind == ArrangementKind.Columns);
        }
        catch (Exception e) when (e is not UnavailableException)
        {
            throw new Exception($"Alt+1 failed. Before: {before}\nAfter: {WindowEvidence(picker)}\nEvents: {string.Join(" | ", events)}", e);
        }
        finally
        {
            picker.RemoveHandler(Keyboard.PreviewKeyDownEvent, key);
            picker.RemoveHandler(Keyboard.PreviewKeyUpEvent, key);
            picker.Completed -= Completed;
        }
    }

    // Automation controls the selection and buttons; this does not certify keyboard delivery.
    private static async Task CheckQuickCancelUndo()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
        var originals = targets.Select(manager.ReadState).ToArray();
        var panel = new MainWindow(manager, display, targets, enableShortcuts: false); panel.Show();
        QuickSelectWindow? picker = null;
        async Task<QuickSelectWindow> Open()
        {
            await fixture.Send("top 0");
            var bounds = manager.ReadState(targets[0]).Bounds;
            await ClickNativeOwned(fixture, targets[0].Handle, new Native.Point { X = bounds.X + 100, Y = bounds.Y + 12 });
            await Until(() => GetForegroundWindow() == targets[0].Handle);
            var result = panel.OpenQuickSelection(targets[0].Handle, targets)!;
            picker = result; await Ready(result); return result;
        }
        try
        {
            picker = await Open();
            Toggle(Candidate(picker, targets[1].Title));
            Elements<ComboBox>(picker).Single().SelectedItem = ArrangementKind.Columns;
            Equal(ArrangementKind.Columns, picker.Kind);
            Invoke(ButtonNamed(picker, "确认本屏选窗"));
            await Until(() => !picker.IsVisible && !manager.HasPending && manager.HasUndo);
            var layout = LayoutEngine.Arrange(display.WorkArea, 2, ArrangementKind.Columns);
            Equal(layout[0], manager.ReadState(targets[0]).Bounds);
            Equal(layout[1], manager.ReadState(targets[1]).Bounds);
            Equal(originals[2], manager.ReadState(targets[2]));
            var applied = targets.Select(manager.ReadState).ToArray();
            picker = await Open();
            Equal(1, Elements<CheckBox>(picker).Count(c => c.IsChecked == true));
            Toggle(Candidate(picker, targets[2].Title));
            Elements<ComboBox>(picker).Single().SelectedItem = ArrangementKind.MainAndOthers;
            Equal(ArrangementKind.MainAndOthers, picker.Kind);
            Invoke(ButtonNamed(picker, "取消本屏选窗")); await Until(() => !picker.IsVisible);
            Equal(true, manager.HasUndo);
            for (int i = 0; i < targets.Length; i++) Equal(applied[i], manager.ReadState(targets[i]));
            Equal(true, (await manager.UndoAsync()).Success); Equal(false, manager.HasUndo);
            for (int i = 0; i < targets.Length; i++) Equal(originals[i], manager.ReadState(targets[i]));
        }
        finally { picker?.Close(); panel.Exit(); }
    }
}
