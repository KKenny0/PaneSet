namespace PaneSet;

public readonly record struct WindowRect(int X, int Y, int Width, int Height);

public static class LayoutEngine
{
    public static WindowRect[] TwoColumns(WindowRect workArea, int gap = 12)
    {
        if (gap < 0 || workArea.Width < gap + 2L || workArea.Height <= 0
            || (long)workArea.X + workArea.Width > int.MaxValue || (long)workArea.Y + workArea.Height > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(workArea), "The work area cannot fit two windows.");
        int leftWidth = (workArea.Width - gap) / 2;
        return [new(workArea.X, workArea.Y, leftWidth, workArea.Height),
            new(checked(workArea.X + leftWidth + gap), workArea.Y, workArea.Width - gap - leftWidth, workArea.Height)];
    }
}
