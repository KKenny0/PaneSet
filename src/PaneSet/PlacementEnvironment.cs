using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PaneSet;

public sealed record PlacementDisplay(DisplayArea Display, string Device, WindowRect Bounds, uint Dpi, long Configuration);
public sealed record LayoutPositions(long Version, PlacementDisplay Environment, Guid DesktopId, IReadOnlyList<WindowRect> Regions)
{
    public DisplayArea Display => Environment.Display;
}

// Native observations are kept at this boundary so failure checks need not alter the user's desktop.
internal class PlacementEnvironment : IDisposable
{
    private readonly IVirtualDesktopManager desktop;
    private readonly HwndSource probe;
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private long configuration;
    private bool disposed;

    public PlacementEnvironment()
    {
        desktop = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A"))!)!;
        probe = new HwndSource(new HwndSourceParameters("PaneSet display sampling")
        { Width = 1, Height = 1, WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x08000080 });
        probe.AddHook(Receive);
    }

    private nint Receive(nint window, int message, nint key, nint data, ref bool handled)
    {
        if (message == 0x7E || (message == 0x1A && key == 0x2F)) Interlocked.Increment(ref configuration);
        return 0;
    }

    public virtual bool IsCurrent(nint window) => desktop.IsWindowOnCurrentVirtualDesktop(window, out bool current) == 0 && current;
    public virtual Guid DesktopFor(nint window)
    {
        if (!IsCurrent(window) || desktop.GetWindowDesktopId(window, out var id) != 0 || id == Guid.Empty)
            throw new InvalidOperationException("无法确认当前虚拟桌面，请回到原桌面后重试。");
        return id;
    }
    public virtual WindowStateInfo ReadState(nint window) => new(Native.Bounds(window), Native.IsZoomed(window));
    public virtual bool IsForeground(nint window) => Native.GetForegroundWindow() == window;
    public virtual bool Activate(nint window)
    {
        if (IsForeground(window)) return true;
        bool requested = Native.SetForegroundWindow(window);
        for (int i = 0; i < (requested ? 20 : 1); i++)
        {
            if (IsForeground(window)) return true;
            if (requested) Thread.Sleep(25);
        }
        return false;
    }
    public virtual PlacementDisplay Capture(DisplayArea display) => dispatcher.CheckAccess()
        ? ReadDisplay(display) : dispatcher.Invoke(() => ReadDisplay(display));
    private PlacementDisplay ReadDisplay(DisplayArea display)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        long version = Interlocked.Read(ref configuration);
        var info = Native.MonitorExtended(display.Handle);
        if (Native.GetAwarenessFromDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(probe.Handle)) != 2)
            throw new InvalidOperationException("无法可靠读取显示器缩放，请重新启动 PaneSet。");
        if (!Native.SetWindowPos(probe.Handle, 0, info.Work.Left, info.Work.Top, 1, 1, 0x14)
            || Native.MonitorFromWindow(probe.Handle, 0) != display.Handle)
            throw new InvalidOperationException("无法读取目标显示器缩放，请重新排布。");
        uint dpi = Native.GetDpiForWindow(probe.Handle);
        var after = Native.MonitorExtended(display.Handle);
        if (dpi == 0 || version != Interlocked.Read(ref configuration) || info.Device != after.Device
            || info.Monitor.Value != after.Monitor.Value || info.Work.Value != after.Work.Value)
            throw new InvalidOperationException("显示环境正在变化，请重新排布。");
        return new(new(display.Handle, info.Work.Value) { Name = display.Name }, info.Device, info.Monitor.Value, dpi, version);
    }
    public void Dispose()
    {
        if (!dispatcher.CheckAccess()) { dispatcher.Invoke(Dispose); return; }
        if (disposed) return;
        disposed = true; probe.RemoveHook(Receive); probe.Dispose();
    }
}
