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

internal static partial class Program
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
        ("cross-monitor", CheckMonitors),
        ("three-window", async () =>
        {
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = manager.ListWindows(display).Where(t => fixture.Handles.Contains(t.Handle)).ToArray();
            var originals = targets.Select(manager.ReadState).ToArray();
            foreach (var kind in new[] { ArrangementKind.Automatic, ArrangementKind.MainAndOthers })
            {
                var result = await manager.ApplyAsync(targets, display, kind);
                if (result.Pending) result = await manager.CompletePendingAsync();
                Equal(true, result.Success); Equal(3, result.Windows.Count);
                var layout = LayoutEngine.Arrange(display.WorkArea, 3, kind);
                for (int i = 0; i < 3; i++) Equal(layout[i], manager.ReadState(targets[i]).Bounds);
                Equal(true, (await manager.UndoAsync()).Success);
                for (int i = 0; i < 3; i++) Equal(originals[i], manager.ReadState(targets[i]));
            }
        }),
        ("grid-windows", async () =>
        {
            await using var fixture = await Fixture.Start(9);
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
            var originals = targets.Select(manager.ReadState).ToArray();
            var chosen = targets.Take(8).ToArray();
            var result = await manager.ApplyAsync(chosen, display);
            if (result.Pending) result = await manager.CompletePendingAsync();
            Equal(true, result.Success); Equal(8, result.Windows.Count);
            var layout = LayoutEngine.Arrange(display.WorkArea, 8);
            CheckGeometry(display.WorkArea, layout);
            for (int i = 0; i < 8; i++) Equal(layout[i], manager.ReadState(targets[i]).Bounds);
            Equal(originals[8], manager.ReadState(targets[8]));
            Equal(true, (await manager.UndoAsync()).Success);
            for (int i = 0; i < 9; i++) Equal(originals[i], manager.ReadState(targets[i]));
        }),
        ("layout-many", () =>
        {
            var area = new WindowRect(-2560, -1440, 2559, 1399);
            foreach (int count in new[] { 2, 3, 4, 5, 8, 12 })
                foreach (var kind in Enum.GetValues<ArrangementKind>())
                {
                    var layout = LayoutEngine.Arrange(area, count, kind);
                    Equal(count, layout.Length); CheckGeometry(area, layout);
                }
            return Task.CompletedTask;
        }),
        ("layout-too-small", async () =>
        {
            await using var fixture = await Fixture.Start(12);
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = manager.ListWindows().Where(t => fixture.Handles.Contains(t.Handle)).ToArray();
            var originals = targets.Select(manager.ReadState).ToArray();
            Equal(false, (await manager.ApplyAsync(targets, display, ArrangementKind.Columns)).Success);
            Equal(false, manager.HasUndo);
            for (int i = 0; i < targets.Length; i++) Equal(originals[i], manager.ReadState(targets[i]));
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
        ("placement-guards", CheckPlacementGuards),
        ("placement-undo", CheckPlacementUndo),
        ("placement-flow", CheckPlacementFlow),
        ("placement", CheckPlacement),
        ("placement-settings", CheckPlacementSettings),
        ("placement-input", CheckPlacementInput),
        ("overview-selection-refresh", CheckOverviewSelectionRefresh),
        ("selection-eligibility", CheckSelectionEligibility),
        ("selection-order", CheckSelectionOrder),
        ("overview-size", CheckOverviewSize),
        ("overview-size-ui", CheckOverviewSizeUi),
        ("overview-viewport", CheckOverviewViewport),
        ("overview", CheckOverview),
        ("thumbnail", CheckThumbnail),
        ("mouse-routing", CheckMouseRouting),
        ("quick-entry", CheckQuickEntry),
        ("quick-cancel-undo", CheckQuickCancelUndo),
        ("quick-flow", CheckQuickFlow),
        ("quick-invalid", CheckQuickInvalid),
        ("quick-input", CheckQuickInput),
        ("global-quick-input", CheckGlobalQuickInput),
        ("quick-activation-failure", CheckQuickActivationFailure),
        ("screen-guard", CheckScreenGuard),
        ("screen-arrange", CheckScreenArrange),
        ("screen-partial", CheckScreenPartial),
        ("screen-eligibility", CheckScreenEligibility),
        ("screen-member-move", CheckScreenMemberMove),
        ("settings-ui", CheckSettingsUi),
        ("shortcut-settings", CheckShortcutSettings),
        ("settings-replace", CheckSettingsReplace),
        ("settings-cleanup", CheckSettingsCleanup),
        ("settings-recovery", CheckSettingsRecovery),
        ("settings-recovery-blocked", CheckSettingsRecoveryBlocked),
        ("settings-save-failures", CheckSettingsSaveFailures),
        ("settings-binding-replace", CheckSettingsBindingReplace),
        ("settings-temporary-cleanup", CheckSettingsTemporaryCleanup),
        ("configured-input", CheckConfiguredInput),
        ("single-instance", CheckSingleInstance),
        ("shortcut-routing", CheckShortcutRouting),
        ("shortcuts", CheckShortcuts),
        ("local-layout-keys", CheckLocalLayoutInput),
        ("local-layout-routing", CheckLayoutKeys),
        ("selection-refresh", CheckSelectionRefresh),
        ("recent-group", CheckRecentGroup),
        ("recent-group-noop", CheckRecentNoop),
        ("recent-group-missing", CheckRecentMissing),
        ("recent-group-failures", CheckRecentFailures),
        ("recent-ui-error", CheckRecentUiError),
        ("recent-group-exit", CheckRecentExit),
        ("ui-many", CheckManyUi),
        ("ui", CheckUi),
        ("appearance", CheckAppearance)
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        if (args.Contains("--fixture")) return FixtureHost.Run(args.Length > 1 ? int.Parse(args[1]) : 3);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int exit = 0;
        app.Dispatcher.InvokeAsync(async () =>
        {
            var cases = args.Length == 2 && args[0] == "--case" ? Cases.Where(c => c.Name == args[1]) : Cases;
            if (!cases.Any()) { Console.Error.WriteLine("Unknown check case."); exit = 1; }
            int passed = 0, failed = 0, unavailable = 0, executed = 0;
            foreach (var test in cases)
            {
                executed++;
                try { await test.Run(); passed++; Console.WriteLine("PASS " + test.Name); }
                catch (UnavailableException e) { unavailable++; Console.WriteLine("SKIP " + test.Name + ": " + e.Message); }
                catch (Exception e)
                {
                    failed++; Console.Error.WriteLine("FAIL " + test.Name + ": " + e); exit = 1;
                    // shortcut: guards use existing error text; introduce a typed failure if those messages change.
                    if (Environment.GetEnvironmentVariable("PANESET_CHECKS_STOP_ON_INPUT_GUARD") == "1" && e.ToString().Contains("injection refused"))
                    {
                        Console.WriteLine($"ABORT input protection; unrun={cases.Count() - executed}");
                        break;
                    }
                }
            }
            Console.WriteLine($"RESULT passed={passed}, failed={failed}, unavailable={unavailable}");
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
    private static Button ButtonNamed(Window window, string name) => Elements<Button>(window).Single(b => AutomationProperties.GetName(b) == name);
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

    private static void Screenshot(Window window, string name)
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
        var window = new MainWindow(manager, display, candidates, fixture.Handles[0], enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            var checks = Elements<CheckBox>(window).ToArray();
            var second = checks.Single(c => AutomationProperties.GetName(c) == "采购合同 · 修订稿");
            ((IToggleProvider)new CheckBoxAutomationPeer(second).GetPattern(PatternInterface.Toggle)).Toggle();
            await Task.Delay(80);
            Equal(true, ButtonNamed(window, "排布所选窗口").IsEnabled);
            var third = checks.Single(c => AutomationProperties.GetName(c) == "报价清单");
            ((IToggleProvider)new CheckBoxAutomationPeer(third).GetPattern(PatternInterface.Toggle)).Toggle();
            Equal(true, third.IsChecked == true);
            ((IToggleProvider)new CheckBoxAutomationPeer(third).GetPattern(PatternInterface.Toggle)).Toggle();
            foreach (var size in new[] { (820d, 730d, "paneset-normal"), (600d, 730d, "paneset-narrow"), (820d, 600d, "paneset-short"), (600d, 600d, "paneset-minimum") })
            {
                window.Width = size.Item1; window.Height = size.Item2; await Task.Delay(60);
                var button = ButtonNamed(window, "排布所选窗口");
                window.UpdateLayout();
                var point = button.TranslatePoint(new(0, 0), window);
                var client = ClientSize(window);
                if (point.Y + button.ActualHeight > client.Height || point.X + button.ActualWidth > client.Width)
                    throw new Exception("Action button clipped at minimum size.");
                Screenshot(window, size.Item3);
            }
            var search = Elements<TextBox>(window).Single(); search.Text = "修订"; await Task.Delay(60);
            Equal(1, Elements<CheckBox>(window).Count()); Equal(true, ButtonNamed(window, "排布所选窗口").IsEnabled);
            search.Text = ""; await Task.Delay(60);
            Invoke(ButtonNamed(window, "交换左右")); await Task.Delay(60);
            Invoke(ButtonNamed(window, "排布所选窗口"));
            await Until(() => window.WindowState == System.Windows.WindowState.Minimized);
            var expected = LayoutEngine.TwoColumns(display.WorkArea);
            Equal(expected[0], manager.ReadState(candidates.Single(t => t.Handle == fixture.Handles[1])).Bounds);
            Equal(expected[1], manager.ReadState(candidates.Single(t => t.Handle == fixture.Handles[0])).Bounds);
            window.WindowState = System.Windows.WindowState.Normal;
            Invoke(ButtonNamed(window, "恢复上次排布")); await Until(() => !manager.HasUndo && !manager.HasPending);
            foreach (var target in candidates) Equal(originals[target.Handle], manager.ReadState(target));
        }
        finally { window.Exit(); }
        await fixture.Send("long");
        using var longManager = new WindowManager();
        var longWindow = new MainWindow(longManager, display, longManager.ListWindows(display).Where(t => fixture.Handles.Contains(t.Handle)).ToArray(), enableShortcuts: false) { Width = 600, Height = 600 };
        longWindow.Show(); await Task.Delay(120); Screenshot(longWindow, "paneset-long-title"); longWindow.Exit();
    }

    private static async Task CheckAppearance()
    {
        string path = Path.Combine(Path.GetTempPath(), "paneset-appearance-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            new ShortcutSettings { Theme = AppearanceMode.Dark }.Save(path);
            Equal(AppearanceMode.Dark, ShortcutSettings.Load(path, out _).Theme);
            await using var fixture = await Fixture.Start();
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var candidates = manager.ListWindows(display).Where(t => fixture.Handles.Contains(t.Handle)).ToArray();
            var window = new MainWindow(manager, display, candidates, fixture.Handles[0], enableShortcuts: false, settingsPath: path);
            window.Show(); await Task.Delay(120);
            void CheckPreviewHeading()
            {
                window.UpdateLayout();
                var preview = (Expander)window.FindName("PreviewExpander");
                var heading = Elements<TextBlock>(preview).Single(t => t.Text == "布局预览");
                Equal(UiPalette.Ink.Color, ((SolidColorBrush)heading.Foreground).Color);
            }
            try
            {
                var second = Elements<CheckBox>(window).Single(c => AutomationProperties.GetName(c) == "采购合同 · 修订稿");
                ((IToggleProvider)new CheckBoxAutomationPeer(second).GetPattern(PatternInterface.Toggle)).Toggle();
                Equal(true, UiPalette.IsDark);
                Equal(UiPalette.Canvas.Color, ((SolidColorBrush)window.Background).Color);
                var rowTitle = Elements<TextBlock>(window).Single(t => t.Text == "采购合同 · 修订稿");
                Equal(UiPalette.Ink.Color, ((SolidColorBrush)rowTitle.Foreground).Color);
                CheckPreviewHeading();
                Screenshot(window, "paneset-appearance-dark");
                await PreparePickerHost(window);
                var quick = new QuickSelectWindow(window, manager, display, candidates, candidates[0].Handle, ArrangementKind.Automatic);
                quick.Show();
                try
                {
                    await Ready(quick);
                    Screenshot(quick, "paneset-overview-dark");
                    var card = Elements<CheckBox>(quick).First();
                    card.Focus();
                    await Until(() => card.IsKeyboardFocused);
                    var cardFrame = (Border)card.Template.FindName("CardFrame", card);
                    Equal(UiPalette.Focus.Color, ((SolidColorBrush)cardFrame.BorderBrush).Color);
                    UiPalette.Apply(false);
                    await Until(() => ((SolidColorBrush)cardFrame.BorderBrush).Color == UiPalette.Focus.Color);
                    CheckPreviewHeading();
                    UiPalette.Apply(true);
                    await Until(() => ((SolidColorBrush)cardFrame.BorderBrush).Color == UiPalette.Focus.Color);
                    CheckPreviewHeading();
                    quick.Width = 700; quick.Height = 450; quick.UpdateLayout();
                    var action = ButtonNamed(quick, "确认本屏选窗");
                    var point = action.TranslatePoint(new Point(), quick);
                    var client = ClientSize(quick);
                    Equal(true, point.X >= 0 && point.Y >= 0 && point.X + action.ActualWidth <= client.Width && point.Y + action.ActualHeight <= client.Height);
                }
                finally { quick.Close(); }
                var settings = new SettingsWindow(window, null, AppearanceMode.Dark, mode =>
                {
                    new ShortcutSettings { Theme = mode }.Save(path);
                    UiPalette.Apply(ThemeSupport.ResolveDark(mode));
                    return "";
                });
                settings.Show(); await Task.Delay(60);
                try
                {
                    Screenshot(settings, "paneset-settings-dark");
                    var picker = Elements<ComboBox>(settings).Single(c => AutomationProperties.GetName(c) == "外观模式");
                    picker.SelectedIndex = 1;
                    Equal(AppearanceMode.Light, ShortcutSettings.Load(path, out _).Theme);
                    Equal(false, UiPalette.IsDark);
                    Screenshot(settings, "paneset-settings-light");
                }
                finally { settings.Close(); }
                await Task.Delay(60);
                Equal(UiPalette.Canvas.Color, ((SolidColorBrush)window.Background).Color);
                Equal(UiPalette.Ink.Color, ((SolidColorBrush)rowTitle.Foreground).Color);
                CheckPreviewHeading();
                Screenshot(window, "paneset-appearance-light");
                Invoke(ButtonNamed(window, "最小化"));
                await Until(() => window.WindowState == WindowState.Minimized);
                window.WindowState = WindowState.Normal;
                Invoke(ButtonNamed(window, "最大化或还原"));
                await Until(() => window.WindowState == WindowState.Maximized);
                Invoke(ButtonNamed(window, "最大化或还原"));
                await Until(() => window.WindowState == WindowState.Normal);
            }
            finally { window.Exit(); }
            Equal(AppearanceMode.Light, ShortcutSettings.Load(path, out _).Theme);
            File.WriteAllText(path, "{\"version\":1,\"shortcuts\":{},\"theme\":99}");
            Equal(AppearanceMode.System, ShortcutSettings.Load(path, out string warning).Theme);
            Equal(true, warning.Length > 0);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Process process;
        public nint[] Handles { get; }
        public bool Owns(nint handle)
        {
            Native.GetWindowThreadProcessId(handle, out uint pid);
            return !process.HasExited && Handles.Contains(handle) && pid == process.Id;
        }
        private Fixture(Process process, nint[] handles) { this.process = process; Handles = handles; }
        public static async Task<Fixture> Start(int count = 3)
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
            if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--fixture"); start.ArgumentList.Add(count.ToString());
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
