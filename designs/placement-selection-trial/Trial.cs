using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SelectionTrial;

internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var trial = new Trial(app, args);
        app.Startup += (_, _) => trial.Open();
        app.DispatcherUnhandledException += (_, e) =>
        {
            trial.Record("unexpected-error", new { message = e.Exception.Message });
            Console.Error.WriteLine(e.Exception); e.Handled = true; app.Shutdown(1);
        };
        return app.Run();
    }
}

internal static class Look
{
    internal static readonly SolidColorBrush Ink = Brush("#233039"), Muted = Brush("#617079"), Canvas = Brush("#F8F9F7"),
        Mint = Brush("#E6F4EB"), Focus = Brush("#276F62"), Line = Brush("#D7DFDF"), Primary = Brush("#25464C");
    internal static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    internal static TextBlock Text(string text, double size = 14, Brush? color = null) => new()
    { Text = text, FontSize = size, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
    internal static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 6), MinHeight = 36,
            Background = Brushes.White, Foreground = Ink, BorderBrush = Line };
        button.Click += (_, _) => action(); return button;
    }
}

internal sealed class Fixture : Window
{
    internal readonly TextBox Editor;
    internal readonly string Label;
    internal bool IsClosed;
    internal int UnderlyingClicks;
    internal Fixture(Trial trial, string label, string heading, string body, bool source)
    {
        Label = label; Title = "PaneSet 交互试验 · " + label;
        Width = 640; Height = 570; MinWidth = 360; MinHeight = 330;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Look.Canvas; Foreground = Look.Ink; FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 14;
        var root = new DockPanel { Margin = new Thickness(22) }; Content = root;
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        header.Children.Add(Look.Text(source ? "工作记录 · 可直接编辑" : "参考资料 · 测试窗口", 12, Look.Muted));
        header.Children.Add(Look.Text(heading, 25));
        var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(Look.Text("F8 选取位置    F9 恢复    F10 重置试验", 12, Look.Muted));
        var counter = Look.Text("普通按钮点击：0", 12, Look.Muted);
        footer.Children.Add(Look.Button("测试普通按钮", () =>
        { UnderlyingClicks++; counter.Text = "普通按钮点击：" + UnderlyingClicks; trial.Record("underlying-button", new { window = Label, count = UnderlyingClicks }); }));
        footer.Children.Add(counter);
        Editor = new TextBox { Text = body, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 16, Padding = new Thickness(14), Background = Brushes.White, Foreground = Look.Ink, BorderBrush = Look.Line };
        root.Children.Add(Editor);
        Editor.PreviewTextInput += (_, e) => trial.Typed(this, e.Text.Length, "text-input");
        Editor.PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Back or Key.Delete) trial.Typed(this, 1, "editing-key"); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.IsRepeat || Keyboard.Modifiers != ModifierKeys.None) return;
            if (e.Key == Key.F8) { e.Handled = true; trial.Begin(this); }
            if (e.Key == Key.F9) { e.Handled = true; trial.Restore(); }
            if (e.Key == Key.F10) { e.Handled = true; trial.Reset(); }
        };
        Closed += (_, _) => { IsClosed = true; trial.Active?.Cancel("测试窗口已关闭", false); trial.App.Shutdown(); };
    }
    internal bool FocusEditor() { bool active = Activate(); Editor.Focus(); Keyboard.Focus(Editor); return active; }
}

internal sealed class Trial
{
    internal readonly Application App;
    internal readonly List<Fixture> Fixtures = [];
    internal SelectionOverlay? Active;
    internal readonly List<object> Events = [];
    internal readonly string RunDirectory;
    internal readonly bool Checking;
    internal readonly bool ModelChecking;
    private nint modelForeground;
    internal bool IsForeground(Window window) => (ModelChecking ? modelForeground : Native.GetForegroundWindow()) == Native.Handle(window);
    internal Fixture? PointTarget(SelectionOverlay overlay, Native.Point point)
    {
        if (!ModelChecking) return Native.Pick(this, overlay, point);
        // Explicit observation injection for deterministic model checks only.
        if (Native.Bounds(overlay.Source).Contains(point)) return null;
        return Fixtures.AsEnumerable().Reverse().FirstOrDefault(f => !f.IsClosed && Native.Bounds(f).Contains(point));
    }
    private readonly string[] args;
    private Window controller = null!;
    private TextBlock status = null!;
    private readonly List<ComboBox> answers = [];
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private Native.Rect[] initial = [];
    private (Fixture Source, Native.Rect Bounds)? undo;
    private double? completedAt;
    private Fixture? completedSource;
    private bool resumed;
    internal int Moves;
    internal string LastResult = "";
    internal Trial(Application app, string[] arguments)
    {
        App = app; args = arguments; ModelChecking = args.Contains("--model-check"); Checking = ModelChecking || args.Contains("--check");
        RunDirectory = Checking && args.Length > 1 ? Path.GetFullPath(args[1])
            : Path.Combine(Path.GetTempPath(), "PaneSet-SelectionTrial", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(RunDirectory);
        app.Exit += (_, _) => { Active?.Dispose(); Export(); };
    }
    internal void Record(string type, object? detail = null)
    {
        var item = new { milliseconds = Math.Round(clock.Elapsed.TotalMilliseconds), type, detail };
        Events.Add(item); File.AppendAllText(Path.Combine(RunDirectory, "events.jsonl"), JsonSerializer.Serialize(item) + Environment.NewLine);
    }
    internal void Typed(Fixture source, int characters, string kind)
    {
        if (characters <= 0 || completedAt == null || source != completedSource || resumed) return;
        resumed = true;
        Record("first-input-after-result", new { window = source.Label, kind, characters, delayMs = Math.Round(clock.Elapsed.TotalMilliseconds - completedAt.Value),
            foregroundMatches = Native.GetForegroundWindow() == Native.Handle(source), focused = source.Editor.IsKeyboardFocused });
        status.Text = "已记录：结束后的首次输入。请在下方评价这次操作。";
    }
    internal void Open()
    {
        Fixtures.Add(new Fixture(this, "工作记录", "对照阅读记录", "阅读目标\n\n把参考资料里的重点整理在这里。\n\n先写一句话，再尝试调整窗口位置。\n", true));
        Fixtures.Add(new Fixture(this, "参考资料 A", "窗口放置的操作边界", "资料 A\n\n单窗放置只改变源窗口的位置和大小。\n参照窗口留在原位，仍可作为后续参考。\n\n这个窗口也是可编辑的测试窗口。", false));
        Fixtures.Add(new Fixture(this, "参考资料 B", "注意力与继续工作", "资料 B\n\n操作结束后，用户应该能够接着写下刚才的想法。\n\n如果需要额外点击找回光标，就记录这次打断。", false));
        foreach (var fixture in Fixtures) fixture.Show();
        controller = BuildController(); controller.Show();
        Reset();
        Record("trial-start", new { checking = Checking, evidence = "owned WPF windows; scoped F8; source-monitor only", doubleClickMs = Native.GetDoubleClickTime() });
        Export();
        if (Checking) App.Dispatcher.InvokeAsync(async () => await Smoke(), DispatcherPriority.ApplicationIdle);
    }
    private Window BuildController()
    {
        var window = new Window { Title = "PaneSet · 放置选取验证", Width = 560, Height = 550, MinWidth = 460, MinHeight = 440,
            Background = Look.Canvas, FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 14, WindowStartupLocation = WindowStartupLocation.Manual };
        var body = new StackPanel { Margin = new Thickness(22) };
        window.Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        body.Children.Add(Look.Text("放置选取 · 体验验证", 24));
        body.Children.Add(Look.Text("仅操作本程序创建的窗口。先在「工作记录」写一句话，按 F8 开始。", 14));
        body.Children.Add(Look.Text("1  试选并取消", 16));
        body.Children.Add(Look.Text("移动鼠标，停在两份资料上。说出当前状态，再取消；看能否接着输入。", 13, Look.Muted));
        body.Children.Add(Look.Text("2  预测、放置、继续写", 16));
        body.Children.Add(Look.Text("确认前先说出：哪扇窗口会移动、放到哪里。完成后直接续写，观察是否需要额外点击。", 13, Look.Muted));
        body.Children.Add(Look.Text("3  检查退出与连击", 16));
        body.Children.Add(Look.Text("重试空白取消、右键、双击，以及 Alt+Tab 后继续使用其他应用。", 13, Look.Muted));
        status = Look.Text("就绪：请先操作工作记录。", 13, Look.Focus); body.Children.Add(status);
        string[] questions = ["选取状态是否容易辨认？", "确认前是否清楚移动谁、放到哪里？", "结束后是否能自然继续工作？"];
        foreach (string question in questions)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 5) };
            var combo = new ComboBox { Width = 105, Margin = new Thickness(10, 0, 0, 0), MinHeight = 30 };
            foreach (string value in new[] { "未回答", "清楚 / 顺畅", "有些犹豫", "不清楚 / 打断" }) combo.Items.Add(value);
            combo.SelectedIndex = 0; DockPanel.SetDock(combo, Dock.Right); row.Children.Add(combo);
            row.Children.Add(Look.Text(question, 13)); body.Children.Add(row); answers.Add(combo);
        }
        var buttons = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(Look.Button("导出本次记录", () => { Export(); status.Text = "记录已保存到：" + RunDirectory; }));
        buttons.Children.Add(Look.Button("重置位置", Reset));
        buttons.Children.Add(Look.Button("结束试验", () => App.Shutdown())); body.Children.Add(buttons);
        body.Children.Add(Look.Text("F8 仅在试验窗口内生效。记录时间和输入次数，不保存你输入的正文。", 12, Look.Muted));
        window.Closed += (_, _) => App.Shutdown();
        return window;
    }
    internal void Reset()
    {
        Active?.Dispose(); Active = null;
        var work = SystemParameters.WorkArea;
        double w = Math.Min(650, Math.Max(380, work.Width * .31)), h = Math.Min(650, Math.Max(380, work.Height * .66));
        PlaceDip(Fixtures[0], work.Left + 35, work.Top + 70, w, h);
        PlaceDip(Fixtures[1], work.Left + work.Width * .38, work.Top + 85, w, h);
        PlaceDip(Fixtures[2], work.Left + work.Width * .55, work.Top + Math.Min(390, work.Height * .32), w, h);
        controller.Left = work.Right - controller.Width - 28; controller.Top = work.Top + 24;
        foreach (var fixture in Fixtures.Where(f => !f.IsClosed)) fixture.Show();
        Fixtures[0].FocusEditor(); initial = Fixtures.Select(Native.Bounds).ToArray();
        undo = null; completedAt = null; completedSource = null; resumed = false;
        status.Text = "已重置。请在工作记录内按 F8。"; Record("reset");
    }
    private static void PlaceDip(Window window, double x, double y, double width, double height)
    { window.WindowState = WindowState.Normal; window.Left = x; window.Top = y; window.Width = width; window.Height = height; }
    internal void Begin(Fixture source)
    {
        if (Active is { Finishing: false }) { Active.Cancel("再次按 F8", true); return; }
        Active?.Dispose(); Active = null;
        if (source.IsClosed || !IsForeground(source)) { status.Text = "请先激活一扇试验窗口。"; return; }
        completedAt = null; resumed = false;
        var overlay = new SelectionOverlay(this, source); Active = overlay;
        if (ModelChecking) { new System.Windows.Interop.WindowInteropHelper(overlay).EnsureHandle(); modelForeground = Native.Handle(overlay); }
        overlay.Show();
        Record("selection-start", new { source = source.Label });
    }
    internal void Finish(SelectionOverlay session, Fixture? reference, Native.Rect? destination, bool returnFocus, string reason)
    {
        if (Active != session || session.Source.IsClosed) return;
        var source = session.Source;
        bool ownedFocus = IsForeground(session);
        if (reference != null && destination is { } rect && ownedFocus)
        {
            var before = Native.Bounds(source);
            Native.MoveOwned(source, rect);
            var actual = Native.Bounds(source);
            if (!actual.Equals(rect)) { LastResult = "窗口没有到达预览位置，请用 F9 恢复。"; undo = (source, before); }
            else if (!actual.Equals(before)) { undo = (source, before); Moves++; LastResult = "已放置。可以直接继续输入。"; }
            else LastResult = "位置未改变，原恢复记录保留。";
            Record("placement-result", new { source = source.Label, target = reference.Label, confirmed = actual.Equals(rect), changed = !actual.Equals(before), moves = Moves });
        }
        else { LastResult = "已取消：" + reason; Record("selection-cancel", new { reason, noReactivation = !returnFocus || !ownedFocus }); }
        status.Text = LastResult; completedAt = clock.Elapsed.TotalMilliseconds; completedSource = source;
        if (returnFocus && ownedFocus)
        {
            source.FocusEditor();
            if (ModelChecking) modelForeground = Native.Handle(source);
        }
        Record("result-focus", new { foregroundMatches = Native.GetForegroundWindow() == Native.Handle(source), editorFocused = source.Editor.IsKeyboardFocused });
    }
    internal void Restore()
    {
        Active?.Dispose(); Active = null;
        if (undo is not { } snapshot || snapshot.Source.IsClosed) { status.Text = "没有本次试验可恢复的位置。"; return; }
        Native.MoveOwned(snapshot.Source, snapshot.Bounds); snapshot.Source.FocusEditor(); undo = null;
        status.Text = "已恢复试验源窗口。"; Record("restore");
    }
    internal void Export()
    {
        var data = new { scope = "owned-window native interaction trial, not production acceptance", humanAnswers = answers.Select(a => a.SelectedItem?.ToString()).ToArray(),
            humanVerdict = answers.Any(a => a.SelectedIndex == 0) ? "pending" : "participant-reported", moves = Moves, events = Events };
        File.WriteAllText(Path.Combine(RunDirectory, "results.json"), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }
    internal static void Capture(Window window, string path)
    {
        var image = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(window); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); png.Save(file);
    }
    private async Task Smoke()
    {
        var results = new List<object>();
        void Check(string name, bool okay) { results.Add(new { name, passed = okay }); if (!okay) throw new Exception(name); }
        async Task Settle() { await App.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); await Task.Delay(100); }
        try
        {
            await Settle();
            Capture(controller, Path.Combine(RunDirectory, "controller.png"));
            var controllerSize = new Size(controller.Width, controller.Height);
            controller.Width = controller.MinWidth; controller.Height = controller.MinHeight; await Settle();
            Capture(controller, Path.Combine(RunDirectory, "controller-minimum.png"));
            controller.Width = controllerSize.Width; controller.Height = controllerSize.Height;
            Capture(Fixtures[0], Path.Combine(RunDirectory, "editor.png"));
            Capture(Fixtures[1], Path.Combine(RunDirectory, "reference-a.png"));
            Capture(Fixtures[2], Path.Combine(RunDirectory, "reference-b.png"));
            Fixtures[1].Activate(); Fixtures[0].FocusEditor(); await Settle();
            if (ModelChecking) modelForeground = Native.Handle(Fixtures[0]);
            if (!IsForeground(Fixtures[0])) throw new InvalidOperationException("UNAVAILABLE: fixture foreground not acquired; no input sent");
            var before = Fixtures.Select(Native.Bounds).ToArray();
            Begin(Fixtures[0]); await Settle();
            Check("selection remains open before confirmation", Active is { Finishing: false });
            var pick = Active ?? throw new Exception("Selection unexpectedly closed");
            var point = Native.Bounds(Fixtures[1]).Center;
            pick.Observe(point, forceReady: true); await Settle();
            if (pick.Preview != Fixtures[1]) throw new InvalidOperationException("UNAVAILABLE: reference positive control obscured; no input sent");
            Capture(pick, Path.Combine(RunDirectory, "selection.png"));
            Check("hover preserves all fixture geometry", Fixtures.Select(Native.Bounds).SequenceEqual(before));
            pick.Cancel("smoke cancellation", true); await Settle();
            Check("cancel preserves geometry and no Undo created", Fixtures.Select(Native.Bounds).SequenceEqual(before) && undo == null);
            if (!ModelChecking) Check("normal cancel returns actual foreground and editor focus", Native.GetForegroundWindow() == Native.Handle(Fixtures[0]) && Fixtures[0].Editor.IsKeyboardFocused);
            Begin(Fixtures[0]); await Settle(); pick = Active!;
            pick.Observe(point, true); await Settle(); pick.GestureDown(point, false); pick.GestureUp(new Native.Point(point.X + 50, point.Y + 50), false);
            Check("drag does not commit", Moves == 0 && !pick.Finishing);
            pick.Observe(point, true); await Settle(); pick.GestureDown(point, false);
            var drift = Native.Bounds(Fixtures[1]); drift.Left += 8; drift.Right += 8; Native.MoveOwned(Fixtures[1], drift);
            pick.GestureUp(point, false);
            Check("reference drift refuses old preview", Moves == 0 && !pick.Finishing);
            Native.MoveOwned(Fixtures[1], before[1]);
            pick.Observe(point, true); await Settle(); pick.GestureDown(point, false); pick.GestureUp(point, false); await Settle();
            Check("confirmation moves source to shown geometry", Moves == 1 && Native.Bounds(Fixtures[0]).Equals(before[1]));
            Check("references stay unchanged", Native.Bounds(Fixtures[1]).Equals(before[1]) && Native.Bounds(Fixtures[2]).Equals(before[2]));
            if (!ModelChecking) Check("completion returns actual foreground and editor focus", Native.GetForegroundWindow() == Native.Handle(Fixtures[0]) && Fixtures[0].Editor.IsKeyboardFocused);
            pick.GestureDown(point, false); pick.GestureUp(point, false);
            Check("double-click tail cannot start another placement", Moves == 1);
            await Task.Delay((int)Native.GetDoubleClickTime() + 120);
            Check("tail layer disposes on timeout", Active == null);
            Restore(); await Settle();
            Check("restore returns original source geometry", Native.Bounds(Fixtures[0]).Equals(before[0]));
            Begin(Fixtures[0]); await Settle(); pick = Active!;
            pick.Observe(point, true); pick.GestureDown(point, false); pick.GestureUp(point, false);
            Check("unrendered target is never submitted by a quick click", !pick.Finishing && Moves == 1);
            await Settle(); pick.GestureDown(point, true); pick.Cancel("Esc while button held", true);
            Check("cancel during a held gesture waits for release", !pick.Finishing);
            pick.GestureUp(point, true);
            Check("right gesture cancellation preserves geometry and move count", pick.Finishing && Moves == 1 && Native.Bounds(Fixtures[0]).Equals(before[0]));
            await Task.Delay((int)Native.GetDoubleClickTime() + 120);
            Begin(Fixtures[0]); await Settle(); pick = Active!;
            pick.Observe(Native.Bounds(Fixtures[0]).Center, true); pick.GestureDown(Native.Bounds(Fixtures[0]).Center, false); pick.GestureUp(Native.Bounds(Fixtures[0]).Center, false); await Settle();
            Check("invalid complete click cancels without placement", pick.Finishing && Moves == 1);
            await Task.Delay((int)Native.GetDoubleClickTime() + 120);
            Begin(Fixtures[0]); await Settle(); pick = Active!; Fixtures[2].FocusEditor();
            if (ModelChecking) { modelForeground = Native.Handle(Fixtures[2]); pick.Cancel("injected foreground change", false); }
            await Settle();
            Check("session focus change cancels without returning to old source", Active == null && IsForeground(Fixtures[2]));
            Record("smoke-summary", new { evidence = ModelChecking
                ? "injected hit/foreground observations and gesture methods; real owned-window geometry/rendering; native input/focus unverified"
                : "synthetic coordinates/method calls; real fixture geometry and foreground reads; no native key/mouse injection", results });
            File.WriteAllText(Path.Combine(RunDirectory, "checks.json"), JsonSerializer.Serialize(new { passed = true, results }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS " + results.Count + (ModelChecking ? " model/owned-geometry checks" : " fixture/method checks") + "; physical input and human answers pending"); App.Shutdown(0);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(RunDirectory, "checks.json"), JsonSerializer.Serialize(new { passed = false, error = e.Message, results }, new JsonSerializerOptions { WriteIndented = true }));
            Console.Error.WriteLine(e); App.Shutdown(e.Message.StartsWith("UNAVAILABLE") ? 2 : 1);
        }
    }
}
