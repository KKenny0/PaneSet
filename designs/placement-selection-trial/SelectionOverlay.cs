using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace SelectionTrial;

internal sealed class SelectionOverlay : Window, IDisposable
{
    internal readonly Fixture Source;
    internal readonly Native.MonitorInfo Screen;
    internal Fixture? Preview { get; private set; }
    internal bool Finishing { get; private set; }
    private readonly Trial trial;
    private readonly Canvas canvas = new();
    private readonly Border outline, hud;
    private readonly TextBlock sourceText, targetText, help;
    private readonly DispatcherTimer poll, tail;
    private readonly uint sourceDpi;
    private readonly double scale;
    private Native.Rect previewBounds;
    private Native.Point pressPoint;
    private Fixture? pressedTarget;
    private Native.Rect pressedBounds;
    private bool armed, gesture, rightGesture, dragged, releasing, disposed;
    private int version, renderedVersion = -1, lastFrameVersion = -1;
    private TimeSpan lastFrame;
    private string? deferredCancel;
    private DateTime tailDeadline;

    internal SelectionOverlay(Trial owner, Fixture source)
    {
        trial = owner; Source = source; Screen = Native.Environment(source);
        sourceDpi = Native.GetDpiForWindow(Native.Handle(source)); scale = sourceDpi / 96d;
        if (scale <= 0) throw new InvalidOperationException("无法读取试验屏幕缩放。");
        Title = "PaneSet · 正在选取放置位置";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        ShowInTaskbar = false; Topmost = true; WindowStartupLocation = WindowStartupLocation.Manual;
        Left = Screen.Monitor.Left / scale; Top = Screen.Monitor.Top / scale;
        Width = Screen.Monitor.Width / scale; Height = Screen.Monitor.Height / scale;
        Background = new SolidColorBrush(Color.FromArgb(28, 15, 27, 38));
        FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 14; Content = canvas;
        canvas.Background = Brushes.Transparent; canvas.Focusable = true;
        outline = new Border { BorderThickness = new Thickness(3), BorderBrush = Look.Focus, Background = Look.Brush("#14276F62"),
            IsHitTestVisible = false, Visibility = Visibility.Collapsed, CornerRadius = new CornerRadius(3) };
        canvas.Children.Add(outline);
        var labels = new StackPanel();
        labels.Children.Add(Look.Text("正在选取放置位置", 18));
        sourceText = Look.Text("移动：" + source.Label, 14); labels.Children.Add(sourceText);
        targetText = Look.Text("指向另一扇测试窗口，查看目标位置。", 14, Look.Focus); labels.Children.Add(targetText);
        help = Look.Text("单击描边窗口放置 · 右键 / Esc / F8 取消 · 点击其他位置退出", 12, Look.Muted); help.Margin = new Thickness(0);
        labels.Children.Add(help);
        hud = new Border { Background = Brushes.White, BorderBrush = Look.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 12, 18, 12), Width = Math.Min(740, Width - 48), Child = labels, IsHitTestVisible = false };
        canvas.Children.Add(hud); Canvas.SetLeft(hud, 24); Canvas.SetTop(hud, (Screen.Work.Top - Screen.Monitor.Top) / scale + 18);
        poll = new DispatcherTimer(TimeSpan.FromMilliseconds(35), DispatcherPriority.Input, (_, _) => Poll(), Dispatcher);
        tail = new DispatcherTimer(TimeSpan.FromMilliseconds(25), DispatcherPriority.Input, (_, _) => TailTick(), Dispatcher);
        // This constructor starts a DispatcherTimer; lifecycle entry points start them explicitly.
        poll.Stop(); tail.Stop();
        SourceInitialized += (_, _) =>
        {
            Native.SetWindowPos(Native.Handle(this), -1, Screen.Monitor.Left, Screen.Monitor.Top, Screen.Monitor.Width, Screen.Monitor.Height, 0x10);
            HwndSource.FromHwnd(Native.Handle(this))?.AddHook(Receive);
        };
        ContentRendered += (_, _) =>
        {
            canvas.Focus();
            if (!trial.IsForeground(this) || (!trial.ModelChecking && !IsKeyboardFocusWithin))
            { Cancel("未取得选取焦点", false); return; }
            CompositionTarget.Rendering += Frame; Poll(); poll.Start();
        };
        PreviewMouseDown += (_, e) =>
        {
            e.Handled = true;
            if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right)) return;
            if (Native.GetCursorPos(out var point)) GestureDown(point, e.ChangedButton == MouseButton.Right);
        };
        PreviewMouseUp += (_, e) =>
        {
            e.Handled = true;
            if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right)) return;
            if (Native.GetCursorPos(out var point)) GestureUp(point, e.ChangedButton == MouseButton.Right);
        };
        PreviewMouseMove += (_, _) =>
        {
            if (gesture && Native.GetCursorPos(out var point))
                dragged |= Math.Abs(point.X - pressPoint.X) / scale > SystemParameters.MinimumHorizontalDragDistance
                    || Math.Abs(point.Y - pressPoint.Y) / scale > SystemParameters.MinimumVerticalDragDistance;
        };
        LostMouseCapture += (_, _) => { if (!releasing && !Finishing) Cancel("鼠标捕获已改变", false); };
        PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key is Key.LeftAlt or Key.RightAlt or Key.Tab) return;
            e.Handled = true;
            if (!e.IsRepeat && (key == Key.Escape || (key == Key.F8 && armed))) Cancel(key == Key.Escape ? "Esc" : "再次按 F8", true);
        };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!Finishing && !disposed && !trial.IsForeground(this)) Cancel("切换到其他窗口", false);
        }));
        Closed += (_, _) => Cleanup();
    }
    private nint Receive(nint window, int message, nint key, nint data, ref bool handled)
    {
        if (Finishing && message == 0x21) { handled = true; return 3; }
        return 0;
    }
    private void Frame(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs frame || frame.RenderingTime == lastFrame) return;
        lastFrame = frame.RenderingTime;
        if (lastFrameVersion == version) renderedVersion = version;
        lastFrameVersion = version;
    }
    private bool EnvironmentValid()
    {
        if (Source.IsClosed || !Native.IsWindowEnabled(Native.Handle(Source))) return false;
        var current = Native.Environment(Source);
        return current.Monitor.Equals(Screen.Monitor) && current.Work.Equals(Screen.Work)
            && Native.GetDpiForWindow(Native.Handle(Source)) == sourceDpi;
    }
    private void Poll()
    {
        if (disposed || Finishing) return;
        if (!EnvironmentValid()) { Cancel("源窗口或显示环境改变", true); return; }
        if (deferredCancel != null && !Native.AnyButton) { Complete(null, null, true, deferredCancel); return; }
        if (!armed && !Native.Down(0x77) && !Native.AnyButton && !Native.Down(16) && !Native.Down(17) && !Native.Down(18)) armed = true;
        if (!trial.Checking && !gesture && Native.GetCursorPos(out var point)) Observe(point);
    }
    internal void Observe(Native.Point point, bool forceReady = false)
    {
        if (Finishing || gesture || disposed) return;
        if (forceReady) armed = true;
        bool overHud = point.X >= Screen.Monitor.Left + 24 * scale && point.X <= Screen.Monitor.Left + (24 + hud.ActualWidth) * scale
            && point.Y >= Screen.Work.Top + 18 * scale && point.Y <= Screen.Work.Top + (18 + hud.ActualHeight) * scale;
        var candidate = !overHud && Screen.Monitor.Contains(point) ? trial.PointTarget(this, point) : null;
        var bounds = candidate == null ? default : Native.Bounds(candidate);
        if (candidate == Preview && bounds.Equals(previewBounds)) return;
        Preview = candidate; previewBounds = bounds; version++;
        if (candidate == null)
        {
            outline.Visibility = Visibility.Collapsed;
            targetText.Text = "此处不可借用。指向另一扇窗口，或点击退出。";
        }
        else
        {
            outline.Width = bounds.Width / scale; outline.Height = bounds.Height / scale;
            Canvas.SetLeft(outline, (bounds.Left - Screen.Monitor.Left) / scale); Canvas.SetTop(outline, (bounds.Top - Screen.Monitor.Top) / scale);
            outline.Visibility = Visibility.Visible;
            targetText.Text = "借用：" + candidate.Label + " · " + bounds.Width + " × " + bounds.Height;
        }
        trial.Record("preview", new { source = Source.Label, target = candidate?.Label, version });
    }
    internal void GestureDown(Native.Point point, bool right)
    {
        if (disposed) return;
        if (Finishing)
        {
            tailDeadline = DateTime.UtcNow.AddMilliseconds(Native.GetDoubleClickTime());
            trial.Record("tail-click-consumed", new { right }); return;
        }
        if (gesture || !armed) return;
        if (!trial.Checking && Keyboard.Modifiers != ModifierKeys.None) { help.Text = "请松开修饰键后再单击。Esc 取消。"; return; }
        if (!trial.Checking && !trial.IsForeground(this)) { Cancel("前台已改变", false); return; }
        // Do not select a fresh target on click. Only the already rendered preview is eligible.
        gesture = true; rightGesture = right; dragged = false; pressPoint = point;
        pressedTarget = renderedVersion == version ? Preview : null; pressedBounds = previewBounds;
        if (!trial.Checking && !CaptureMouse()) { gesture = false; Cancel("无法接收完整单击", false); return; }
        trial.Record("gesture-down", new { right, target = pressedTarget?.Label, rendered = renderedVersion == version });
    }
    internal void GestureUp(Native.Point point, bool right)
    {
        if (disposed) return;
        if (Finishing) { tailDeadline = DateTime.UtcNow.AddMilliseconds(Native.GetDoubleClickTime()); return; }
        if (!gesture || right != rightGesture) return;
        gesture = false; releasing = true; if (IsMouseCaptured) ReleaseMouseCapture(); releasing = false;
        if (deferredCancel != null) { Complete(null, null, true, deferredCancel); return; }
        if (right) { Complete(null, null, true, "右键"); return; }
        dragged |= Math.Abs(point.X - pressPoint.X) / scale > SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(point.Y - pressPoint.Y) / scale > SystemParameters.MinimumVerticalDragDistance;
        if (dragged) { trial.Record("gesture-refused", new { reason = "drag" }); Observe(point); return; }
        if (pressedTarget == null)
        {
            if (Preview != null && renderedVersion != version) { targetText.Text = "目标正在更新，请看清描边后再点一次。"; return; }
            Complete(null, null, true, "点击无效区域"); return;
        }
        if (!EnvironmentValid()) { Complete(null, null, true, "源窗口或显示环境改变"); return; }
        bool valid = !pressedTarget.IsClosed && Native.Bounds(pressedTarget).Equals(pressedBounds)
            && trial.PointTarget(this, point) == pressedTarget && trial.PointTarget(this, pressPoint) == pressedTarget;
        if (!valid)
        {
            trial.Record("gesture-refused", new { reason = "target changed" });
            Preview = null; version++; outline.Visibility = Visibility.Collapsed;
            targetText.Text = "目标已改变，本次未放置。请重新预览。"; return;
        }
        Complete(pressedTarget, pressedBounds, true, "确认");
    }
    internal void Cancel(string reason, bool returnFocus)
    {
        if (Finishing || disposed) return;
        if (returnFocus && (gesture || Native.AnyButton))
        { deferredCancel = reason; help.Text = "已取消，松开鼠标后退出。"; return; }
        Complete(null, null, returnFocus, reason);
    }
    private void Complete(Fixture? target, Native.Rect? rect, bool returnFocus, string reason)
    {
        if (Finishing || disposed) return;
        Finishing = true; poll.Stop(); CompositionTarget.Rendering -= Frame;
        releasing = true; if (IsMouseCaptured) ReleaseMouseCapture(); releasing = false;
        // Experimental tail: retain a nearly invisible input shield on this monitor for
        // the system double-click interval. Keyboard focus returns immediately. Rapid
        // intentional clicks are also swallowed, and must be evaluated by participants.
        canvas.Children.Clear(); Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); Cursor = Cursors.Arrow;
        Native.NoActivate(this);
        trial.Finish(this, target, rect, returnFocus, reason);
        if (!returnFocus || Source.IsClosed || !trial.IsForeground(Source)) { Dispose(); return; }
        tailDeadline = DateTime.UtcNow.AddMilliseconds(Native.GetDoubleClickTime()); tail.Start();
    }
    private void TailTick()
    {
        if (!Finishing || disposed) return;
        if (Source.IsClosed || !trial.IsForeground(Source)) { Dispose(); return; }
        if (DateTime.UtcNow >= tailDeadline && !Native.AnyButton) Dispose();
    }
    private void Cleanup()
    {
        poll.Stop(); tail.Stop(); CompositionTarget.Rendering -= Frame;
        if (trial.Active == this) trial.Active = null;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Finishing = true;
        trial.Record("layer-disposed", new { source = Source.Label, gesture, tailActive = tail.IsEnabled });
        Cleanup(); Close();
    }
}
