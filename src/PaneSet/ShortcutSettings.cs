using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace PaneSet;

internal enum ShortcutAction { OpenPanel = 1, Restore = 2, ArrangeScreen = 3, SelectScreen = 4, PlaceWindow = 5 }
internal enum AppearanceMode { System, Light, Dark }
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
    public AppearanceMode Theme { get; set; } = AppearanceMode.System;
    internal static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaneSet", "settings.json");
    public static ShortcutBinding Default(ShortcutAction action) => new(3, action switch {
        ShortcutAction.OpenPanel => 0x20, ShortcutAction.Restore => 0x5A,
        ShortcutAction.ArrangeScreen => 0x31, ShortcutAction.SelectScreen => 0x32, ShortcutAction.PlaceWindow => 0x33,
        _ => throw new ArgumentOutOfRangeException(nameof(action)) });
    public ShortcutBinding? For(ShortcutAction action) => Shortcuts.TryGetValue(action, out var value) ? value : Default(action);
    public static ShortcutSettings Load(string path, out string notice)
    {
        notice = "";
        try
        {
            if (!File.Exists(path)) return new();
            var settings = JsonSerializer.Deserialize<ShortcutSettings>(File.ReadAllText(path), JsonOptions);
            if (settings == null || settings.Version != 1 || settings.Shortcuts == null || !Enum.IsDefined(settings.Theme)
                || settings.Shortcuts.Values.Any(b => b != null && !b.Valid)) throw new InvalidDataException("配置格式或版本无效");
            if (!settings.Shortcuts.ContainsKey(ShortcutAction.PlaceWindow)
                && Enum.GetValues<ShortcutAction>().Any(a => a != ShortcutAction.PlaceWindow && settings.For(a) == Default(ShortcutAction.PlaceWindow)))
            {
                settings.Shortcuts[ShortcutAction.PlaceWindow] = null;
                notice = "单窗放置默认组合已用于旧动作，已暂时禁用单窗放置快捷键；可在设置中改绑，原绑定与外观保留。";
            }
            var bindings = Enum.GetValues<ShortcutAction>().Select(settings.For).Where(b => b != null).ToArray();
            if (bindings.Distinct().Count() != bindings.Length) throw new InvalidDataException("配置包含重复组合");
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidDataException)
        { notice = "无法读取快捷键配置，正在使用默认值；主动保存后才覆盖原文件。" + e.Message; return new(); }
    }
    // A return value is a cleanup notice after commit; failures throw.
    public string Save(string path) => Save(path, ShortcutSettingsFiles.Instance);
    internal string Save(string path, IShortcutSettingsFiles files)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string backup = temporary + ".bak";
        bool committed = false, hasBackup = false;
        Exception? failure = null;
        string notice = "";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, this, JsonOptions); file.Flush(true); }
            if (File.Exists(full))
            {
                // An unused backup name avoids replacing the internally created ~RF destination.
                if (File.Exists(backup) || Directory.Exists(backup)) throw new IOException("配置备份路径已被占用：" + backup);
                try { files.Replace(temporary, full, backup); hasBackup = true; }
                catch (IOException e) when ((e.HResult & 0xffff) == 1177)
                {
                    try { files.Move(backup, full); }
                    catch (Exception recovery) when (recovery is IOException or UnauthorizedAccessException)
                    {
                        throw new IOException("保存失败：" + e.Message + "；原配置未能恢复，保留于：" + backup + "。" + recovery.Message,
                            new AggregateException(e, recovery)) { HResult = e.HResult };
                    }
                    throw;
                }
            }
            else files.Move(temporary, full);
            committed = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        { failure = e; throw; }
        finally
        {
            try { if (File.Exists(temporary)) files.Delete(temporary); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                string message = "临时配置未能清理：" + temporary + "。" + cleanup.Message;
                if (failure != null)
                    throw new IOException(failure.Message + "；" + message, new AggregateException(failure, cleanup)) { HResult = failure.HResult };
                if (!committed) throw;
                notice = message;
            }
        }
        if (hasBackup)
        {
            try { files.Delete(backup); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { notice += "旧配置备份未能清理：" + backup + "。" + e.Message; }
        }
        return notice;
    }
}

internal interface IShortcutSettingsFiles
{
    void Replace(string source, string destination, string backup);
    void Move(string source, string destination);
    void Delete(string path);
}

internal sealed class ShortcutSettingsFiles : IShortcutSettingsFiles
{
    public static readonly ShortcutSettingsFiles Instance = new();
    public void Replace(string source, string destination, string backup) => File.Replace(source, destination, backup);
    public void Move(string source, string destination) => File.Move(source, destination);
    public void Delete(string path) => File.Delete(path);
}
