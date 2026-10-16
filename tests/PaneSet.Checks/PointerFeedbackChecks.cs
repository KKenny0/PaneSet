using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static Task CheckPointerFeedback() => CheckPointerFeedbackControl(false);
    private static Task CheckPointerFeedbackCrossProcess() => CheckPointerFeedbackControl(true);
    private static async Task CheckPointerFeedbackControl(bool crossProcess)
    {
        var display = WindowManager.ListDisplays()[0];
        var work = display.WorkArea;
        using var environment = new PlacementEnvironment();
        await using var fixture = crossProcess ? await Fixture.Start(1) : null;
        using var control = crossProcess ? null : new HwndSource(new HwndSourceParameters("PaneSet feedback input control")
        {
            PositionX = work.X + 120, PositionY = work.Y + 120, Width = 420, Height = 300,
            WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x08000088
        }) { RootVisual = new Border { Background = Brushes.White } };
        nint underneath = fixture?.Handles.Single() ?? control!.Handle;
        if (fixture != null) RequireFixtureTarget(fixture, underneath);
        Equal(true, Native.SetWindowPos(underneath, -1, work.X + 120, work.Y + 120, 420, 300, 0x50));
        await Task.Delay(100);
        var target = Native.Bounds(underneath);
        if (Native.GetAncestor(Native.WindowFromPoint(new Native.Point { X = target.X + 1, Y = target.Y + 40 }), 2) != underneath)
            throw new UnavailableException("Feedback " + (crossProcess ? "cross-process" : "same-process") + " control is obscured; no input sent.");
        bool dark = UiPalette.IsDark;
        try
        {
            foreach (bool theme in new[] { false, true })
            {
                UiPalette.Apply(theme);
                nint foreground = Native.GetForegroundWindow();
                using (var feedback = new PlacementFeedback(display, target))
                {
                    Equal(foreground, Native.GetForegroundWindow());
                    await Task.Delay(120);
                    var handle = FeedbackHandles().Single();
                    Equal(true, Native.GetWindowRect(handle, out var rect)); Equal(target, rect.Value);
                    foreach (var point in new[] { new Native.Point { X = target.X + 1, Y = target.Y + 40 }, new Native.Point { X = target.X + 60, Y = target.Y + 80 } })
                    {
                        Equal(underneath, Native.GetAncestor(Native.WindowFromPoint(point), 2));
                        Equal(underneath, environment.WindowAtPoint(point));
                    }
                    SaveFeedback(handle, theme ? "paneset-pointer-target-dark" : "paneset-pointer-target-light");
                    foreground = Native.GetForegroundWindow();
                }
                Equal(foreground, Native.GetForegroundWindow()); Equal(0, FeedbackHandles().Count);
                using (var hint = new PlacementFeedback(display, null, "请将鼠标移到另一扇可见、可调整大小的窗口，再按放置键。"))
                {
                    Equal(foreground, Native.GetForegroundWindow());
                    await Task.Delay(120);
                    var handle = FeedbackHandles().Single();
                    Equal(true, Native.GetWindowRect(handle, out var hintRect));
                    Equal(true, hintRect.Left >= work.X && hintRect.Top >= work.Y
                        && hintRect.Right <= (long)work.X + work.Width && hintRect.Bottom <= (long)work.Y + work.Height);
                    SaveFeedback(handle, theme ? "paneset-pointer-hint-dark" : "paneset-pointer-hint-light");
                    foreground = Native.GetForegroundWindow();
                }
                Equal(foreground, Native.GetForegroundWindow()); Equal(0, FeedbackHandles().Count);
            }
            using (var feedback = new PlacementFeedback(display, target)) await Until(() => FeedbackHandles().Count == 0);
            Console.WriteLine("POINTER FEEDBACK: actual native foreground unchanged on show/dispose; opaque-border and interior point queries pass through to controlled " + (crossProcess ? "cross-process" : "same-process") + " HWND; target/hint in both themes rendered; timed release verified. No physical key/click acceptance.");
        }
        finally { UiPalette.Apply(dark); }
    }

    private static List<nint> FeedbackHandles()
    {
        var handles = new List<nint>();
        Native.EnumWindows((h, _) =>
        {
            Native.GetWindowThreadProcessId(h, out uint pid);
            if (pid == Environment.ProcessId && Native.GetProp(h, Native.PlacementFeedbackProperty) == 1) handles.Add(h);
            return true;
        }, 0);
        return handles;
    }

    private static void SaveFeedback(nint handle, string name)
    {
        var visual = (FrameworkElement)HwndSource.FromHwnd(handle).RootVisual;
        visual.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(visual);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(visual.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory("artifacts");
        using var output = File.Create("artifacts/" + name + ".png"); png.Save(output);
    }
}
