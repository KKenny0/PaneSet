using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckDragPlacement()
    {
        await using var fixture = await Fixture.Start(3);
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var untouched = manager.ReadState(targets[2]);
        var zones = LayoutEngine.Zones(display.WorkArea, ZoneTemplate.TwoColumns);

        // The session is captured at drag start, the window is moved by the "user drag", then the
        // placement executes: exactly what a real drag does, minus the native move loop.
        Equal(true, manager.TryBeginDragSession(targets[0].Handle, out var session, out string message));
        Equal("", message);
        var before = session!.State;
        await fixture.Send("position 0 400 300");
        await MakeForeground(targets[0].Handle);
        var result = await manager.PlaceDraggedAsync(session, zones[1], display);
        if (result.Pending) result = await manager.CompletePendingAsync();
        Equal(true, result.Success);
        Equal(zones[1], manager.ReadState(targets[0]).Bounds);
        Equal(untouched, manager.ReadState(targets[2])); // bystanders keep their geometry
        Equal(true, manager.HasUndo);
        Equal(true, (await manager.UndoAsync()).Success);
        Equal(before, manager.ReadState(targets[0])); // restored to the pre-drag state, not the drop position

        // A maximized source is captured maximized and restores maximized.
        await fixture.Send("max 1");
        var maximizedTarget = manager.ListWindows().Single(t => t.Handle == fixture.Handles[1]);
        Equal(true, manager.TryBeginDragSession(maximizedTarget.Handle, out var maximizedSession, out _));
        Equal(true, maximizedSession!.State.Maximized);
        await MakeForeground(maximizedTarget.Handle);
        result = await manager.PlaceDraggedAsync(maximizedSession, zones[0], display);
        if (result.Pending) result = await manager.CompletePendingAsync();
        Equal(true, result.Success);
        Equal(false, manager.ReadState(maximizedTarget).Maximized);
        Equal(zones[0], manager.ReadState(maximizedTarget).Bounds);
        Equal(true, (await manager.UndoAsync()).Success);
        Equal(true, manager.ReadState(maximizedTarget).Maximized);

        // A session whose window closed before execution fails without creating a restore record.
        Equal(true, manager.TryBeginDragSession(targets[2].Handle, out var closedSession, out _));
        await fixture.Send("close 2");
        result = await manager.PlaceDraggedAsync(closedSession!, zones[0], display);
        if (result.Pending) result = await manager.CompletePendingAsync();
        Equal(false, result.Success);
        Equal(false, manager.HasUndo);

        // Ineligible windows never start a session: a window of this process.
        Equal(false, manager.TryBeginDragSession(Process.GetCurrentProcess().MainWindowHandle, out var own, out _));
        Equal(null, own);
    }

    /// A real drag always has its source in the foreground. The check marks itself as the last
    /// input receiver with a zero-delta synthetic mouse event (a synthetic input with no visible
    /// effect), attaches its input queue to the foreground and the target, and then hands the
    /// foreground to the fixture.
    private static async Task MakeForeground(nint handle)
    {
        if (GetForegroundWindow() == handle) return;
        RequireInputIdle();
        // A zero-delta synthetic move marks this process as the last input receiver, which is the
        // system's own rule for who may hand the foreground around; it has no visible effect.
        SendInput(1, [new MouseInput { Type = 0, Flags = 1 }], 40);
        uint me = GetCurrentThreadId();
        nint foreground = GetForegroundWindow();
        uint foregroundThread = foreground == 0 ? 0 : GetWindowThreadProcessId(foreground, out _);
        uint targetThread = GetWindowThreadProcessId(handle, out _);
        bool attachedForeground = foregroundThread != 0 && foregroundThread != me && AttachThreadInput(me, foregroundThread, true);
        bool attachedTarget = targetThread != 0 && targetThread != me && AttachThreadInput(me, targetThread, true);
        try { SetForegroundWindow(handle); }
        finally
        {
            if (attachedTarget) AttachThreadInput(me, targetThread, false);
            if (attachedForeground) AttachThreadInput(me, foregroundThread, false);
        }
        await Until(() => GetForegroundWindow() == handle);
    }

    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint source, uint target, bool attach);
}
