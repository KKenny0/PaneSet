namespace PaneSet;

// One B session. Native observations enter as values; no window operations occur here.
internal sealed class OverviewSelection
{
    internal sealed class Item(OverviewSelection owner, int index)
    {
        public WindowTarget Target => owner.targets[index];
        public int Order => owner.selected.IndexOf(this) + 1;
        public bool Selected => Order > 0;
        public bool IsValid => owner.valid[index];
    }

    internal sealed record Preview(bool CanConfirm, IReadOnlyList<WindowRect> Areas, string Message);

    private readonly WindowRect workArea;
    private readonly WindowTarget[] targets;
    private readonly bool[] valid;
    private readonly List<Item> selected = [];
    public IReadOnlyList<Item> Candidates { get; }
    public IReadOnlyList<Item> Selected { get; }

    public OverviewSelection(IEnumerable<(WindowTarget Target, WindowRect Bounds)> candidates, nint foreground, WindowRect workArea)
    {
        this.workArea = workArea;
        targets = candidates.Select((value, index) => (value.Target, value.Bounds, index))
            .OrderBy(x => x.Target.Handle == foreground ? 0 : 1)
            .ThenBy(x => x.Bounds.Y).ThenBy(x => x.Bounds.X).ThenBy(x => x.index)
            .Select(x => x.Target).ToArray();
        valid = Enumerable.Repeat(true, targets.Length).ToArray();
        Candidates = Array.AsReadOnly(targets.Select((_, index) => new Item(this, index)).ToArray());
        Selected = selected.AsReadOnly();
        if (Candidates.FirstOrDefault(i => i.Target.Handle == foreground) is { } first) selected.Add(first);
    }

    public void SetSelected(Item item, bool value)
    {
        if (!Candidates.Contains(item)) throw new ArgumentException("窗口不属于本次选择。", nameof(item));
        if (value) { if (item.IsValid && !item.Selected) selected.Add(item); }
        else selected.Remove(item);
    }

    public Preview Evaluate(ArrangementKind kind)
    {
        bool invalid = selected.Any(i => !i.IsValid);
        string message = invalid ? "成员已失效，请取消该成员或等待其恢复。" : selected.Count < 2
            ? "至少选择两扇窗口。" : "编号对应顺序。Enter 确认，Esc 取消。";
        try
        {
            var areas = LayoutEngine.Arrange(workArea, Math.Max(2, selected.Count), kind);
            bool fits = areas.All(r => r.Width >= 240 && r.Height >= 160);
            if (selected.Count >= 2 && !fits) message = "目标窗口尺寸过小，请减少选择或改用网格。";
            return new(selected.Count >= 2 && !invalid && fits,
                Array.AsReadOnly(invalid ? [] : areas.Take(selected.Count).ToArray()), message);
        }
        catch (ArgumentOutOfRangeException e) { return new(false, Array.Empty<WindowRect>(), e.Message); }
    }

    public IReadOnlyList<Item> Filter(string query) => Candidates.Where(i =>
        (i.Target.Title + " " + i.Target.Application).Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToArray();

    public void Refresh(IReadOnlyList<WindowTarget> current)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var value = current.FirstOrDefault(t => SameIdentity(targets[i], t));
            valid[i] = value != null;
            if (value != null) targets[i] = value;
        }
    }

    private static bool SameIdentity(WindowTarget a, WindowTarget b) => a.Handle == b.Handle && a.Generation == b.Generation
        && a.ProcessId == b.ProcessId && a.ThreadId == b.ThreadId && a.Started == b.Started && a.ClassName == b.ClassName;

    public void Move(Item item, int delta)
    {
        int index = selected.IndexOf(item);
        long next = (long)index + delta;
        if (index < 0 || next < 0 || next >= selected.Count) return;
        selected.RemoveAt(index); selected.Insert((int)next, item);
    }
}
