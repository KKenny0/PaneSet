using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PaneSet;

internal sealed class PanelShortcuts : IDisposable
{
    private readonly HwndSource source;
    private readonly Action<ShortcutAction, nint> dispatch;
    private readonly string path;
    private readonly Func<ShortcutSettings, string> save;
    private ShortcutSettings settings;
    private readonly Dictionary<ShortcutAction, (int Id, ShortcutBinding? Binding, string Error)> slots = [];
    private readonly HashSet<int> registered = [];
    private int nextId = 100;
    private bool disposed;
    public string Notice { get; private set; }
    public IEnumerable<ShortcutAction> Actions => slots.Keys;
    public string Hint => string.Join(" · ", Actions.Where(a => a is ShortcutAction.OpenPanel or ShortcutAction.Restore or ShortcutAction.PlaceWindow)
        .Select(a => $"{BindingFor(a)?.ToString() ?? "已禁用"} {Label(a)}{(slots[a].Error.Length > 0 ? "不可用（冲突），请用任务栏或面板按钮" : "")}"));
    public static string Label(ShortcutAction action) => action switch {
        ShortcutAction.OpenPanel => "打开 / 收起", ShortcutAction.Restore => "恢复",
        ShortcutAction.ArrangeScreen => "A · 排布本屏全部窗口", ShortcutAction.SelectScreen => "B · 本屏快速选窗",
        ShortcutAction.PlaceWindow => "单窗放置", _ => throw new ArgumentOutOfRangeException(nameof(action)) };
    public ShortcutBinding? BindingFor(ShortcutAction action) => settings.For(action);
    public int RegisteredId(ShortcutAction action) => slots[action].Id;
    public string StatusFor(ShortcutAction action) => slots[action].Error.Length > 0 ? slots[action].Error
        : slots[action].Id == 0 ? "已禁用" : "已生效：" + slots[action].Binding;

    public PanelShortcuts(nint window, Action<ShortcutAction, nint> dispatch, string? settingsPath = null,
        IEnumerable<ShortcutAction>? actions = null, Func<ShortcutSettings, string>? saveSettings = null)
    {
        this.dispatch = dispatch; path = settingsPath ?? ShortcutSettings.DefaultPath;
        save = saveSettings ?? (value => value.Save(path));
        settings = ShortcutSettings.Load(path, out string notice); Notice = notice;
        source = HwndSource.FromHwnd(window); source.AddHook(Receive);
        foreach (var action in actions ?? new[] { ShortcutAction.OpenPanel, ShortcutAction.Restore })
        {
            var binding = settings.For(action); int id = (int)action; string error = "";
            if (binding == null) id = 0;
            else if (Native.RegisterHotKey(window, id, 0x4000 | binding.Modifiers, binding.Key)) registered.Add(id);
            else { id = 0; error = "未生效（注册冲突，错误 " + Marshal.GetLastWin32Error() + "）"; }
            slots[action] = (id, binding, error);
        }
    }

    public string Change(ShortcutAction action, ShortcutBinding? binding)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (binding != null && !binding.Valid) return "请选择修饰键和有效主键；F12 为系统保留键。";
        if (binding != null && Enum.GetValues<ShortcutAction>().Any(a => a != action && settings.For(a) == binding))
            return "该组合已用于其他动作，请使用不同的组合。";
        var old = slots[action];
        if (settings.For(action) == binding && (binding == null || old.Id != 0)) return "";
        int staged = 0;
        if (binding != null)
        {
            if (nextId >= 0xBFFF) return "本次运行修改次数过多，请重新启动。";
            staged = nextId++;
            if (!Native.RegisterHotKey(source.Handle, staged, 0x4000 | binding.Modifiers, binding.Key))
                return "新组合注册失败（错误 " + Marshal.GetLastWin32Error() + "），原绑定保留。";
            registered.Add(staged);
        }
        var next = new ShortcutSettings { Shortcuts = new(settings.Shortcuts), Theme = settings.Theme };
        next.Shortcuts[action] = binding;
        string saveNotice;
        try { saveNotice = save(next); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { return "未保存，原绑定保留。" + e.Message + Release(staged); }
        if (old.Id != 0 && !Native.UnregisterHotKey(source.Handle, old.Id))
        {
            string message = "旧注册未能释放，修改未完成，请重新启动。";
            try { message += save(settings); }
            catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
            { settings = next; message += "保存值为新组合，当前仍持有旧注册；配置回滚失败：" + e.Message; }
            Notice = message + Release(staged); return Notice;
        }
        registered.Remove(old.Id);
        slots[action] = (staged, binding, ""); settings = next; Notice = saveNotice;
        return "";
    }
    public string ChangeTheme(AppearanceMode mode)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Enum.IsDefined(mode)) return "无效的外观选项。";
        if (settings.Theme == mode) return "";
        var next = new ShortcutSettings { Shortcuts = new(settings.Shortcuts), Theme = mode };
        try { Notice = save(next); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { return "外观未保存：" + e.Message; }
        settings = next;
        return "";
    }
    private string Release(int id)
    {
        if (id == 0) return "";
        if (Native.UnregisterHotKey(source.Handle, id)) { registered.Remove(id); return ""; }
        Notice = "临时注册清理失败，请退出后重新启动。"; return Notice;
    }
    private nint Receive(nint window, int message, nint key, nint data, ref bool handled)
    {
        if (message == 0x312 && registered.Contains((int)key))
        {
            handled = true;
            foreach (var pair in slots)
                if (pair.Value.Id == (int)key) { dispatch(pair.Key, WindowManager.Foreground); break; }
        }
        return 0;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        foreach (int id in registered) Native.UnregisterHotKey(source.Handle, id);
        registered.Clear(); source.RemoveHook(Receive);
    }
}
