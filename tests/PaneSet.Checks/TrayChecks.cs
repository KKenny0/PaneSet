using System.Diagnostics;
using System.Windows;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckTrayPresence()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = manager.ListWindows(display).Where(t => fixture.Handles.Contains(t.Handle)).ToArray();
        var window = new MainWindow(manager, display, candidates, fixture.Handles[0],
            enableShortcuts: false, enableTray: true);
        window.Show();
        await Until(() => window.IsVisible && window.IsLoaded);
        try
        {
            // Collapsing hides the window completely; with a tray icon there is no taskbar button.
            Invoke(ButtonNamed(window, "收起"));
            await Until(() => !window.IsVisible);
            await Task.Delay(60);
            window.OpenForForeground(WindowManager.Foreground, toggle: false);
            await Until(() => window.IsVisible);

            // The system close has the same effect as collapsing: hidden, process alive.
            window.Close();
            await Until(() => !window.IsVisible);
            Equal(true, window.IsLoaded); // close was intercepted: process entry point remains alive
            window.OpenForForeground(WindowManager.Foreground, toggle: false);
            await Until(() => window.IsVisible);

            // A caption minimize also collapses to the tray instead of parking on the taskbar.
            Invoke(ButtonNamed(window, "最小化"));
            await Until(() => !window.IsVisible);
            window.OpenForForeground(WindowManager.Foreground, toggle: false);
            await Until(() => window.IsVisible);

            // Explicit exit really closes and releases the tray icon.
            window.Exit();
            await Until(() => !window.IsLoaded);
        }
        finally { if (window.IsLoaded) window.Exit(); }

        // Without a tray icon the old behavior remains: collapse minimizes to the taskbar.
        var fallback = new MainWindow(manager, display, candidates, fixture.Handles[0],
            enableShortcuts: false, enableTray: false);
        fallback.Show();
        await Until(() => fallback.IsVisible && fallback.IsLoaded);
        try
        {
            Invoke(ButtonNamed(fallback, "收起"));
            await Until(() => fallback.WindowState == WindowState.Minimized);
        }
        finally { fallback.Exit(); }
    }
}
