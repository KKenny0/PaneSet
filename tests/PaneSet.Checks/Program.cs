using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PaneSet;

namespace PaneSet.Checks;

internal static class Program
{
    private static readonly List<(string Name, Func<Task> Run)> Cases =
    [
        ("layout", () =>
        {
            var layout = LayoutEngine.TwoColumns(new WindowRect(0, 0, 1920, 1040));
            Equal(new WindowRect(0, 0, 954, 1040), layout[0]);
            Equal(new WindowRect(966, 0, 954, 1040), layout[1]);
            return Task.CompletedTask;
        }),
        ("enumeration", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var targets = manager.ListWindows(WindowManager.DisplayFor(fixture.Handles[0]));
            Equal(3, targets.Count(t => fixture.Handles.Contains(t.Handle)));
        }),
        ("arrange", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            var untouched = manager.ReadState(targets[2]);
            Equal(true, (await manager.ApplyAsync(targets.Take(2).ToArray(), display)).Success);
            var expected = LayoutEngine.TwoColumns(display.WorkArea);
            Equal(expected[0], manager.ReadState(targets[0]).Bounds);
            Equal(expected[1], manager.ReadState(targets[1]).Bounds);
            Equal(untouched, manager.ReadState(targets[2]));
        }),
        ("restore", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            var original = targets.Select(manager.ReadState).ToArray();
            Equal(true, (await manager.ApplyAsync(targets, display)).Success);
            Equal(true, (await manager.UndoAsync()).Success);
            Equal(original[0], manager.ReadState(targets[0]));
            Equal(original[1], manager.ReadState(targets[1]));
        }),
        ("group-snapshot", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            var original = targets.Select(manager.ReadState).ToArray();
            await fixture.Send("link");
            Equal(true, (await manager.ApplyAsync(targets, display)).Success);
            Equal(true, (await manager.UndoAsync()).Success);
            Equal(original[0], manager.ReadState(targets[0])); Equal(original[1], manager.ReadState(targets[1]));
        }),
        ("group-partial-snapshot", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            var original = targets.Select(manager.ReadState).ToArray();
            await fixture.Send("linkmin");
            var result = await manager.ApplyAsync(targets, display);
            if (result.Pending) result = await manager.CompletePendingAsync();
            Equal(true, result.Windows[0].Success); Equal(false, result.Windows[1].Success);
            Equal(true, (await manager.UndoAsync()).Success);
            Equal(original[0], manager.ReadState(targets[0])); Equal(original[1], manager.ReadState(targets[1]));
        }),
        ("maximized", async () =>
        {
            await using var fixture = await Fixture.Start();
            await fixture.Send("max 0");
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            var original = manager.ReadState(targets[0]);
            Equal(true, original.Maximized);
            Equal(true, (await manager.ApplyAsync(targets, display)).Success);
            Equal(false, manager.ReadState(targets[0]).Maximized);
            Equal(true, (await manager.UndoAsync()).Success);
            Equal(original, manager.ReadState(targets[0]));
        }),
        ("closed-target", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            var original = manager.ReadState(targets[1]);
            Equal(true, (await manager.ApplyAsync(targets, display)).Success);
            await fixture.Send("close 0");
            var result = await manager.UndoAsync();
            Equal(false, result.Windows[0].Success);
            Equal(true, result.Windows[1].Success);
            Equal(original, manager.ReadState(targets[1]));
        }),
        ("partial-failure", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            var original = targets.Select(manager.ReadState).ToArray();
            await fixture.Send("block 1");
            var result = await manager.ApplyAsync(targets, display);
            if (result.Pending) result = await manager.CompletePendingAsync();
            Equal(true, result.Windows[0].Success);
            Equal(false, result.Windows[1].Success);
            await fixture.Send("unblock");
            Equal(true, (await manager.UndoAsync()).Success);
            Equal(original[0], manager.ReadState(targets[0]));
            Equal(original[1], manager.ReadState(targets[1]));
        }),
        ("delayed-response", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            var original = targets.Select(manager.ReadState).ToArray();
            await fixture.Send("stall");
            Equal(true, (await manager.ApplyAsync(targets, display)).Pending);
            Equal(true, (await manager.UndoAsync()).Pending);
            Equal(true, (await manager.CompletePendingAsync()).Success);
            Equal(true, (await manager.UndoAsync()).Success);
            Equal(original[0], manager.ReadState(targets[0]));
            Equal(original[1], manager.ReadState(targets[1]));
        }),
        ("identity", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            var original = targets.Select(manager.ReadState).ToArray();
            var invalid = targets[0] with { Generation = targets[0].Generation + 100 };
            Equal(true, (await manager.ApplyAsync([invalid, targets[1]], display)).NeedsRefresh);
            Equal(original[0], manager.ReadState(targets[0])); Equal(original[1], manager.ReadState(targets[1]));
            Equal(true, (await manager.ApplyAsync(targets, display)).Success);
            Equal(true, (await manager.ApplyAsync([invalid, targets[1]], display)).NeedsRefresh);
            Equal(true, manager.HasUndo);
            Equal(true, (await manager.UndoAsync()).Success);
            Equal(original[0], manager.ReadState(targets[0])); Equal(original[1], manager.ReadState(targets[1]));
        }),
        ("restore-retry", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            var original = targets.Select(manager.ReadState).ToArray();
            Equal(true, (await manager.ApplyAsync(targets, display)).Success);
            await fixture.Send("block 0");
            var result = await manager.UndoAsync();
            if (result.Pending) result = await manager.CompletePendingAsync();
            Equal(false, result.Windows[0].Success); Equal(true, result.Windows[1].Success); Equal(true, manager.HasUndo);
            await fixture.Send("unblock");
            Equal(true, (await manager.UndoAsync()).Success);
            Equal(original[0], manager.ReadState(targets[0])); Equal(original[1], manager.ReadState(targets[1]));
        }),
        ("terminated-hang", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Take(2).Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
            await fixture.Send("hang");
            Equal(true, (await manager.ApplyAsync(targets, display)).Pending);
            fixture.Terminate();
            var result = await manager.CompletePendingAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Equal(false, result.Success); Equal(false, manager.HasPending);
        }),
        ("layout-invalid", () =>
        {
            foreach (var area in new[] { new WindowRect(0, 0, 12, 100), new WindowRect(0, 0, 100, 0), new WindowRect(int.MaxValue - 1, 0, 100, 100) })
            {
                try { LayoutEngine.TwoColumns(area); throw new Exception("Invalid work area was accepted."); }
                catch (ArgumentOutOfRangeException) { }
            }
            return Task.CompletedTask;
        }),
        ("layout-negative", () =>
        {
            var layout = LayoutEngine.TwoColumns(new(-1920, -100, 1919, 1000));
            Equal(new WindowRect(-1920, -100, 953, 1000), layout[0]);
            Equal(new WindowRect(-955, -100, 954, 1000), layout[1]);
            return Task.CompletedTask;
        }),
        ("ui", CheckUi)
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--fixture")) return FixtureHost.Run();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int exit = 0;
        app.Dispatcher.InvokeAsync(async () =>
        {
            var cases = args.Length == 2 && args[0] == "--case" ? Cases.Where(c => c.Name == args[1]) : Cases;
            foreach (var test in cases)
            {
                try { await test.Run(); Console.WriteLine("PASS " + test.Name); }
                catch (Exception e) { Console.Error.WriteLine("FAIL " + test.Name + ": " + e); exit = 1; }
            }
            app.Shutdown(exit);
        });
        app.Run();
        return exit;
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
    }

    private static IEnumerable<T> Elements<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T element) yield return element;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Elements<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static Button ButtonNamed(MainWindow window, string name) => Elements<Button>(window).Single(b => AutomationProperties.GetName(b) == name);
    private static void Invoke(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
    private static async Task Until(Func<bool> condition)
    {
        for (int i = 0; i < 100; i++) { if (condition()) return; await Task.Delay(50); }
        throw new Exception("UI did not reach the expected state.");
    }
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint window, [Out] int[] rect);

    private static Size ClientSize(Window window)
    {
        var rect = new int[4];
        if (!GetClientRect(new WindowInteropHelper(window).Handle, rect)) throw new Exception("Cannot read client viewport.");
        var dpi = VisualTreeHelper.GetDpi(window);
        return new Size(rect[2] / dpi.DpiScaleX, rect[3] / dpi.DpiScaleY);
    }

    private static void Screenshot(MainWindow window, string name)
    {
        window.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(window);
        var client = ClientSize(window);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(client.Width * dpi.DpiScaleX), (int)Math.Ceiling(client.Height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory("artifacts");
        using var stream = File.Create("artifacts/" + name + ".png"); encoder.Save(stream);
    }
    private static async Task CheckUi()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = manager.ListWindows(display).Where(t => fixture.Handles.Contains(t.Handle)).ToArray();
        var originals = candidates.ToDictionary(t => t.Handle, manager.ReadState);
        var window = new MainWindow(manager, display, candidates, fixture.Handles[0]);
        window.Show(); await Task.Delay(120);
        try
        {
            var checks = Elements<CheckBox>(window).ToArray();
            var second = checks.Single(c => AutomationProperties.GetName(c) == "采购合同 · 修订稿");
            ((IToggleProvider)new CheckBoxAutomationPeer(second).GetPattern(PatternInterface.Toggle)).Toggle();
            await Task.Delay(80);
            Equal(true, ButtonNamed(window, "排布这两扇窗口").IsEnabled);
            var third = checks.Single(c => AutomationProperties.GetName(c) == "报价清单");
            ((IToggleProvider)new CheckBoxAutomationPeer(third).GetPattern(PatternInterface.Toggle)).Toggle();
            Equal(false, third.IsChecked == true);
            foreach (var size in new[] { (820d, 730d, "paneset-normal"), (600d, 730d, "paneset-narrow"), (820d, 600d, "paneset-short"), (600d, 600d, "paneset-minimum") })
            {
                window.Width = size.Item1; window.Height = size.Item2; await Task.Delay(60);
                var button = ButtonNamed(window, "排布这两扇窗口");
                window.UpdateLayout();
                var point = button.TranslatePoint(new(0, 0), window);
                var client = ClientSize(window);
                if (point.Y + button.ActualHeight > client.Height || point.X + button.ActualWidth > client.Width)
                    throw new Exception("Action button clipped at minimum size.");
                Screenshot(window, size.Item3);
            }
            var search = Elements<TextBox>(window).Single(); search.Text = "修订"; await Task.Delay(60);
            Equal(1, Elements<CheckBox>(window).Count()); Equal(true, ButtonNamed(window, "排布这两扇窗口").IsEnabled);
            search.Text = ""; await Task.Delay(60);
            Invoke(ButtonNamed(window, "交换左右")); await Task.Delay(60);
            Invoke(ButtonNamed(window, "排布这两扇窗口"));
            await Until(() => window.WindowState == System.Windows.WindowState.Minimized);
            var expected = LayoutEngine.TwoColumns(display.WorkArea);
            Equal(expected[0], manager.ReadState(candidates.Single(t => t.Handle == fixture.Handles[1])).Bounds);
            Equal(expected[1], manager.ReadState(candidates.Single(t => t.Handle == fixture.Handles[0])).Bounds);
            window.WindowState = System.Windows.WindowState.Normal;
            Invoke(ButtonNamed(window, "恢复上次排布")); await Until(() => !manager.HasUndo && !manager.HasPending);
            foreach (var target in candidates) Equal(originals[target.Handle], manager.ReadState(target));
        }
        finally { window.Close(); }
        await fixture.Send("long");
        using var longManager = new WindowManager();
        var longWindow = new MainWindow(longManager, display, longManager.ListWindows(display).Where(t => fixture.Handles.Contains(t.Handle)).ToArray()) { Width = 600, Height = 600 };
        longWindow.Show(); await Task.Delay(120); Screenshot(longWindow, "paneset-long-title"); longWindow.Close();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Process process;
        public nint[] Handles { get; }
        private Fixture(Process process, nint[] handles) { this.process = process; Handles = handles; }
        public static async Task<Fixture> Start()
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
            if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--fixture");
            var process = Process.Start(start)!;
            try
            {
                var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(8));
                return new(process, line!.Split(',').Select(s => (nint)long.Parse(s)).ToArray());
            }
            catch { process.Kill(true); process.Dispose(); throw; }
        }
        public async Task Send(string command)
        {
            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();
            Equal("OK", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));
            await Task.Delay(60);
        }
        public void Terminate() => process.Kill(true);
        public async ValueTask DisposeAsync()
        {
            try { if (!process.HasExited) { await process.StandardInput.WriteLineAsync("quit"); await process.StandardInput.FlushAsync(); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } }
            finally { if (!process.HasExited) process.Kill(true); process.Dispose(); }
        }
    }
}
