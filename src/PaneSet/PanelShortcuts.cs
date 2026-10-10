using System.Windows.Interop;

namespace PaneSet;

internal sealed class PanelShortcuts : IDisposable
{
    private readonly HwndSource source;
    private readonly Action<nint> open;
    private readonly Action undo;
    private readonly List<int> registered = [];
    public string Hint { get; }

    public PanelShortcuts(nint window, Action<nint> open, Action undo)
    {
        this.open = open; this.undo = undo;
        source = HwndSource.FromHwnd(window);
        source.AddHook(Receive);
        var hints = new List<string>();
        foreach (var (id, key, label) in new[] { (1, 0x20u, "Ctrl+Alt+Space 打开 / 收起"), (2, 0x5Au, "Ctrl+Alt+Z 恢复") })
        {
            if (Native.RegisterHotKey(window, id, 0x4003, key)) { registered.Add(id); hints.Add(label); }
            else hints.Add(label + "不可用（冲突），请用任务栏或面板按钮");
        }
        Hint = string.Join(" · ", hints);
    }

    private nint Receive(nint window, int message, nint key, nint data, ref bool handled)
    {
        if (message == 0x312 && registered.Contains((int)key))
        {
            handled = true;
            if (key == 1) open(WindowManager.Foreground); else undo();
        }
        return 0;
    }

    public void Dispose()
    {
        foreach (int id in registered) Native.UnregisterHotKey(source.Handle, id);
        registered.Clear();
        source.RemoveHook(Receive);
    }
}
