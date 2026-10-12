using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace PaneSet;

internal enum ShortcutAction { OpenPanel = 1, Restore = 2, ArrangeScreen = 3, SelectScreen = 4 }
internal sealed record ShortcutBinding(uint Modifiers, uint Key)
{
    public bool Valid => Modifiers is > 0 and <= 15 && Key is > 0 and <= 255
        && Key != 0x7B && Key is not (0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5)
        && System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)Key) is not (System.Windows.Input.Key.None or System.Windows.Input.Key.System);
    public override string ToString() => string.Join("+", new[] {
        (Modifiers & 2) != 0 ? "Ctrl" : null, (Modifiers & 1) != 0 ? "Alt" : null,
        (Modifiers & 4) != 0 ? "Shift" : null, (Modifiers & 8) != 0 ? "Win" : null,
        Key == 0x20 ? "Space" : Key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A ? ((char)Key).ToString() : KeyInterop.KeyFromVirtualKey((int)Key).ToString()
    }.Where(s => s != null));
}

internal sealed class ShortcutSettings
{
    public int Version { get; set; } = 1;
    public Dictionary<ShortcutAction, ShortcutBinding?> Shortcuts { get; set; } = [];
    internal static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaneSet", "settings.json");
    public static ShortcutBinding Default(ShortcutAction action) => new(3, action switch {
        ShortcutAction.OpenPanel => 0x20, ShortcutAction.Restore => 0x5A,
        ShortcutAction.ArrangeScreen => 0x31, ShortcutAction.SelectScreen => 0x32, _ => throw new ArgumentOutOfRangeException(nameof(action)) });
    public ShortcutBinding? For(ShortcutAction action) => Shortcuts.TryGetValue(action, out var value) ? value : Default(action);
    public static ShortcutSettings Load(string path, out string notice)
    {
        notice = "";
        try
        {
            if (!File.Exists(path)) return new();
            var settings = JsonSerializer.Deserialize<ShortcutSettings>(File.ReadAllText(path), JsonOptions);
            if (settings == null || settings.Version != 1 || settings.Shortcuts == null
                || settings.Shortcuts.Values.Any(b => b != null && !b.Valid)) throw new InvalidDataException("配置格式或版本无效");
            var bindings = Enum.GetValues<ShortcutAction>().Select(settings.For).Where(b => b != null).ToArray();
            if (bindings.Distinct().Count() != bindings.Length) throw new InvalidDataException("配置包含重复组合");
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        { notice = "无法读取快捷键配置，正在使用默认值；主动保存后才覆盖原文件。" + e.Message; return new(); }
    }
    public void Save(string path)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, this, JsonOptions); file.Flush(true); }
            if (File.Exists(full)) File.Replace(temporary, full, null); else File.Move(temporary, full);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
