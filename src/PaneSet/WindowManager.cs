namespace PaneSet;

public sealed record DisplayArea(nint Handle, WindowRect WorkArea)
{
    public string Name { get; init; } = "显示器";
    public override string ToString() => $"{Name} · {WorkArea.Width} × {WorkArea.Height}";
}
public sealed record WindowTarget(nint Handle, uint ProcessId, uint ThreadId, long Started, string ClassName,
    long Generation, string Application, string Title);
public sealed record WindowStateInfo(WindowRect Bounds, bool Maximized);
public sealed record WindowOutcome(WindowTarget Target, bool Success, string Message);
public sealed record OperationReport(IReadOnlyList<WindowOutcome> Windows, bool Pending = false, bool NeedsRefresh = false)
{
    public bool Success => !Pending && !NeedsRefresh && Windows.Count > 0 && Windows.All(w => w.Success);
}

public sealed class WindowManager : IDisposable
{
    public static nint Foreground => Native.GetForegroundWindow();
    public static DisplayArea DisplayFor(nint window)
    {
        nint monitor;
        if (window != 0) monitor = Native.MonitorFromWindow(window, 2);
        else { Native.GetCursorPos(out var point); monitor = Native.MonitorFromPoint(point, 2); }
        return new(monitor, Native.Monitor(monitor).Work.Value);
    }
    public static IReadOnlyList<DisplayArea> ListDisplays()
    {
        var handles = new List<nint>();
        Native.EnumDisplayMonitors(0, 0, (monitor, _, _, _) => { handles.Add(monitor); return true; }, 0);
        return handles.OrderByDescending(h => (Native.Monitor(h).Flags & 1) != 0)
            .ThenBy(h => Native.Monitor(h).Monitor.Left).ThenBy(h => Native.Monitor(h).Monitor.Top)
            .Select((h, i) => new DisplayArea(h, Native.Monitor(h).Work.Value) { Name = $"显示器 {i + 1}" + ((Native.Monitor(h).Flags & 1) != 0 ? "（主屏）" : "") }).ToArray();
    }
    private readonly object sync = new();
    private readonly Dictionary<nint, long> known = [];
    private readonly IVirtualDesktopManager desktop;
    private readonly Native.EventProc destroyed;
    private readonly System.Runtime.InteropServices.GCHandle eventRoot;
    private readonly nint hook;
    private long generation;
    private bool disposed;

    public WindowManager()
    {
        desktop = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A"))!)!;
        destroyed = (_, _, window, obj, child, _, _) => { if (obj == 0 && child == 0) lock (sync) known.Remove(window); };
        eventRoot = System.Runtime.InteropServices.GCHandle.Alloc(destroyed);
        hook = Native.SetWinEventHook(0x8001, 0x8001, 0, destroyed, 0, 0, 2);
        if (hook == 0) { eventRoot.Free(); throw new InvalidOperationException("无法监测窗口关闭，请重新启动。"); }
    }

    public IReadOnlyList<WindowTarget> ListWindows(DisplayArea? display = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var windows = new List<WindowTarget>();
        Native.EnumWindows((window, _) =>
        {
            try
            {
                if (!Eligible(window, display)) return true;
                uint thread = Native.GetWindowThreadProcessId(window, out uint pid);
                using var process = System.Diagnostics.Process.GetProcessById((int)pid);
                long token;
                lock (sync)
                {
                    if (!known.TryGetValue(window, out token)) known[window] = token = ++generation;
                }
                windows.Add(new(window, pid, thread, process.StartTime.ToUniversalTime().Ticks,
                    Native.Text(window, true), token, process.ProcessName, Native.Text(window)));
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or ArgumentException or InvalidOperationException) { }
            return true;
        }, 0);
        return windows;
    }

    private bool Eligible(nint window, DisplayArea? display = null)
    {
        Native.GetWindowThreadProcessId(window, out uint pid);
        long style = Native.GetWindowLong(window, -16).ToInt64();
        long extended = Native.GetWindowLong(window, -20).ToInt64();
        if (pid == Environment.ProcessId || !Native.IsWindowVisible(window) || Native.IsIconic(window)
            || Native.GetWindow(window, 4) != 0 || (style & 0x40000) == 0 || (extended & 0x80) != 0
            || (display != null && Native.MonitorFromWindow(window, 2) != display.Handle) || string.IsNullOrWhiteSpace(Native.Text(window))) return false;
        if (Native.Cloaked(window, 14, out int cloaked, 4) != 0 || cloaked != 0) return false;
        if (desktop.IsWindowOnCurrentVirtualDesktop(window, out bool current) != 0 || !current) return false;
        var bounds = Native.Bounds(window);
        var monitor = Native.Monitor(Native.MonitorFromWindow(window, 2)).Monitor.Value;
        return Native.IsZoomed(window) || (style & 0xC00000) != 0
            || bounds.X > monitor.X || bounds.Y > monitor.Y || bounds.Width < monitor.Width || bounds.Height < monitor.Height;
    }

    private bool IdentityMatches(WindowTarget target)
    {
        lock (sync) if (disposed || !known.TryGetValue(target.Handle, out long token) || token != target.Generation) return false;
        uint thread = Native.GetWindowThreadProcessId(target.Handle, out uint pid);
        if (pid != target.ProcessId || thread != target.ThreadId || Native.Text(target.Handle, true) != target.ClassName) return false;
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            return process.StartTime.ToUniversalTime().Ticks == target.Started;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    private sealed record Snapshot(WindowTarget Target, WindowStateInfo State, Native.Placement Placement, DisplayArea Display, DisplayArea Destination);
    private List<Snapshot> undo = [];
    private Task<OperationReport>? operation;
    public bool HasUndo { get { lock (sync) return undo.Count > 0; } }
    public bool HasPending { get { lock (sync) return operation != null; } }
    public bool CanCollect { get { lock (sync) return operation?.IsCompleted == true; } }

    public Task<OperationReport> ApplyOnScreenAsync(IReadOnlyList<WindowTarget> targets, DisplayArea display, ArrangementKind kind = ArrangementKind.Automatic)
        => StartApply(targets, display, kind, true);

    public Task<OperationReport> ApplyAsync(IReadOnlyList<WindowTarget> targets, DisplayArea display, ArrangementKind kind = ArrangementKind.Automatic)
        => StartApply(targets, display, kind, false);

    private async Task<OperationReport> StartApply(IReadOnlyList<WindowTarget> targets, DisplayArea display, ArrangementKind kind, bool currentScreenOnly)
    {
        if (targets.Count < 2 || targets.Select(t => t.Handle).Distinct().Count() != targets.Count)
            throw new ArgumentException("请选择至少两扇不同的窗口。");
        var layout = LayoutEngine.Arrange(display.WorkArea, targets.Count, kind);
        if (layout.Any(r => r.Width < 240 || r.Height < 160))
            return new(targets.Select(t => new WindowOutcome(t, false, "预期窗口小于 240 × 160，请减少选择或改用网格。")).ToArray());
        lock (sync)
        {
            if (operation != null) return new([], Pending: true);
            if (!DisplayMatches(display) || targets.Any(t => !IdentityMatches(t) || !Eligible(t.Handle, currentScreenOnly ? display : null)))
                return new(targets.Select(t => new WindowOutcome(t, false, "窗口或工作区域已改变，请刷新后重新选择。")).ToArray(), NeedsRefresh: true);
            var selected = targets.ToArray();
            operation = Task.Run(() => ApplyCore(selected, display, layout, currentScreenOnly));
        }
        return await CollectWithinDeadline();
    }

    private OperationReport ApplyCore(WindowTarget[] targets, DisplayArea display, WindowRect[] layout, bool currentScreenOnly)
    {
        Snapshot[] originals;
        try
        {
            originals = targets.Select(target =>
            {
                var source = DisplayFor(target.Handle);
                if (!DisplayMatches(display) || !IdentityMatches(target) || !ContextMatches(target, source) || !Eligible(target.Handle, currentScreenOnly ? display : null))
                    throw new InvalidOperationException("窗口或工作区域已改变，请刷新。");
                var placement = new Native.Placement { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.Placement>() };
                if (!Native.GetWindowPlacement(target.Handle, ref placement)) throw new System.ComponentModel.Win32Exception();
                return new Snapshot(target, ReadState(target), placement, source, display);
            }).ToArray();
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new(targets.Select(t => new WindowOutcome(t, false, e.Message)).ToArray(), NeedsRefresh: true);
        }
        var affected = new List<Snapshot>();
        var outcomes = new List<WindowOutcome>();
        bool mutationStarted = false;
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            var snapshot = originals[i];
            if (currentScreenOnly && !DisplayMatches(display))
            {
                outcomes.AddRange(targets.Skip(i).Select(t => new WindowOutcome(t, false, "目标屏幕或工作区域已改变，已停止后续排布。")));
                break;
            }
            try
            {
                if (!IdentityMatches(target)) throw new InvalidOperationException("窗口已关闭或身份发生变化。");
                if (!ContextMatches(target, snapshot.Display) || !Eligible(target.Handle, currentScreenOnly ? display : null)
                    || Native.Monitor(display.Handle).Work.Value != display.WorkArea)
                    throw new InvalidOperationException("窗口所属桌面或显示器已改变，请刷新。");
                if (snapshot.State.Maximized)
                {
                    if (!IdentityMatches(target)) throw new InvalidOperationException("窗口已关闭或身份发生变化。");
                    mutationStarted = true;
                    Native.ShowWindow(target.Handle, 4);
                    if (!WaitFor(target, s => !s.Maximized)) throw new InvalidOperationException("应用未能退出最大化状态。");
                }
                var visible = Native.Bounds(target.Handle);
                if (!Native.GetWindowRect(target.Handle, out var outer)) throw new System.ComponentModel.Win32Exception();
                var rect = layout[i];
                if (!IdentityMatches(target)) throw new InvalidOperationException("窗口已关闭或身份发生变化。");
                if (!ContextMatches(target, snapshot.Display) || !Eligible(target.Handle)
                    || Native.Monitor(display.Handle).Work.Value != display.WorkArea)
                    throw new InvalidOperationException("窗口状态、桌面或显示器已改变，请刷新。");
                // Synchronous native work runs off the UI thread; a timeout never releases conflicting mutations.
                mutationStarted = true;
                if (!Native.SetWindowPos(target.Handle, 0, rect.X - (visible.X - outer.Left), rect.Y - (visible.Y - outer.Top),
                    rect.Width + outer.Right - outer.Left - visible.Width, rect.Height + outer.Bottom - outer.Top - visible.Height, 0x14))
                    throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                bool success = WaitFor(target, s => s.Bounds == rect && !s.Maximized);
                outcomes.Add(new(target, success, success ? "已排布" : "未确认窗口达到预期布局；若窗口已改变，可尝试恢复。"));
                if (success && IdentityMatches(target)) Native.SetWindowPos(target.Handle, 0, 0, 0, 0, 0, 0x13);
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            { outcomes.Add(new(target, false, e.Message)); }
        }
        if (mutationStarted)
        {
            foreach (var snapshot in originals)
            {
                var target = snapshot.Target;
                try { if (ReadState(target) != snapshot.State) affected.Add(snapshot); }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
                { if (IdentityMatches(target)) affected.Add(snapshot); }
            }
        }
        lock (sync) if (affected.Count > 0) undo = affected;
        return new(outcomes);
    }

    private static bool DisplayMatches(DisplayArea display)
    {
        try { return Native.Monitor(display.Handle).Work.Value == display.WorkArea; }
        catch (InvalidOperationException) { return false; }
    }

    public bool TryReadOnScreen(WindowTarget target, DisplayArea display, out WindowStateInfo? state)
    {
        state = null;
        try
        {
            if (!DisplayMatches(display) || !IdentityMatches(target) || !Eligible(target.Handle, display)) return false;
            state = ReadState(target); return true;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    private bool ContextMatches(WindowTarget target, DisplayArea display) =>
        Native.Monitor(display.Handle).Work.Value == display.WorkArea && Native.MonitorFromWindow(target.Handle, 2) == display.Handle
        && desktop.IsWindowOnCurrentVirtualDesktop(target.Handle, out bool current) == 0 && current;

    private bool WaitFor(WindowTarget target, Func<WindowStateInfo, bool> expected)
    {
        for (int i = 0; i < 20; i++)
        {
            if (!IdentityMatches(target)) return false;
            if (expected(ReadState(target))) return true;
            Thread.Sleep(25);
        }
        return false;
    }

    private async Task<OperationReport> CollectWithinDeadline()
    {
        Task<OperationReport> active;
        lock (sync) active = operation!;
        if (await Task.WhenAny(active, Task.Delay(700)) != active) return new([], Pending: true);
        return await CompletePendingAsync();
    }

    public async Task<OperationReport> CompletePendingAsync()
    {
        Task<OperationReport>? active;
        lock (sync) active = operation;
        if (active == null) return new([]);
        try { return await active; }
        finally { lock (sync) if (operation == active) operation = null; }
    }

    public async Task<OperationReport> UndoAsync()
    {
        lock (sync)
        {
            if (operation != null) return new([], Pending: true);
            var snapshots = undo.ToArray();
            operation = Task.Run(() => RestoreCore(snapshots));
        }
        return await CollectWithinDeadline();
    }

    private OperationReport RestoreCore(Snapshot[] snapshots)
    {
        var remaining = new List<Snapshot>();
        var outcomes = new List<WindowOutcome>();
        foreach (var snapshot in snapshots)
        {
            var target = snapshot.Target;
            try
            {
                if (!IdentityMatches(target)) throw new InvalidOperationException("窗口已关闭或身份变化，已跳过。");
                // A partial arrangement may leave a target on either its source or destination monitor.
                if (!ContextMatches(target, snapshot.Destination) && !ContextMatches(target, snapshot.Display))
                    throw new InvalidOperationException("显示器或工作区域已改变，无法精确恢复；记录已保留，可稍后重试。");
                if (Native.Monitor(snapshot.Display.Handle).Work.Value != snapshot.Display.WorkArea)
                    throw new InvalidOperationException("原显示器工作区域已改变；恢复记录已保留，可稍后重试。");
                var placement = snapshot.Placement;
                if (!IdentityMatches(target)) throw new InvalidOperationException("窗口已关闭或身份变化，已跳过。");
                if (!Native.SetWindowPlacement(target.Handle, ref placement))
                    throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                bool success = WaitFor(target, s => s == snapshot.State);
                if (!success && IdentityMatches(target)) remaining.Add(snapshot);
                outcomes.Add(new(target, success, success ? "已恢复" : "应用限制了恢复，可稍后重试。"));
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                if (IdentityMatches(target)) remaining.Add(snapshot);
                outcomes.Add(new(target, false, e.Message));
            }
        }
        lock (sync) undo = remaining;
        return new(outcomes);
    }

    public WindowStateInfo ReadState(WindowTarget target)
    {
        if (!IdentityMatches(target)) throw new InvalidOperationException("窗口已关闭或身份发生变化。");
        return new(Native.Bounds(target.Handle), Native.IsZoomed(target.Handle));
    }

    public void Dispose()
    {
        lock (sync) { if (disposed) return; disposed = true; known.Clear(); }
        Native.UnhookWinEvent(hook);
        if (eventRoot.IsAllocated) eventRoot.Free();
    }
}
