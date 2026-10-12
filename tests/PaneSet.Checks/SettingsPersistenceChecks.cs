using System.IO;
using System.Windows.Interop;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckSettingsReplace()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PaneSet.Settings-replace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.json");
        try
        {
            var settings = new ShortcutSettings { Shortcuts = new() {
                [ShortcutAction.OpenPanel] = new(3, 0x41), [ShortcutAction.Restore] = null,
                [ShortcutAction.ArrangeScreen] = null, [ShortcutAction.SelectScreen] = null } };
            settings.Save(path);
            using var reader = new SettingsDestinationReader(path);
            // Calibrate the observer before relying on a timing-dependent regression.
            string calibration = path + "~RF-calibration.TMP";
            File.WriteAllText(calibration, "calibration");
            await Until(() => reader.Opened > 0);
            reader.Release(); File.Delete(calibration);
            int calibrationOpens = reader.Opened;
            for (int i = 0; i < 200; i++)
            {
                var binding = new ShortcutBinding(3, (uint)(0x41 + i % 26));
                settings.Shortcuts[ShortcutAction.OpenPanel] = binding;
                try { settings.Save(path); }
                catch (IOException e)
                {
                    throw new Exception($"Real Save failed at iteration={i}, Win32={e.HResult & 0xffff}, observedRFopens={reader.Opened - calibrationOpens}", e);
                }
                finally { reader.Release(); }
                Equal(binding, ShortcutSettings.Load(path, out string notice).For(ShortcutAction.OpenPanel));
                Equal("", notice);
            }
            Console.WriteLine($"Real Save/Load replacements=200, observedRFopens={reader.Opened - calibrationOpens}");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static Task CheckSettingsCleanup()
    {
        using var files = new SettingsFaultFiles { HoldBackup = true };
        using var fixture = new SettingsShortcutFixture(files);
        try
        {
            Equal("", fixture.Shortcuts.Change(ShortcutAction.OpenPanel, fixture.NewBinding));
            fixture.AssertBinding(fixture.NewBinding);
            Equal(true, fixture.Shortcuts.Notice.Contains("清理"));
            Equal(true, fixture.Shortcuts.Notice.Contains(files.Backup!));
            Equal(fixture.OldBinding, ShortcutSettings.Load(files.Backup!, out _).For(ShortcutAction.OpenPanel));
        }
        finally { files.Dispose(); }
        return Task.CompletedTask;
    }

    private static Task CheckSettingsRecovery()
    {
        using var files = new SettingsFaultFiles { ReplaceError = 1177 };
        using var fixture = new SettingsShortcutFixture(files);
        string error = fixture.Shortcuts.Change(ShortcutAction.OpenPanel, fixture.NewBinding);
        Equal(true, error.StartsWith("未保存，原绑定保留。"));
        Equal(true, error.Contains("1177"));
        fixture.AssertBinding(fixture.OldBinding);
        Equal(false, File.Exists(files.Backup));
        Equal(1, Directory.GetFiles(fixture.DirectoryPath).Length);
        return Task.CompletedTask;
    }

    private static Task CheckSettingsRecoveryBlocked()
    {
        foreach (bool external in new[] { false, true })
        {
            using var files = new SettingsFaultFiles { ReplaceError = 1177, HoldBackup = !external, RecreateDestination = external };
            using var fixture = new SettingsShortcutFixture(files);
            try
            {
                string error = fixture.Shortcuts.Change(ShortcutAction.OpenPanel, fixture.NewBinding);
                Equal(true, error.Contains("1177"));
                Equal(true, error.Contains("原配置未能恢复"));
                Equal(true, error.Contains(files.Backup!));
                Equal(fixture.OldBinding, fixture.Shortcuts.BindingFor(ShortcutAction.OpenPanel));
                fixture.AssertRegistrations(fixture.OldBinding);
                Equal(fixture.OldBinding, ShortcutSettings.Load(files.Backup!, out _).For(ShortcutAction.OpenPanel));
                Equal(external, File.Exists(fixture.Path));
                if (external) Equal("external", File.ReadAllText(fixture.Path));
            }
            finally { files.Dispose(); }
        }
        return Task.CompletedTask;
    }

    private static Task CheckSettingsSaveFailures()
    {
        using (var files = new SettingsFaultFiles { ReplaceError = 1176 })
        using (var fixture = new SettingsShortcutFixture(files))
        {
            Equal(true, fixture.Shortcuts.Change(ShortcutAction.OpenPanel, fixture.NewBinding).Contains("1176"));
            fixture.AssertBinding(fixture.OldBinding);
            Equal(1, Directory.GetFiles(fixture.DirectoryPath).Length);
        }
        using (var fixture = new SettingsShortcutFixture())
        {
            using (var held = new FileStream(fixture.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Equal(true, fixture.Shortcuts.Change(ShortcutAction.OpenPanel, fixture.NewBinding).StartsWith("未保存，原绑定保留。"));
            fixture.AssertBinding(fixture.OldBinding);
            Equal(1, Directory.GetFiles(fixture.DirectoryPath).Length);
        }
        return Task.CompletedTask;
    }

    private static Task CheckSettingsTemporaryCleanup()
    {
        using var files = new SettingsFaultFiles { ReplaceError = 1177, HoldBackup = true, HoldTemporary = true };
        using var fixture = new SettingsShortcutFixture(files);
        try
        {
            string error = fixture.Shortcuts.Change(ShortcutAction.OpenPanel, fixture.NewBinding);
            Equal(true, error.Contains("1177"));
            Equal(true, error.Contains(files.Backup!));
            Equal(true, error.Contains("清理"));
            Equal(1177, fixture.SaveError!.HResult & 0xffff);
            Equal(true, fixture.SaveError.InnerException is AggregateException);
            Equal(false, File.Exists(fixture.Path));
            Equal(true, File.Exists(files.Temporary));
            Equal(fixture.OldBinding, fixture.Shortcuts.BindingFor(ShortcutAction.OpenPanel));
            fixture.AssertRegistrations(fixture.OldBinding);
            Equal(fixture.OldBinding, ShortcutSettings.Load(files.Backup!, out _).For(ShortcutAction.OpenPanel));
        }
        finally { files.Dispose(); }
        return Task.CompletedTask;
    }

    private static Task CheckSettingsBindingReplace()
    {
        using var fixture = new SettingsShortcutFixture();
        for (int i = 0; i < 100; i++)
        {
            var next = i % 2 == 0 ? fixture.NewBinding : fixture.OldBinding;
            Equal("", fixture.Shortcuts.Change(ShortcutAction.OpenPanel, next));
            fixture.AssertBinding(next);
        }
        Equal(1, Directory.GetFiles(fixture.DirectoryPath).Length);
        return Task.CompletedTask;
    }

    private sealed class SettingsShortcutFixture : IDisposable
    {
        public readonly string DirectoryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PaneSet.Settings-transaction-" + Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(DirectoryPath, "settings.json");
        public readonly ShortcutBinding OldBinding = new(7, 0x7C);
        public readonly ShortcutBinding NewBinding = new(7, 0x7D);
        private readonly HwndSource source = new(new HwndSourceParameters("PaneSet settings owner") { ParentWindow = new nint(-3) });
        private readonly HwndSource probe = new(new HwndSourceParameters("PaneSet settings probe") { ParentWindow = new nint(-3) });
        private PanelShortcuts? shortcuts;
        public IOException? SaveError;
        public PanelShortcuts Shortcuts => shortcuts!;
        public SettingsShortcutFixture(IShortcutSettingsFiles? files = null)
        {
            try
            {
                var settings = new ShortcutSettings { Shortcuts = new() {
                    [ShortcutAction.OpenPanel] = OldBinding, [ShortcutAction.Restore] = null,
                    [ShortcutAction.ArrangeScreen] = null, [ShortcutAction.SelectScreen] = null } };
                settings.Save(Path);
                if (!Native.RegisterHotKey(probe.Handle, 10, 0x4000 | NewBinding.Modifiers, NewBinding.Key))
                    throw new UnavailableException("Owned settings test requires Ctrl+Alt+Shift+F14 to be available.");
                Equal(true, Native.UnregisterHotKey(probe.Handle, 10));
                shortcuts = new(source.Handle, (_, _) => { }, Path, [ShortcutAction.OpenPanel],
                    files == null ? null : value =>
                    {
                        try { return value.Save(Path, files); }
                        catch (IOException e) { SaveError = e; throw; }
                    });
                if (shortcuts.RegisteredId(ShortcutAction.OpenPanel) == 0)
                    throw new UnavailableException("Owned settings test requires Ctrl+Alt+Shift+F13 to be available.");
            }
            catch { Dispose(); throw; }
        }
        public void AssertBinding(ShortcutBinding binding)
        {
            Equal(binding, Shortcuts.BindingFor(ShortcutAction.OpenPanel));
            Equal(binding, ShortcutSettings.Load(Path, out string notice).For(ShortcutAction.OpenPanel));
            Equal("", notice);
            AssertRegistrations(binding);
        }
        public void AssertRegistrations(ShortcutBinding binding)
        {
            var freed = binding == NewBinding ? OldBinding : NewBinding;
            Equal(true, Native.RegisterHotKey(probe.Handle, 10, 0x4000 | freed.Modifiers, freed.Key));
            Equal(true, Native.UnregisterHotKey(probe.Handle, 10));
            Equal(false, Native.RegisterHotKey(probe.Handle, 10, 0x4000 | binding.Modifiers, binding.Key));
        }
        public void Dispose()
        {
            shortcuts?.Dispose(); Native.UnregisterHotKey(probe.Handle, 10); source.Dispose(); probe.Dispose();
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
        }
    }

    private sealed class SettingsFaultFiles : IShortcutSettingsFiles, IDisposable
    {
        public bool HoldBackup;
        public int ReplaceError;
        public bool RecreateDestination;
        public bool HoldTemporary;
        public string? Backup;
        public string? Temporary;
        private readonly List<FileStream> held = [];
        public void Replace(string source, string destination, string backup)
        {
            Backup = backup; Temporary = source;
            if (ReplaceError != 0)
            {
                // Simulate the documented disk state as well as the HRESULT, not just an exception.
                if (ReplaceError == 1177)
                {
                    File.Move(destination, backup);
                    if (HoldBackup) held.Add(new(backup, FileMode.Open, FileAccess.Read, FileShare.Read));
                    if (RecreateDestination) File.WriteAllText(destination, "external");
                    if (HoldTemporary) held.Add(new(source, FileMode.Open, FileAccess.Read, FileShare.Read));
                }
                throw new IOException("Injected ReplaceFileW " + ReplaceError, unchecked((int)(0x80070000u | (uint)ReplaceError)));
            }
            File.Replace(source, destination, backup);
            if (HoldBackup) held.Add(new(backup, FileMode.Open, FileAccess.Read, FileShare.Read));
        }
        public void Move(string source, string destination) => File.Move(source, destination);
        public void Delete(string path) => File.Delete(path);
        public void Dispose() { foreach (var file in held) file.Dispose(); held.Clear(); }
    }

    private sealed class SettingsDestinationReader : IDisposable
    {
        private readonly FileSystemWatcher watcher;
        private readonly string path;
        private readonly List<FileStream> held = [];
        private bool disposed;
        private int opened;
        public int Opened => Volatile.Read(ref opened);
        public SettingsDestinationReader(string path)
        {
            this.path = path;
            watcher = new(Path.GetDirectoryName(path)!) { NotifyFilter = NotifyFilters.FileName };
            watcher.Created += (_, e) => Open(e.FullPath);
            watcher.EnableRaisingEvents = true;
        }
        private void Open(string candidate)
        {
            if (!candidate.StartsWith(path + "~RF", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                var file = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                lock (held)
                {
                    if (disposed) file.Dispose();
                    else { held.Add(file); Interlocked.Increment(ref opened); }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // The notified file may already be unavailable.
        }
        public void Release()
        {
            lock (held) { foreach (var file in held) file.Dispose(); held.Clear(); }
        }
        public void Dispose()
        {
            watcher.Dispose();
            lock (held) { disposed = true; Release(); }
        }
    }
}
