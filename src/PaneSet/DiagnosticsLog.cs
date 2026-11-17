using System.IO;
using System.Text;

namespace PaneSet;

/// Best-effort append-only log for unexpected exceptions. Lives beside settings.json so
/// trial users can find one folder; rotated to .old beyond 256 KB. Never throws: logging
/// failures are swallowed by design -- this is the crash path, not a feature.
internal static class DiagnosticsLog
{
    private const long Limit = 256 * 1024;
    private static readonly object sync = new();
    private static string? resolved;

    public static void Write(string settingsPath, string context, Exception exception)
    {
        try
        {
            lock (sync)
            {
                resolved ??= Path.Combine(Path.GetDirectoryName(settingsPath) ?? ".", "error.log");
                var info = new FileInfo(resolved);
                if (info.Exists && info.Length > Limit) File.Move(resolved, resolved + ".old", true);
                File.AppendAllText(resolved,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {context}\n{exception}\n\n", Encoding.UTF8);
            }
        }
        catch { /* best effort by design */ }
    }
}
