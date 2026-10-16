using System.Runtime.InteropServices;
using System.Text;

namespace PaneSet;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public readonly WindowRect Value => new(Left, Top, Right - Left, Bottom - Top); }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct Placement { public int Length, Flags, Show; public Point Min, Max; public Rect Normal; }
    internal delegate bool EnumProc(nint window, nint param);
    internal delegate void EventProc(nint hook, uint evt, nint window, int obj, int child, uint thread, uint time);
    internal delegate bool MonitorProc(nint monitor, nint device, nint rect, nint param);
    [DllImport("user32.dll")] internal static extern bool EnumDisplayMonitors(nint device, nint clip, MonitorProc callback, nint param);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, nint param);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(nint window);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(nint window, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint window, StringBuilder text, int max);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLong(nint window, int index);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] internal static extern int Frame(nint window, int attr, out Rect rect, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] internal static extern int Cloaked(nint window, int attr, out int value, int size);
    [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll")] internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] internal static extern nint GetTopWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(nint window);
    [DllImport("user32.dll")] internal static extern bool EnableWindow(nint window, bool enabled);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetProp(nint window, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool SetProp(nint window, string name, nint value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint window, int show);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWinEventHook(uint first, uint last, nint module, EventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(nint hook);

    [StructLayout(LayoutKind.Sequential)] internal struct Size { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] internal struct ThumbnailProperties
    {
        public uint Flags;
        public Rect Destination, Source;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool SourceClientAreaOnly;
    }
    [DllImport("dwmapi.dll")] internal static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] internal static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out Size size);
    [DllImport("dwmapi.dll")] internal static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref ThumbnailProperties properties);
    [DllImport("dwmapi.dll")] internal static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
    internal const string PlacementFeedbackProperty = "PaneSet.PointerFeedback";
    internal static nint PointerWindow(Point point)
    {
        nint root = GetAncestor(WindowFromPoint(point), 2);
        if (root == 0 || !IsWindowEnabled(root)) return 0;
        var visited = new HashSet<nint>();
        for (nint window = GetTopWindow(0); window != 0 && visited.Count < 4096; window = GetWindow(window, 2))
        {
            if (!visited.Add(window)) return 0;
            if (!IsWindowVisible(window) || IsIconic(window)) continue;
            GetWindowThreadProcessId(window, out uint pid);
            if (pid == Environment.ProcessId && GetProp(window, PlacementFeedbackProperty) == 1) continue;
            if (Cloaked(window, 14, out int cloaked, 4) != 0) return 0;
            if (cloaked != 0) continue;
            if (!GetWindowRect(window, out var bounds)) return 0;
            bool contains = point.X >= bounds.Left && point.Y >= bounds.Top && point.X < bounds.Right && point.Y < bounds.Bottom;
            if (window == root)
                return contains && IsWindowEnabled(root) && GetAncestor(WindowFromPoint(point), 2) == root ? root : 0;
            // Rectangles conservatively block uncertain shaped or transparent covering surfaces.
            if (contains) return 0;
        }
        return 0;
    }

    internal static WindowRect Bounds(nint window)
    {
        if (Frame(window, 9, out var visible, 16) == 0) return visible.Value;
        if (GetWindowRect(window, out var outer)) return outer.Value;
        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    internal static string Text(nint window, bool className = false)
    {
        var value = new StringBuilder(1024);
        if (className) GetClassName(window, value, value.Capacity); else GetWindowText(window, value, value.Capacity);
        return value.ToString();
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct MonitorInfoExtended
    {
        public int Size; public Rect Monitor, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfoExtended(nint monitor, ref MonitorInfoExtended info);
    [DllImport("user32.dll")] internal static extern nint GetWindowDpiAwarenessContext(nint window);
    [DllImport("user32.dll")] internal static extern int GetAwarenessFromDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    internal static MonitorInfoExtended MonitorExtended(nint handle)
    {
        var info = new MonitorInfoExtended { Size = Marshal.SizeOf<MonitorInfoExtended>(), Device = "" };
        if (!GetMonitorInfoExtended(handle, ref info)) throw new InvalidOperationException("显示器已不可用，请重新排布。");
        return info;
    }
    internal static MonitorInfo Monitor(nint handle)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(handle, ref info)) throw new InvalidOperationException("显示器已不可用，请刷新窗口。");
        return info;
    }
}

[ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopManager
{
    [PreserveSig] int IsWindowOnCurrentVirtualDesktop(nint window, [MarshalAs(UnmanagedType.Bool)] out bool current);
    [PreserveSig] int GetWindowDesktopId(nint window, out Guid id);
    [PreserveSig] int MoveWindowToDesktop(nint window, ref Guid id);
}
