using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PaneSet;

public partial class App : Application
{
    private Mutex? instance;
    private EventWaitHandle? reopen;
    private DispatcherTimer? reopenTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Velopack lifecycle hook (install/update/uninstall bookkeeping); must precede all
        // other startup work. Inert when the app is not Velopack-installed (e.g. repo builds).
        Velopack.VelopackApp.Build().Run();
        base.OnStartup(e);
        if (e.Args.Contains("--probe"))
        {
            var probe = new Window { Title = "PaneSet", Width = 640, Height = 480 };
            probe.Show();
            if (new WindowInteropHelper(probe).Handle == 0) throw new InvalidOperationException("WPF window was not created.");
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => Shutdown(0)));
            return;
        }
        reopen = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\PaneSet.Reopen");
        instance = new Mutex(true, "Local\\PaneSet.Instance", out bool first);
        if (!first) { reopen.Set(); Shutdown(0); return; }
        // --startup comes from the autostart Run key: skip the foreground-capture dance and
        // start minimized to the tray so a login launch never steals focus from the user's
        // session. Falls back to a hidden panel when no tray icon is available.
        bool startupLaunch = e.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase);
        // Last-resort handler for exceptions that escape the UI thread (timer ticks, routed
        // events, async void continuations). Without this a single stray exception kills the
        // process: tray gone, restore records gone, drag placement silently dead.
        DispatcherUnhandledException += (_, e) =>
        {
            DiagnosticsLog.Write(ShortcutSettings.DefaultPath, "ui-unhandled", e.Exception);
            // Keep the process alive whenever the main window still exists: without a live
            // tray process the global hotkeys and the restore records are silently gone.
            // The balloon is best-effort; being rate-limited it cannot spam on cascades.
            if (MainWindow is MainWindow { ShutdownSafe: true } main)
            {
                main.TrayNotify("PaneSet 遇到错误已自动恢复，可继续使用；详情见日志。");
                e.Handled = true;
            }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            DiagnosticsLog.Write(ShortcutSettings.DefaultPath, "domain-unhandled", (Exception)e.ExceptionObject);
        WindowManager? manager = null;
        try
        {
            nint foreground = WindowManager.Foreground;
            manager = new WindowManager();
            var display = WindowManager.DisplayFor(foreground);
            var candidates = manager.ListWindows();
            if (!candidates.Any(t => t.Handle == foreground)) display = WindowManager.DisplayFor(0);
            var window = new MainWindow(manager, display, candidates, foreground);
            window.Show();
            if (startupLaunch)
                // SourceInitialized has run (tray created); collapsing right after Show keeps
                // the HWND alive without a visible flash of the panel at login.
                window.StartupCollapse();
            reopenTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Normal,
                (_, _) => { if (reopen.WaitOne(0)) window.OpenForForeground(WindowManager.Foreground, toggle: false); }, Dispatcher);
        }
        catch (Exception exception)
        {
            manager?.Dispose();
            MessageBox.Show("无法打开 PaneSet：" + exception.Message, "PaneSet", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        reopenTimer?.Stop(); reopen?.Dispose(); instance?.Dispose();
        base.OnExit(e);
    }
}
