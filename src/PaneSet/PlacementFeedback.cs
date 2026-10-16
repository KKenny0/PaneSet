using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PaneSet;

internal sealed class PlacementFeedback : IDisposable
{
    private HwndSource? source;
    private DispatcherTimer? timer;

    public PlacementFeedback(DisplayArea display, WindowRect? target, string message = "")
    {
        var work = display.WorkArea;
        try
        {
            source = new HwndSource(new HwndSourceParameters("PaneSet placement feedback")
            {
                PositionX = work.X, PositionY = work.Y, Width = 1, Height = 1,
                WindowStyle = unchecked((int)0x80000000),
                // NOACTIVATE | LAYERED | TOOLWINDOW | TRANSPARENT | TOPMOST.
                ExtendedWindowStyle = 0x080800A8,
                UsesPerPixelOpacity = true
            });
            source.AddHook(Receive);
            if (!Native.SetProp(source.Handle, Native.PlacementFeedbackProperty, 1)) throw new Win32Exception();
            uint dpi = Native.GetDpiForWindow(source.Handle);
            if (dpi == 0) throw new InvalidOperationException("无法读取反馈层缩放。");
            double scale = dpi / 96d;
            var border = new Border { SnapsToDevicePixels = true, Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(UiPalette.IsDark ? Color.FromRgb(22, 43, 51) : Colors.White),
                BorderThickness = new Thickness(1 / scale), CornerRadius = new CornerRadius(3 / scale),
                Child = new Border { BorderBrush = UiPalette.Focus, BorderThickness = new Thickness(2 / scale),
                    Background = Brushes.Transparent, CornerRadius = new CornerRadius(2 / scale) },
                IsHitTestVisible = false, Focusable = false };
            WindowRect bounds;
            if (target is { } rect) bounds = rect;
            else
            {
                double maxWidth = Math.Max(1, Math.Min(440, work.Width / scale - 48));
                var label = new TextBlock { Text = message, FontSize = 14, Foreground = UiPalette.Ink,
                    TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = maxWidth, FontFamily = new FontFamily("Microsoft YaHei UI") };
                border.BorderBrush = UiPalette.Line; border.BorderThickness = new Thickness(1);
                border.CornerRadius = new CornerRadius(8); border.Background = UiPalette.Surface;
                border.Padding = new Thickness(16, 12, 16, 12); border.Child = label;
                border.Measure(new Size(Math.Max(1, work.Width / scale - 24), double.PositiveInfinity));
                int width = Math.Min(work.Width, (int)Math.Ceiling(border.DesiredSize.Width * scale));
                int height = Math.Min(work.Height, (int)Math.Ceiling(border.DesiredSize.Height * scale));
                bounds = new(work.X + (work.Width - width) / 2, Math.Max(work.Y, work.Y + work.Height - height - (int)(24 * scale)), width, height);
            }
            border.Width = bounds.Width / scale; border.Height = bounds.Height / scale;
            source.RootVisual = border;
            if (source.CompositionTarget != null) source.CompositionTarget.BackgroundColor = Colors.Transparent;
            if (!Native.SetWindowPos(source.Handle, -1, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x50)) throw new Win32Exception();
            timer = new DispatcherTimer(DispatcherPriority.Background, source.Dispatcher)
            { Interval = TimeSpan.FromMilliseconds(target == null ? 4500 : 950) };
            timer.Tick += (_, _) => Dispose(); timer.Start();
        }
        catch { Dispose(); throw; }
    }

    private nint Receive(nint window, int message, nint key, nint data, ref bool handled)
    {
        if (message == 0x21) { handled = true; return 3; }
        if (message == 0x84) { handled = true; return -1; }
        return 0;
    }

    public void Dispose()
    {
        timer?.Stop(); timer = null;
        if (source == null) return;
        source.RemoveHook(Receive); source.Dispose(); source = null;
    }
}
