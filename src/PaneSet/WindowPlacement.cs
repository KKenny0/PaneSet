namespace PaneSet;

public sealed partial class WindowManager
{
    private readonly PlacementEnvironment placementEnvironment;
    private LayoutPositions? recentPositions;
    private long positionsVersion;
    private bool singleWindowUndo;
    public LayoutPositions? RecentPositions { get { lock (sync) return recentPositions; } }

    private LayoutPositions? CapturePositions(WindowTarget[] targets, DisplayArea display, WindowRect[] layout)
    {
        try
        {
            var described = ListDisplays().FirstOrDefault(d => d.Handle == display.Handle);
            if (described == null) return null;
            var environment = placementEnvironment.Capture(display with { Name = described.Name });
            if (environment.Display.WorkArea != display.WorkArea) return null;
            var desktopId = placementEnvironment.DesktopFor(targets[0].Handle);
            if (targets.Any(t => !IdentityMatches(t) || placementEnvironment.DesktopFor(t.Handle) != desktopId)) return null;
            return new(Interlocked.Increment(ref positionsVersion), environment, desktopId, Array.AsReadOnly(layout.ToArray()));
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return null; }
    }

    private string PublishPositions(LayoutPositions? positions, WindowTarget[] targets)
    {
        try
        {
            if (positions == null || placementEnvironment.Capture(positions.Display) != positions.Environment
                || targets.Any(t => !IdentityMatches(t) || placementEnvironment.DesktopFor(t.Handle) != positions.DesktopId))
                throw new InvalidOperationException();
            lock (sync) if (!disposed) recentPositions = positions;
            return "";
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { return "本次排布已完成，但位置未能记住；已有位置仍来自此前成功记录，使用前会重新校验环境。"; }
    }

    private LayoutPositions ValidatePlacement(WindowTarget source, long version)
    {
        var positions = RecentPositions ?? throw new InvalidOperationException("先用 PaneSet 排布一次窗口，再使用单窗放置。");
        if (positions.Version != version) throw new InvalidOperationException("布局位置记录已改变，请重新打开单窗放置。");
        if (placementEnvironment.Capture(positions.Display) != positions.Environment)
            throw new InvalidOperationException("目标屏幕、工作区域或缩放已改变，请重新整组排布。");
        if (!IdentityMatches(source) || !Eligible(source.Handle))
            throw new InvalidOperationException("源窗口已关闭、身份变化或不符合放置条件，请从目标窗口重新唤起。");
        if (placementEnvironment.DesktopFor(source.Handle) != positions.DesktopId)
            throw new InvalidOperationException("布局位置在另一虚拟桌面，请返回原桌面或重新整组排布。");
        return positions;
    }

    internal bool IsPlacementDesktop(nint window, Guid id)
    {
        try { return placementEnvironment.DesktopFor(window) == id; }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    public bool CanPlace(WindowTarget source, long version, out string message)
    {
        try { ValidatePlacement(source, version); message = ""; return true; }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { message = e.Message; return false; }
    }

    public async Task<OperationReport> PlaceAsync(WindowTarget source, long version, int index)
    {
        lock (sync) if (operation != null) return new([], Pending: true);
        try
        {
            var positions = RecentPositions ?? throw new InvalidOperationException("先用 PaneSet 排布一次窗口，再使用单窗放置。");
            if (positions.Version != version) throw new InvalidOperationException("布局位置记录已改变，请重新打开单窗放置。");
            if (index < 0 || index >= positions.Regions.Count) throw new InvalidOperationException("布局位置编号无效，请重新选择。");
            var rect = positions.Regions[index];
            var work = positions.Display.WorkArea;
            if (rect.Width < 240 || rect.Height < 160 || rect.X < work.X || rect.Y < work.Y
                || (long)rect.X + rect.Width > (long)work.X + work.Width || (long)rect.Y + rect.Height > (long)work.Y + work.Height)
                throw new InvalidOperationException("布局位置超出原工作区域，请重新整组排布。");
            lock (sync)
            {
                if (operation != null) return new([], Pending: true);
                operation = Task.Run(() => PlaceCore(source, positions, rect));
            }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { return new([new(source, false, e.Message)], NeedsRefresh: true); }
        return await CollectWithinDeadline();
    }

    private OperationReport PlaceCore(WindowTarget source, LayoutPositions positions, WindowRect rect)
    {
        Snapshot? snapshot = null;
        bool mutationStarted = false, confirmedChange = false;
        WindowOutcome outcome;
        try
        {
            ValidatePlacement(source, positions.Version);
            if (!placementEnvironment.Activate(source.Handle))
                throw new InvalidOperationException("未取得源窗口前台，未放置；原恢复记录保留。");
            ValidatePlacement(source, positions.Version);
            if (!placementEnvironment.IsForeground(source.Handle)) throw new InvalidOperationException("源窗口已失去前台，未放置。请重新唤起。");
            var origin = placementEnvironment.Capture(DisplayFor(source.Handle));
            var placement = new Native.Placement { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.Placement>() };
            if (!Native.GetWindowPlacement(source.Handle, ref placement)) throw new System.ComponentModel.Win32Exception();
            snapshot = new(source, ReadState(source), placement, origin.Display, positions.Display)
            { SourceEnvironment = origin, DestinationEnvironment = positions.Environment, DesktopId = positions.DesktopId };
            void Guard(DisplayArea expected)
            {
                ValidatePlacement(source, positions.Version);
                if (placementEnvironment.Capture(origin.Display) != origin || Native.MonitorFromWindow(source.Handle, 2) != expected.Handle)
                    throw new InvalidOperationException("源窗口或来源显示环境已改变，已停止放置；已改变时可尝试恢复。");
            }
            WindowStateInfo Observe(WindowStateInfo state)
            {
                if (state != snapshot.State && IdentityMatches(source)) confirmedChange = true;
                return state;
            }
            Guard(origin.Display);
            if (snapshot.State.Maximized)
            {
                mutationStarted = true;
                Native.ShowWindow(source.Handle, 4);
                if (!WaitFor(source, s => !Observe(s).Maximized)) throw new InvalidOperationException("应用未能退出最大化，可尝试恢复。");
            }
            if (ReadState(source) != new WindowStateInfo(rect, false))
            {
                Guard(origin.Display);
                mutationStarted = true;
                SetVisibleBounds(source, rect, () => Guard(origin.Display));
                bool reached = WaitFor(source, s => Observe(s) == new WindowStateInfo(rect, false));
                // Crossing DPI boundaries can change the non-client frame after the first move.
                if (!reached && IdentityMatches(source) && Native.MonitorFromWindow(source.Handle, 2) == positions.Display.Handle
                    && origin.Dpi != positions.Environment.Dpi)
                {
                    SetVisibleBounds(source, rect, () => Guard(positions.Display));
                    reached = WaitFor(source, s => Observe(s) == new WindowStateInfo(rect, false));
                }
                if (!reached) throw new InvalidOperationException("未确认达到预期位置；若窗口已改变，可尝试恢复。");
            }
            ValidatePlacement(source, positions.Version);
            outcome = new(source, true, placementEnvironment.IsForeground(source.Handle) ? "位置已放置，源窗口在前。" : "位置已放置，未保持前台；未再次抢占焦点。");
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
