using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace PaneSet.Checks;

internal static class FixtureHost
{
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [StructLayout(LayoutKind.Sequential)] private struct WindowPos { public nint Window, After; public int X, Y, Width, Height; public uint Flags; }
    private static readonly HashSet<int> blocked = [];
    public static int Run(int count = 3)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        bool linked = false, minimizeLinked = false, closeLinked = false;
        (int X, int Y)? linkedPosition = null;
        var windows = Enumerable.Range(0, count).Select(i => new Window
        {
            Title = i < 3 ? new[] { "采购合同 · 初稿", "采购合同 · 修订稿", "报价清单" }[i] : $"参考资料 {i + 1}",
            Left = 60 + i * 120, Top = 90 + i * 55, Width = 460, Height = 360,
            Content = new TextBlock { Text = "PaneSet 验证窗口\n仅用于排布和恢复验证", Margin = new Thickness(32), FontSize = 18 }
        }).ToArray();
        for (int i = 0; i < windows.Length; i++)
        {
            int index = i;
            windows[i].Show();
            HwndSource.FromHwnd(new WindowInteropHelper(windows[i]).Handle).AddHook((nint h, int m, nint w, nint l, ref bool handled) =>
            {
                if (m == 0x201) windows[index].Title = "PaneSet fixture received a mouse click";
                if (m == 0x46 && blocked.Contains(index))
                {
                    var pos = Marshal.PtrToStructure<WindowPos>(l);
                    pos.Flags |= 1;
                    Marshal.StructureToPtr(pos, l, false);
                }
                if (m == 0x47 && index == 0 && linked)
                {
                    linked = false;
                    if (closeLinked) windows[1].Close();
                    else if (linkedPosition is { } destination) SetWindowPos(new WindowInteropHelper(windows[1]).Handle, 0, destination.X, destination.Y, 0, 0, 0x15);
                    else { windows[1].Left += 80; if (minimizeLinked) windows[1].WindowState = WindowState.Minimized; }
                }
                return 0;
            });
        }
        Console.WriteLine(string.Join(',', windows.Select(w => new WindowInteropHelper(w).Handle.ToInt64())));
        _ = Task.Run(async () =>
        {
            string? line;
            while ((line = await Console.In.ReadLineAsync()) != null)
            {
                string command = line;
                await app.Dispatcher.InvokeAsync(() =>
                {
                    var parts = command.Split(' ');
                    switch (parts[0])
                    {
                        case "untop": windows[int.Parse(parts[1])].Topmost = false; break;
                        case "top": windows[int.Parse(parts[1])].Topmost = true; break;
                        case "position": SetWindowPos(new WindowInteropHelper(windows[int.Parse(parts[1])]).Handle, 0, int.Parse(parts[2]), int.Parse(parts[3]), 0, 0, 0x15); break;
                        case "close": windows[int.Parse(parts[1])].Close(); break;
                        case "max": windows[int.Parse(parts[1])].WindowState = WindowState.Maximized; break;
                        case "min": windows[int.Parse(parts[1])].WindowState = WindowState.Minimized; break;
                        case "block": blocked.Add(int.Parse(parts[1])); break;
                        case "link": linked = true; break;
                        case "linkscreen": linked = true; linkedPosition = (int.Parse(parts[1]), int.Parse(parts[2])); break;
                        case "linkclose": linked = true; closeLinked = true; break;
                        case "linkmin": linked = true; minimizeLinked = true; break;
                        case "unblock": blocked.Clear(); break;
                        case "long": windows[0].Title = string.Concat(Enumerable.Repeat("采购合同修订条款与付款条件对照 ", 24)); break;
                        case "quit": app.Shutdown(); break;
                        case "stall": Console.WriteLine("OK"); Thread.Sleep(1800); return;
                        case "hang": Console.WriteLine("OK"); Thread.Sleep(Timeout.Infinite); return;
                    }
                    Console.WriteLine("OK");
                });
            }
            await app.Dispatcher.InvokeAsync(() => app.Shutdown());
        });
        app.Run();
        return 0;
    }
}
