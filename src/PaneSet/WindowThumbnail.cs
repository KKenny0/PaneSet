using System.Windows;

namespace PaneSet;

// Owns only a DWM drawing relationship; never changes the source window.
internal sealed class WindowThumbnail : IDisposable
{
    private nint handle;
    public bool Available => handle != 0;
    public WindowThumbnail(nint destination, nint source)
    {
        if (Native.DwmRegisterThumbnail(destination, source, out var registered) == 0) handle = registered;
    }

    public bool Update(Rect slot, Rect viewport, bool visible)
    {
        if (handle == 0) return false;
        var properties = new Native.ThumbnailProperties { Flags = 8, Visible = false };
        if (visible && slot.Width > 0 && slot.Height > 0)
        {
            if (Native.DwmQueryThumbnailSourceSize(handle, out var size) != 0 || size.Width <= 0 || size.Height <= 0)
            { Dispose(); return false; }
            double scale = Math.Min(slot.Width / size.Width, slot.Height / size.Height);
            var destination = new Rect(slot.X + (slot.Width - size.Width * scale) / 2,
                slot.Y + (slot.Height - size.Height * scale) / 2, size.Width * scale, size.Height * scale);
            var clipped = Rect.Intersect(destination, viewport);
            if (!clipped.IsEmpty && clipped.Width >= 1 && clipped.Height >= 1)
            {
                properties = new Native.ThumbnailProperties {
                    Flags = 1 | 2 | 4 | 8 | 16, Visible = true, Opacity = 255, SourceClientAreaOnly = false,
                    Destination = ToRect(clipped),
                    Source = ToRect(new Rect((clipped.X - destination.X) / scale, (clipped.Y - destination.Y) / scale,
                        clipped.Width / scale, clipped.Height / scale)) };
            }
        }
        if (Native.DwmUpdateThumbnailProperties(handle, ref properties) == 0) return true;
        Dispose(); return false;
    }
    private static Native.Rect ToRect(Rect value) => new() {
        Left = (int)Math.Round(value.Left), Top = (int)Math.Round(value.Top),
        Right = (int)Math.Round(value.Right), Bottom = (int)Math.Round(value.Bottom) };
    public void Dispose()
    {
        if (handle == 0) return;
        int result = Native.DwmUnregisterThumbnail(handle);
        if (result != 0) System.Diagnostics.Trace.WriteLine($"DwmUnregisterThumbnail failed: 0x{result:X8}");
        handle = 0;
    }
}
