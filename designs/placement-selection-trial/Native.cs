using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SelectionTrial;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect : IEquatable<Rect>
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
        public readonly bool Contains(Point p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;
        public readonly bool Equals(Rect other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        public override readonly bool Equals(object? obj) => obj is Rect other && Equals(other);
        public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
        public readonly Point Center => new(Left + Width / 2, Top + Height / 2);
    }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] internal static extern nint GetTopWindow(nint window);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(nint window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetStyle(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetStyle(nint window, int index, nint value);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] private static extern int Cloaked(nint window, int attribute, out int value, int size);

    internal static nint Handle(Window window) => new WindowInteropHelper(window).Handle;
    internal static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    internal static bool AnyButton => Down(1) || Down(2) || Down(4) || Down(5) || Down(6);
    internal static Rect Bounds(Window window) => GetWindowRect(Handle(window), out var rect) ? rect : throw new InvalidOperationException("无法读取试验窗口位置。");
    internal static MonitorInfo Environment(Window window)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(Handle(window), 2), ref info)) throw new InvalidOperationException("无法读取屏幕。");
        return info;
    }
    internal static void NoActivate(Window window)
    {
        var handle = Handle(window);
        SetStyle(handle, -20, GetStyle(handle, -20) | 0x08000000);
    }
    internal static void MoveOwned(Fixture window, Rect rect)
    {
        var handle = Handle(window);
        GetWindowThreadProcessId(handle, out uint pid);
        if (window.IsClosed || pid != System.Environment.ProcessId) throw new InvalidOperationException("仅允许移动本次试验创建的窗口。");
        window.WindowState = WindowState.Normal;
        if (!SetWindowPos(handle, 0, rect.Left, rect.Top, rect.Width, rect.Height, 0x14)) throw new InvalidOperationException("试验窗口未接受移动。");
    }
    // Fixture-only: all eligible surfaces are known rectangular WPF windows. An unknown
    // covering window blocks selection. This does not implement production hit testing.
    internal static Fixture? Pick(Trial trial, SelectionOverlay overlay, Point point)
    {
        var seen = new HashSet<nint>();
        for (nint handle = GetTopWindow(0); handle != 0 && seen.Count < 4096; handle = GetWindow(handle, 2))
        {
            if (!seen.Add(handle)) return null;
            if (handle == Handle(overlay) || !IsWindowVisible(handle) || IsIconic(handle)) continue;
            if (Cloaked(handle, 14, out int cloaked, 4) != 0) return null;
            if (cloaked != 0) continue;
            if (!GetWindowRect(handle, out var rect)) return null;
            if (!rect.Contains(point)) continue;
            GetWindowThreadProcessId(handle, out uint pid);
            if (pid != System.Environment.ProcessId || !IsWindowEnabled(handle)) return null;
            var target = trial.Fixtures.FirstOrDefault(f => !f.IsClosed && Handle(f) == handle);
            if (target == null || target == overlay.Source) return null;
            var work = overlay.Screen.Work;
            return rect.Width >= 240 && rect.Height >= 160 && rect.Left >= work.Left && rect.Top >= work.Top
                && rect.Right <= work.Right && rect.Bottom <= work.Bottom ? target : null;
        }
        return null;
    }
}
