using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PaneSet;

public partial class App : Application
{
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
        WindowManager? manager = null;
        try
        {
            nint foreground = WindowManager.Foreground;
            manager = new WindowManager();
            var display = WindowManager.DisplayFor(foreground);
            var candidates = manager.ListWindows(display);
            if (!candidates.Any(t => t.Handle == foreground))
            {
                display = WindowManager.DisplayFor(0);
                candidates = manager.ListWindows(display);
            }
            new MainWindow(manager, display, candidates, foreground).Show();
        }
        catch (Exception exception)
        {
            manager?.Dispose();
            MessageBox.Show("无法打开 PaneSet：" + exception.Message, "PaneSet", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
