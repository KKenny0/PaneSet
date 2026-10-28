namespace PaneSet;

public readonly record struct WindowRect(int X, int Y, int Width, int Height);
public enum ArrangementKind { Automatic, Columns, MainAndOthers, Grid }
public enum ZoneTemplate { TwoColumns, ThreeColumns, MainAndSide, TwoRows, Grid }

public static class LayoutEngine
{
    public static WindowRect[] TwoColumns(WindowRect workArea, int gap = 12) => Arrange(workArea, 2, ArrangementKind.Columns, gap);

    public static WindowRect[] Zones(WindowRect area, ZoneTemplate template, int gap = 12)
    {
        if (!Enum.IsDefined(template)) throw new ArgumentOutOfRangeException(nameof(template));
        return template switch
        {
            ZoneTemplate.TwoColumns => Arrange(area, 2, ArrangementKind.Columns, gap),
            ZoneTemplate.ThreeColumns => Arrange(area, 3, ArrangementKind.Columns, gap),
            ZoneTemplate.MainAndSide => Arrange(area, 2, ArrangementKind.MainAndOthers, gap),
            ZoneTemplate.TwoRows => Rows(area, 2, gap),
            _ => Arrange(area, 4, ArrangementKind.Grid, gap),
        };
    }

    private static WindowRect[] Rows(WindowRect area, int count, int gap)
    {
        if (count < 1 || gap < 0 || area.Width <= 0 || area.Height <= 0) throw new ArgumentOutOfRangeException(nameof(area), "工作区域无法容纳所选分区。");
        var result = new List<WindowRect>();
        for (int i = 0; i < count; i++)
        {
            var (top, height) = Partition(area.Height, count, i, gap);
            result.Add(new(area.X, checked(area.Y + top), area.Width, height));
        }
        if (result.Any(r => r.Width <= 0 || r.Height <= 0)) throw new ArgumentOutOfRangeException(nameof(count), "工作区域过小，无法生成分区。");
        return result.ToArray();
    }

    public static WindowRect[] Arrange(WindowRect area, int count, ArrangementKind kind = ArrangementKind.Automatic, int gap = 12)
    {
        if (count < 2 || gap < 0 || area.Width <= 0 || area.Height <= 0
            || (long)area.X + area.Width > int.MaxValue || (long)area.Y + area.Height > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(area), "工作区域无法容纳所选窗口。");
        if (kind == ArrangementKind.Automatic) kind = count <= 3 ? ArrangementKind.Columns : ArrangementKind.Grid;
        var result = new List<WindowRect>();
        if (kind == ArrangementKind.MainAndOthers)
        {
            int mainWidth = (int)(((long)area.Width - gap) * 2 / 3);
            result.Add(new(area.X, area.Y, mainWidth, area.Height));
            int sideWidth = area.Width - gap - mainWidth;
            for (int i = 0; i < count - 1; i++)
            {
                var (top, height) = Partition(area.Height, count - 1, i, gap);
                result.Add(new(checked(area.X + mainWidth + gap), checked(area.Y + top), sideWidth, height));
            }
        }
        else
        {
            int columns = kind == ArrangementKind.Grid ? (int)Math.Ceiling(Math.Sqrt(count)) : count;
            int rows = (count + columns - 1) / columns;
            for (int i = 0; i < count; i++)
            {
                var (left, width) = Partition(area.Width, columns, i % columns, gap);
                var (top, height) = Partition(area.Height, rows, i / columns, gap);
                result.Add(new(checked(area.X + left), checked(area.Y + top), width, height));
            }
        }
        if (result.Any(r => r.Width <= 0 || r.Height <= 0)) throw new ArgumentOutOfRangeException(nameof(count), "窗口太多，请减少选择或改用网格。");
        return result.ToArray();
    }

    private static (int Start, int Size) Partition(int extent, int parts, int index, int gap)
    {
        long available = extent - (long)gap * (parts - 1);
        if (available < parts) throw new ArgumentOutOfRangeException(nameof(parts), "窗口太多，请减少选择或改用网格。");
        int start = (int)(available * index / parts + (long)gap * index);
        int end = (int)(available * (index + 1) / parts + (long)gap * index);
        return (start, end - start);
    }
}
