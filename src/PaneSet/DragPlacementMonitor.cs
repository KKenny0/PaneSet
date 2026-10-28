using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace PaneSet;

/// Observes system move/size loops and drives the drag placement state machine. All native
/// observation happens here; decisions stay in DragPlacementStateMachine and checks drive that
/// machine with synthetic input.
internal sealed class DragPlacementMonitor : IDisposable
{
    private readonly WindowManager manager;
    private readonly Func<uint> modifierMask;
    private readonly Action<DragSessionStart, DragPlacementCommit, DisplayArea> commit;
    private readonly Action<string> note;
    private readonly Native.EventProc eventProc;
    private readonly GCHandle eventRoot;
    private readonly nint eventHook;
    private readonly DispatcherTimer timer;
    private readonly DragPlacementStateMachine machine = new();
    private readonly DragPlacementOverlay overlay = new();

    private nint sessionWindow;
    private DragSessionStart? session;
    private nint keyboardHook, mouseHook;
    private Native.LowLevelHookProc? keyboardProc, mouseProc;
    private bool escapeSeen, rightSeen, leftReleaseSeen, pendingBlocked, lastModifierHeld;
    private int lastHit = -1;
    private DragZoneView view = new(new(0, 0, 0, 0), [], []);
    private bool overlayShown;
    private bool disposed;

    private static void Debug(string text)
    {
        if (Environment.GetEnvironmentVariable("PANESET_DRAG_DEBUG") != "1") return;
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "paneset-drag-debug.log"), $"{DateTime.Now:HH:mm:ss.fff} {text}\n"); }
        catch { }
    }

    public DragPlacementMonitor(WindowManager manager, Func<uint> modifierMask,
        Action<DragSessionStart, DragPlacementCommit, DisplayArea> commit, Action<string> note)
    {
        Debug("ctor");
        this.manager = manager; this.modifierMask = modifierMask; this.commit = commit; this.note = note;
        eventProc = (_, evt, window, obj, child, _, _) =>
        {
            if (obj != 0 || child != 0 || disposed) return;
            Debug($"event {(evt == 0x000A ? "start" : "end")} hwnd={window}");
            if (evt == 0x000A) Dispatcher.BeginInvoke(DispatcherPriority.Send, () => Start(window));
            else if (evt == 0x000B) Dispatcher.BeginInvoke(DispatcherPriority.Send, () => End(window));
        };
        eventRoot = GCHandle.Alloc(eventProc);
        eventHook = Native.SetWinEventHook(0x000A, 0x000B, 0, eventProc, 0, 0, 2);
        if (eventHook == 0) { eventRoot.Free(); Debug("ctor hook failed"); throw new InvalidOperationException("无法监测窗口拖动，拖动投放不可用。"); }
        Debug($"ctor ok hook={eventHook}");
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Send, (_, _) => Tick(), Dispatcher.CurrentDispatcher);
    }

    private Dispatcher Dispatcher => timer.Dispatcher;

    private static bool PrimaryButtonDown()
    {
        // Button swap (SM_SWAPBUTTON) happens above the raw layer: GetAsyncKeyState and the
        // low-level hook report physical buttons, so the primary must be read through the map.
        bool swapped = Native.GetSystemMetrics(23) != 0;
        return (Native.GetAsyncKeyState(swapped ? 0x02 : Native.VirtualLeftButton) & 0x8000) != 0;
    }

    private void Start(nint window)
    {
        if (disposed || sessionWindow != 0 || window == 0) return;
        Native.GetWindowThreadProcessId(window, out uint pid);
        string reason = "";
        if (pid == Environment.ProcessId || !manager.TryBeginDragSession(window, out var started, out reason) || started == null)
        { Debug($"session rejected hwnd={window} ownPid={pid == Environment.ProcessId} reason={reason}"); return; }
        sessionWindow = window;
        session = started;
        Debug($"session begin hwnd={window} leftDownAtStart={(Native.GetAsyncKeyState(Native.VirtualLeftButton) & 0x8000) != 0} primaryDownAtStart={PrimaryButtonDown()}");
        escapeSeen = rightSeen = leftReleaseSeen = lastModifierHeld = false;
        lastHit = -1;
        pendingBlocked = manager.HasPending;
        bool primaryDown = PrimaryButtonDown();
        machine.Start(primaryDown, started.State.Maximized, started.State.Bounds);
        view = new(new(0, 0, 0, 0), [], []);
        if (primaryDown) InstallHooks();
        timer.Start();
    }

    private void InstallHooks()
    {
        keyboardProc = (code, wparam, lparam) =>
        {
            if (code >= 0 && (wparam == Native.KeyDownMessage || wparam == Native.SystemKeyDownMessage))
            {
                var data = Marshal.PtrToStructure<Native.LowLevelKeyboardHook>(lparam);
                if ((data.Flags & Native.InjectedLowLevel) == 0 && data.Key == 0x1B)
                { escapeSeen = true; machine.ObserveCancel(true, false); }
            }
            return Native.CallNextHookEx(keyboardHook, code, wparam, lparam);
        };
        mouseProc = (code, wparam, lparam) =>
        {
            if (code >= 0)
            {
                var data = Marshal.PtrToStructure<Native.LowLevelMouseHook>(lparam);
                Debug($"mouse wparam={wparam} flags={data.Flags} pos={data.Position.X},{data.Position.Y}");
                // Driver-level button remapping injects the user's real buttons with the injected
                // flag, so mouse events are accepted regardless; a secondary-button cancel is
                // already covered by the end-state button check (loop ends, primary still down).
                // Both up messages are accepted because logical and physical mappings vary.
                if (wparam == Native.LeftButtonUpMessage || wparam == 0x205) leftReleaseSeen = true;
            }
            return Native.CallNextHookEx(mouseHook, code, wparam, lparam);
        };
        nint module = Native.GetModuleHandleW(null);
        keyboardHook = Native.SetWindowsHookExW(Native.KeyboardHook, keyboardProc, module, 0);
        mouseHook = Native.SetWindowsHookExW(Native.MouseHook, mouseProc, module, 0);
        if (keyboardHook == 0 || mouseHook == 0) RemoveHooks();
    }

    private void RemoveHooks()
    {
        if (keyboardHook != 0) { Native.UnhookWindowsHookEx(keyboardHook); keyboardHook = 0; }
        if (mouseHook != 0) { Native.UnhookWindowsHookEx(mouseHook); mouseHook = 0; }
        keyboardProc = null; mouseProc = null;
    }

    private void Tick()
    {
        if (disposed || sessionWindow == 0 || session == null) { timer.Stop(); return; }
        try
        {
            if (!Native.IsWindow(sessionWindow) || !manager.IdentityAlive(session)) { End(0); return; }
            var state = manager.ReadState(session.Target);
            bool leftDown = PrimaryButtonDown();
            lastModifierHeld = IsModifierHeld();
            if (!Native.GetCursorPos(out var pointer)) return;
            nint monitor = Native.MonitorFromPoint(pointer, 2);
            var work = Native.Monitor(monitor).Work.Value;
            if (work != view.WorkArea)
            {
                if (machine.SessionEngaged) ShowOverlay(work);
                else view = new(work, Zones(work), Slots(work, 1));
            }
            if (machine.HoverTemplate(pointer, view.Slots) is { } switched)
            {
                view = view with { Zones = Zones(view.WorkArea) };
                LastTemplate = switched;
            }
            machine.Sample(lastModifierHeld, leftDown, pointer, state.Bounds, view);
            lastHit = machine.HitZoneIndex;
            Debug($"tick held={lastModifierHeld} mask={modifierMask()} left={leftDown} engaged={machine.SessionEngaged} zones={view.Zones.Length} overlay={overlayShown} hit={lastHit} work={work} ptr={pointer.X},{pointer.Y}");
            if (machine.SessionEngaged)
            {
                if (!overlayShown) ShowOverlay(view.WorkArea);
                if (overlayShown) overlay.Update(view, machine.ActiveTemplate, machine.HitZoneIndex, true);
            }
            else if (overlayShown)
            {
                overlay.Dispose(); overlayShown = false;
                view = view with { Zones = Zones(view.WorkArea) };
            }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { Debug($"tick failed: {e.Message}"); End(0); }
    }

    /// The template used by the most recent session; survives sessions within this process.
    public ZoneTemplate LastTemplate { get; private set; } = ZoneTemplate.TwoColumns;

    private WindowRect[] Zones(WindowRect work)
    {
        try { return LayoutEngine.Zones(work, machine.ActiveTemplate); }
        catch (ArgumentOutOfRangeException) { return []; }
    }

    private static DragThumbSlot[] Slots(WindowRect work, double scale) => DragThumbSlotLayout.For(work, scale);

    private void ShowOverlay(WindowRect work)
    {
        try
        {
            overlay.Show(work);
            overlayShown = true;
            view = new(work, Zones(work), Slots(work, overlay.Scale));
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { Debug($"overlay failed: {e.Message}"); overlay.Dispose(); overlayShown = false; }
    }

    private bool IsModifierHeld()
    {
        uint mask = modifierMask();
        if (mask == 0 || mask > 15) return false;
        uint current = 0;
        if ((Native.GetAsyncKeyState(0x10) & 0x8000) != 0) current |= 4;
        if ((Native.GetAsyncKeyState(0x11) & 0x8000) != 0) current |= 2;
        if ((Native.GetAsyncKeyState(0x12) & 0x8000) != 0) current |= 1;
        if ((Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0) current |= 8;
        // Exact match: an extra modifier held for the application itself must not trigger placement.
        return current == mask;
    }

    private void End(nint window)
    {
        if (sessionWindow == 0 || (window != 0 && window != sessionWindow)) return;
        timer.Stop();
        RemoveHooks();
        bool leftDown = PrimaryButtonDown();
        var started = session;
        try
        {
            var decision = pendingBlocked ? null : machine.ShouldCommit(view, leftReleaseSeen, leftDown);
            Debug($"end hwnd={window} engaged={machine.SessionEngaged} leftRelease={leftReleaseSeen} esc={escapeSeen} right={rightSeen} held={lastModifierHeld} hit={lastHit} commit={decision != null} pendingBlocked={pendingBlocked}");
            if (decision == null && pendingBlocked && lastModifierHeld && lastHit >= 0 && !machine.Cancelled)
                note("正在等待窗口响应，完成前不能投放。");
            if (decision is { } placement && started != null)
            {
                nint monitor = Native.MonitorFromPoint(new() { X = placement.WorkArea.X + placement.WorkArea.Width / 2,
                    Y = placement.WorkArea.Y + placement.WorkArea.Height / 2 }, 2);
                commit(started, placement, new DisplayArea(monitor, Native.Monitor(monitor).Work.Value));
            }
        }
        finally
        {
            machine.End();
            overlay.Dispose(); overlayShown = false;
            sessionWindow = 0; session = null;
            pendingBlocked = false; lastHit = -1; lastModifierHeld = false;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop();
        End(0);
        overlay.Dispose();
        Native.UnhookWinEvent(eventHook);
        eventRoot.Free();
    }
}

internal static class DragThumbSlotLayout
{
    internal static DragThumbSlot[] For(WindowRect work, double scale)
    {
        int width = Math.Max(1, (int)(104 * scale)), height = Math.Max(1, (int)(64 * scale)), gap = Math.Max(0, (int)(10 * scale));
        var order = ZoneTemplateCatalog.Order;
        long total = (long)width * order.Length + (long)gap * (order.Length - 1);
        int x = (int)(work.X + Math.Max(0, (work.Width - total) / 2));
        int y = work.Y + (int)(14 * scale);
        var slots = new DragThumbSlot[order.Length];
        for (int i = 0; i < order.Length; i++)
        {
            slots[i] = new(new(x, y, width, height), order[i]);
            x += width + gap;
        }
        return slots;
    }
}
