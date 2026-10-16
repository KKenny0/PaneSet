using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace PaneSet;

// Physical screen coordinates share one scale, including the work-area origin.
internal sealed class ReferencePositionPreview : Canvas
{
    private readonly Border screen = new() { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3) };
    private readonly Border region = new() { BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(2) };
    private WindowRect? work, bounds;

    public ReferencePositionPreview()
    {
        Height = 180; IsHitTestVisible = false; Focusable = false;
        AutomationProperties.SetName(this, "单窗放置位置预览");
        AutomationProperties.SetName(screen, "屏幕工作区"); AutomationProperties.SetName(region, "放置目标区域");
        screen.SetResourceReference(BackgroundProperty, "Preview"); screen.SetResourceReference(Border.BorderBrushProperty, "Muted");
        region.SetResourceReference(BackgroundProperty, "PreviewPane"); region.SetResourceReference(Border.BorderBrushProperty, "Focus");
        Children.Add(screen); Children.Add(region); Clear();
        SizeChanged += (_, _) => DrawPosition();
    }

    public void ShowPosition(WindowRect area, WindowRect visibleBounds)
    {
        work = area; bounds = visibleBounds; DrawPosition();
    }
    public void Clear()
    {
        work = bounds = null; screen.Visibility = region.Visibility = Visibility.Collapsed;
    }
    private void DrawPosition()
    {
        if (work is not { } area || bounds is not { } target || area.Width <= 0 || area.Height <= 0 || target.Width <= 0 || target.Height <= 0
            || target.X < area.X || target.Y < area.Y || (double)target.X + target.Width > (double)area.X + area.Width
            || (double)target.Y + target.Height > (double)area.Y + area.Height || ActualWidth <= 8 || ActualHeight <= 8)
        { screen.Visibility = region.Visibility = Visibility.Collapsed; return; }
        double scale = Math.Min((ActualWidth - 8) / area.Width, (ActualHeight - 8) / area.Height);
        double width = area.Width * scale, height = area.Height * scale;
        double x = (ActualWidth - width) / 2, y = (ActualHeight - height) / 2;
        screen.Width = width; screen.Height = height; SetLeft(screen, x); SetTop(screen, y);
        region.Width = target.Width * scale; region.Height = target.Height * scale;
        SetLeft(region, x + ((double)target.X - area.X) * scale); SetTop(region, y + ((double)target.Y - area.Y) * scale);
        screen.Visibility = region.Visibility = Visibility.Visible;
    }
}
