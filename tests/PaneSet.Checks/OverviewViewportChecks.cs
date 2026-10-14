using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    // Only the owned fixture preparation click; no keyboard input or layout execution.
    private static async Task CheckOverviewViewport()
    {
        await using var fixture = await Fixture.Start(12);
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        await fixture.Send("long");
        var targets = fixture.Handles.Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
        var original = targets.Select(manager.ReadState).ToArray();
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "paneset-viewport-" + Guid.NewGuid().ToString("N") + ".json");
        new ShortcutSettings { Theme = AppearanceMode.Light }.Save(path);
        var panel = new MainWindow(manager, display, targets, enableShortcuts: false, settingsPath: path);
        try
        {
            foreach (bool dark in new[] { false, true })
            {
                UiPalette.Apply(dark);
                panel.Show();
                await fixture.Send("top 5");
                var bounds = manager.ReadState(targets[5]).Bounds;
                await ClickNativeOwned(fixture, targets[5].Handle, new Native.Point { X = bounds.X + 100, Y = bounds.Y + 12 });
                await Until(() => GetForegroundWindow() == targets[5].Handle);
                var picker = panel.OpenQuickSelection(targets[5].Handle, targets) ?? throw new Exception("No viewport picker created.");
                picker.Completed += result => Console.WriteLine($"SESSION {result}; {WindowEvidence(picker)}");
                try
                {
                    await Ready(picker);
                    Equal(12, Elements<CheckBox>(picker).Count());
                    Toggle(Candidate(picker, targets[1].Title)); Toggle(Candidate(picker, targets[2].Title));
                    foreach (var size in new[] { (1440d, 900d), (1000d, 650d), (700d, 450d), (700d, 599d), (700d, 600d), (700d, 450d), (1440d, 900d) })
                    {
                        picker.Width = size.Item1; picker.Height = size.Item2;
                        await picker.Dispatcher.InvokeAsync(() => picker.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ContextIdle);
                        if (!picker.IsReady || !picker.IsVisible) throw new Exception("Viewport session ended before layout measurement: " + WindowEvidence(picker));
                        Equal(dark, UiPalette.IsDark);
                        var client = ClientSize(picker);
                        var dpi = VisualTreeHelper.GetDpi(picker);
                        Equal((int)Math.Round(size.Item1 * dpi.DpiScaleX), Native.Bounds(new WindowInteropHelper(picker).Handle).Width);
                        Equal((int)Math.Round(size.Item2 * dpi.DpiScaleY), Native.Bounds(new WindowInteropHelper(picker).Handle).Height);
                        Console.WriteLine($"VIEWPORT dark={dark} requestedDIP={size} outer={Native.Bounds(new WindowInteropHelper(picker).Handle)} clientDIP={client} dpi={VisualTreeHelper.GetDpi(picker).PixelsPerInchX} originScreen={picker.PointToScreen(new Point())}");
                        Screenshot(picker, $"overview-viewport-{dark}-{size.Item2}");
                        foreach (string name in new[] { "确认本屏选窗", "取消本屏选窗" })
                        {
                            var button = ButtonNamed(picker, name); var point = button.TranslatePoint(new Point(), picker);
                            Console.WriteLine($"BUTTON {name} positionDIP={point} sizeDIP={button.ActualWidth},{button.ActualHeight} enabled={button.IsEnabled}");
                            if (point.X < 0 || point.Y < 0 || point.X + button.ActualWidth > client.Width || point.Y + button.ActualHeight > client.Height)
                                throw new Exception($"Overview action outside client: {name}, position={point}, size={button.ActualWidth},{button.ActualHeight}, client={client}");
                            Equal(true, button.IsEnabled);
                        }
                        var scroll = Elements<ScrollViewer>(picker).Single(s => s.Content is StackPanel);
                        Console.WriteLine($"SELECTED viewportDIP={scroll.ViewportHeight}");
                        Equal(true, scroll.ViewportHeight >= 70);
                    }
                    Invoke(ButtonNamed(picker, "取消本屏选窗")); Equal(false, manager.HasUndo);
                    for (int i = 0; i < targets.Length; i++) Equal(original[i], manager.ReadState(targets[i]));
                }
                finally { picker.Close(); }
            }
        }
        finally { panel.Exit(); if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }
}
