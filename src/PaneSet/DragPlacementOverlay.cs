using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace PaneSet;

/// Purely visual, click-through topmost layer drawn over one screen's work area during a drag
/// placement session. It never takes focus or input; every change is driven by pointer position.
internal sealed class DragPlacementOverlay : IDisposable
{
    private HwndSource? source;
    private Canvas canvas = new();
    private WindowRect workArea;
    private double scale = 1;
    private (WindowRect Work, WindowRect[] Zones, DragThumbSlot[] Slots, ZoneTemplate Active, int Hit, bool Engaged) last;

    public bool Visible => source != null;
    public double Scale => scale;

    public void Show(WindowRect area)
    {
        if (source != null && area == workArea) return;
        workArea = area;
        source?.Dispose();
        source = null;
        try
        {
            source = new HwndSource(new HwndSourceParameters("PaneSet drag placement overlay")
            {
                PositionX = area.X, PositionY = area.Y, Width = Math.Max(1, area.Width), Height = Math.Max(1, area.Height),
                WindowStyle = unchecked((int)0x80000000),
                // NOACTIVATE | LAYERED | TOOLWINDOW | TRANSPARENT | TOPMOST, like the placement feedback layer.
                ExtendedWindowStyle = 0x080800A8,
                UsesPerPixelOpacity = true
            });
            source.AddHook(Receive);
            if (!Native.SetProp(source.Handle, Native.PlacementFeedbackProperty, 1)) throw new Win32Exception();
            uint dpi = Native.GetDpiForWindow(source.Handle);
            if (dpi == 0) throw new InvalidOperationException("无法读取投放层缩放。");
            scale = dpi / 96d;
            canvas = new Canvas { Width = area.Width / scale, Height = area.Height / scale, IsHitTestVisible = false };
            source.RootVisual = canvas;
            if (source.CompositionTarget != null) source.CompositionTarget.BackgroundColor = Colors.Transparent;
            // The popup is created hidden; HWND_TOPMOST plus SWP_SHOWWINDOW actually displays it.
            if (!Native.SetWindowPos(source.Handle, -1, area.X, area.Y, area.Width, area.Height, 0x50))
                throw new System.ComponentModel.Win32Exception();
            last = default;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Update(DragZoneView view, ZoneTemplate active, int hitZone, bool engaged)
    {
        if (source == null) return;
        if (last.Work == view.WorkArea && last.Active == active && last.Hit == hitZone && last.Engaged == engaged
            && last.Zones.AsSpan().SequenceEqual(view.Zones) && SlotsEqual(last.Slots, view.Slots)) return;
        last = (view.WorkArea, view.Zones, view.Slots, active, hitZone, engaged);
        canvas.Children.Clear();
        var work = view.WorkArea;
        for (int i = 0; i < view.Zones.Length; i++)
        {
            var zone = view.Zones[i];
            var hovered = engaged && i == hitZone;
            var border = new Border
            {
                Width = zone.Width / scale, Height = zone.Height / scale,
                BorderThickness = new Thickness((hovered ? 3 : 2) / scale),
                CornerRadius = new CornerRadius(4 / scale),
                Background = hovered ? Semi(UiPalette.Focus, 52) : Brushes.Transparent,
                BorderBrush = hovered ? UiPalette.Focus : Semi(UiPalette.Focus, 150),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(border, (zone.X - work.X) / scale);
            Canvas.SetTop(border, (zone.Y - work.Y) / scale);
            canvas.Children.Add(border);
        }
        if (view.Slots.Length > 0)
        {
            var strip = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
            foreach (var slot in view.Slots)
            {
                bool isActive = slot.Template == active;
                strip.Children.Add(new Border
                {
                    Width = (slot.Rect.Width - 8) / scale, Height = (slot.Rect.Height - 8) / scale,
                    Margin = new Thickness(4 / scale),
                    BorderThickness = new Thickness((isActive ? 2.5 : 1) / scale),
                    CornerRadius = new CornerRadius(4 / scale),
                    BorderBrush = isActive ? UiPalette.Focus : Semi(UiPalette.Ink, 120),
                    Background = isActive ? Semi(UiPalette.Focus, 44) : Semi(UiPalette.Surface, 170),
                    // The miniature is drawn on a small fixed canvas and scaled to fill the slot.
                    Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Child = Miniature(slot.Template) }
                });
            }
            Canvas.SetLeft(strip, (view.Slots[0].Rect.X - work.X) / scale);
            Canvas.SetTop(strip, (view.Slots[0].Rect.Y - work.Y) / scale);
            canvas.Children.Add(strip);
        }
    }

    private static FrameworkElement Miniature(ZoneTemplate template)
    {
        // Fixed virtual canvas; the surrounding Viewbox scales it to fill the thumbnail slot.
        var host = new Canvas { Width = 48, Height = 30, IsHitTestVisible = false };
        var work = new WindowRect(0, 0, 96, 60);
        WindowRect[] zones;
        try { zones = LayoutEngine.Zones(work, template, 4); }
        catch (ArgumentOutOfRangeException) { zones = [work]; }
        foreach (var zone in zones)
        {
            var cell = new Border
            {
                Width = zone.Width / 2d, Height = zone.Height / 2d,
                BorderThickness = new Thickness(1), BorderBrush = Semi(UiPalette.Ink, 150),
                CornerRadius = new CornerRadius(1)
            };
            Canvas.SetLeft(cell, zone.X / 2d); Canvas.SetTop(cell, zone.Y / 2d);
            host.Children.Add(cell);
        }
        return host;
    }

    private static bool SlotsEqual(DragThumbSlot[] a, DragThumbSlot[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static Brush Semi(Brush brush, byte alpha)
    {
        var color = ((SolidColorBrush)brush).Color;
        var semi = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        semi.Freeze();
        return semi;
    }

    private nint Receive(nint window, int message, nint key, nint data, ref bool handled)
    {
        if (message == 0x21) { handled = true; return 3; }
        if (message == 0x84) { handled = true; return -1; }
        return 0;
    }

    public void Dispose()
    {
        if (source == null) return;
        source.RemoveHook(Receive);
        source.Dispose();
        source = null;
    }
}
