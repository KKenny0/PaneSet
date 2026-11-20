using Microsoft.Win32;

namespace PaneSet;

/// Per-user autostart via the HKCU Run key -- no elevation, consistent with asInvoker.
/// The command line carries --startup so the boot launch can stay in the tray instead
/// of popping the panel over whatever the user was doing at login.
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PaneSet";

    public static bool IsRegistered(string exePath) =>
        Registry.CurrentUser.OpenSubKey(RunKey, writable: false)?.GetValue(ValueName) is string command
        && command.Contains(exePath, StringComparison.OrdinalIgnoreCase);

    public static string Apply(bool enable, string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key == null) return "无法打开注册表项，开机自启未保存。";
            if (enable) key.SetValue(ValueName, $"\"{exePath}\" --startup");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
            return "";
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { return "开机自启未保存：" + e.Message; }
    }

    /// Current executable path; empty string when unavailable (e.g. single-file host edge cases).
    public static string ExePath() => Environment.ProcessPath ?? "";
}
