namespace PaneSet;

/// The pre-drag state of a dragged window, captured when its move loop begins.
internal sealed record DragSessionStart(WindowTarget Target, WindowStateInfo State, Native.Placement Placement,
    PlacementDisplay SourceEnvironment, Guid DesktopId);

public sealed partial class WindowManager
{
    private readonly PlacementEnvironment placementEnvironment;
    private bool singleWindowUndo;

    /// Captures the state a dragged window had when its move loop began, so a placement can be
    /// restored to the pre-drag geometry even though execution happens after the drop.
    internal bool TryBeginDragSession(nint window, out DragSessionStart? session, out string message)
    {
        session = null; message = "";
        try
        {
            Native.GetWindowThreadProcessId(window, out uint pid);
            if (pid == Environment.ProcessId || !Eligible(window)) return false;
            var target = ListWindows().FirstOrDefault(t => t.Handle == window);
            if (target == null) return false;
            var state = ReadState(target);
            var placement = new Native.Placement { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.Placement>() };
            if (!Native.GetWindowPlacement(window, ref placement)) throw new System.ComponentModel.Win32Exception();
            var environment = placementEnvironment.Capture(DisplayFor(window));
            session = new(target, state, placement, environment, placementEnvironment.DesktopFor(window));
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { message = e.Message; return false; }
    }

    internal bool IdentityAlive(DragSessionStart session)
    {
        try { return IdentityMatches(session.Target) && Eligible(session.Target.Handle); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    internal async Task<OperationReport> PlaceDraggedAsync(DragSessionStart session, WindowRect zone, DisplayArea destination)
    {
        lock (sync) if (operation != null) return new([], Pending: true);
        try
        {
            if (!IdentityMatches(session.Target) || !Eligible(session.Target.Handle))
                throw new InvalidOperationException("拖动的窗口已关闭、身份变化或不符合放置条件，本次未投放。");
            var destinationEnvironment = placementEnvironment.Capture(destination);
            var desktopId = placementEnvironment.DesktopFor(session.Target.Handle);
            if (desktopId != session.DesktopId)
                throw new InvalidOperationException("拖动的窗口所在桌面已改变，本次未投放。");
            void Validate()
            {
                if (!IdentityMatches(session.Target) || !Eligible(session.Target.Handle))
                    throw new InvalidOperationException("拖动的窗口已关闭、身份变化或不符合放置条件，已停止投放。");
                if (placementEnvironment.DesktopFor(session.Target.Handle) != desktopId)
                    throw new InvalidOperationException("拖动的窗口所在桌面已改变，已停止投放。");
            }
            lock (sync)
            {
                if (operation != null) return new([], Pending: true);
                operation = Task.Run(() => PlaceCore(session.Target, destinationEnvironment, desktopId, zone, Validate,
                    restoreSession: session));
            }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { return new([new(session.Target, false, e.Message)], NeedsRefresh: true); }
        return await CollectWithinDeadline();
    }

    private OperationReport PlaceCore(WindowTarget source, PlacementDisplay destination, Guid desktopId, WindowRect rect,
        Action validate, bool activateSource = true, PlacementDisplay? sourceEnvironment = null, DragSessionStart? restoreSession = null)
    {
        Snapshot? snapshot = null;
        bool mutationStarted = false, confirmedChange = false;
        WindowOutcome outcome;
        try
        {
            validate();
            if (activateSource && !placementEnvironment.Activate(source.Handle))
                throw new InvalidOperationException("未取得源窗口前台，未放置；原恢复记录保留。");
            validate();
            if (!placementEnvironment.IsForeground(source.Handle)) throw new InvalidOperationException("源窗口已失去前台，未放置。请重新唤起。");
            var origin = sourceEnvironment ?? placementEnvironment.Capture(DisplayFor(source.Handle));
            Guard(origin.Display);
            var placement = new Native.Placement { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.Placement>() };
            if (!Native.GetWindowPlacement(source.Handle, ref placement)) throw new System.ComponentModel.Win32Exception();
            // A drag placement restores the state captured when the drag began, not the drop position.
            var restoreState = restoreSession?.State ?? ReadState(source);
            var restorePlacement = restoreSession?.Placement ?? placement;
            var restoreSource = restoreSession?.SourceEnvironment ?? origin;
            snapshot = new(source, restoreState, restorePlacement, restoreSource.Display, destination.Display)
            { SourceEnvironment = restoreSource, DestinationEnvironment = destination, DesktopId = desktopId };
            void Guard(DisplayArea expected)
            {
                validate();
                if (placementEnvironment.Capture(origin.Display) != origin || Native.MonitorFromWindow(source.Handle, 2) != expected.Handle)
                    throw new InvalidOperationException("源窗口或来源显示环境已改变，已停止放置；已改变时可尝试恢复。");
                if (!activateSource && !placementEnvironment.IsForeground(source.Handle))
                    throw new InvalidOperationException("源窗口已失去前台，已停止放置；已改变时可尝试恢复。");
            }
            WindowStateInfo Observe(WindowStateInfo state)
            {
                if (state != snapshot.State && IdentityMatches(source)) confirmedChange = true;
                return state;
            }
            Guard(origin.Display);
            if (ReadState(source).Maximized)
            {
                mutationStarted = true;
                Native.ShowWindow(source.Handle, 4);
                if (!WaitFor(source, s => !Observe(s).Maximized)) throw new InvalidOperationException("应用未能退出最大化，可尝试恢复。");
            }
            if (ReadState(source) != new WindowStateInfo(rect, false))
            {
                Guard(origin.Display);
                SetVisibleBounds(source, rect, () =>
                {
                    Guard(origin.Display);
                    mutationStarted = true;
                });
                bool reached = WaitFor(source, s => Observe(s) == new WindowStateInfo(rect, false));
                // Crossing DPI boundaries can change the non-client frame after the first move.
                if (!reached && IdentityMatches(source) && Native.MonitorFromWindow(source.Handle, 2) == destination.Display.Handle
                    && origin.Dpi != destination.Dpi)
                {
                    SetVisibleBounds(source, rect, () => Guard(destination.Display));
                    reached = WaitFor(source, s => Observe(s) == new WindowStateInfo(rect, false));
                }
                if (!reached) throw new InvalidOperationException("未确认达到预期位置；若窗口已改变，可尝试恢复。");
            }
            validate();
            outcome = new(source, true, placementEnvironment.IsForeground(source.Handle) ? "已放入分区，源窗口在前。" : "已放入分区，未保持前台；未再次抢占焦点。");
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { outcome = new(source, false, e.Message); }
        if (mutationStarted && snapshot != null)
        {
            bool affected = false;
            try
            {
                var finalState = ReadState(source);
                affected = finalState != snapshot.State;
                if (outcome.Success && finalState != new WindowStateInfo(rect, false))
                    outcome = new(source, false, "未确认窗口保持预期位置；已改变时可尝试恢复，未改变时保留原恢复记录。");
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                bool sameIdentity = IdentityMatches(source);
                affected = sameIdentity || confirmedChange;
                outcome = new(source, false, sameIdentity ? "结果未能确认，可尝试恢复。"
                    : confirmedChange ? "已确认源窗口改变，但窗口已关闭或身份变化；恢复会跳过该窗口，先前恢复记录已替换。"
                    : "源窗口身份已失效，本次未能记录可恢复结果；原恢复记录保留。");
            }
            if (affected && IdentityMatches(source))
            {
                try
                {
                    // A size-limited application can leave even a failed move mostly on a neighbouring monitor.
                    var actual = placementEnvironment.Capture(DisplayFor(source.Handle));
                    snapshot = snapshot with { Destination = actual.Display, DestinationEnvironment = actual };
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
            lock (sync) if (!disposed && affected) { undo = [snapshot]; singleWindowUndo = true; }
        }
        return new([outcome]);
    }

    private void SetVisibleBounds(WindowTarget target, WindowRect rect, Action guard)
    {
        var visible = Native.Bounds(target.Handle);
        if (!Native.GetWindowRect(target.Handle, out var outer)) throw new System.ComponentModel.Win32Exception();
        guard();
        if (!Native.SetWindowPos(target.Handle, 0, rect.X - (visible.X - outer.Left), rect.Y - (visible.Y - outer.Top),
            rect.Width + outer.Right - outer.Left - visible.Width, rect.Height + outer.Bottom - outer.Top - visible.Height, 0x14))
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
    }

    private void ValidatePlacementRestore(Snapshot snapshot)
    {
        var target = snapshot.Target;
        if (placementEnvironment.DesktopFor(target.Handle) != snapshot.DesktopId
            || placementEnvironment.Capture(snapshot.SourceEnvironment!.Display) != snapshot.SourceEnvironment)
            throw new InvalidOperationException("来源桌面或显示环境已改变；恢复记录已保留，请回到原环境后重试。");
        nint current = Native.MonitorFromWindow(target.Handle, 2);
        var expected = current == snapshot.Display.Handle ? snapshot.SourceEnvironment
            : current == snapshot.Destination.Handle ? snapshot.DestinationEnvironment : null;
        if (expected == null || placementEnvironment.Capture(expected.Display) != expected)
            throw new InvalidOperationException("窗口当前显示环境已改变；恢复记录已保留，可稍后重试。");
    }
}
