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
