using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static string WindowEvidence(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var foreground = GetForegroundWindow();
        Native.GetWindowRect(handle, out var bounds);
        return $"hwnd={handle}, loaded={window.IsLoaded}, visible={window.IsVisible}, state={window.WindowState}, wpfTopmost={window.Topmost}, nativeTopmost={(Native.GetWindowLong(handle, -20).ToInt64() & 8) != 0}, bounds={bounds.Value}, foreground={foreground}, foregroundClass={Native.Text(foreground, true)}, focus={Keyboard.FocusedElement?.GetType().Name}, modifiers={Keyboard.Modifiers}";
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
            await SendKeys(0x12, 0x31);
            await Until(() => picker.Kind == ArrangementKind.Columns);
        }
        catch (Exception e)
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
            await ClickNativeOwned(targets[0].Handle, new Native.Point { X = bounds.X + 100, Y = bounds.Y + 12 });
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
