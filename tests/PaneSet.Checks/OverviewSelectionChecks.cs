using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static WindowTarget SelectionTarget(int id) => new((nint)id, 10, 20, 30, "Fixture", id, "Editor", "窗口 " + id);

    private static Task CheckOverviewSelectionRefresh()
    {
        var targets = Enumerable.Range(1, 3).Select(SelectionTarget).ToArray();
        var group = new OverviewSelection(targets.Select(t => (t, new WindowRect(0, 0, 300, 300))),
            targets[0].Handle, new WindowRect(0, 0, 1920, 1040));
        var two = group.Candidates[1];
        group.SetSelected(two, true);
        group.Refresh([targets[0], targets[2], SelectionTarget(4)]);
        Equal(false, two.IsValid);
        Equal("1,2", string.Join(',', group.Selected.Select(i => i.Target.Handle)));
        group.SetSelected(two, false); group.SetSelected(two, true);
        Equal(false, two.Selected);
        group.Refresh(targets);
        Equal(true, two.IsValid); Equal(false, two.Selected);
        group.SetSelected(group.Candidates[2], true); group.SetSelected(two, true);
        Equal(3, two.Order);
        foreach (var impostor in new[] { targets[1] with { Generation = 999 }, targets[1] with { ProcessId = 999 },
            targets[1] with { ThreadId = 999 }, targets[1] with { Started = 999 }, targets[1] with { ClassName = "Other" },
            targets[1] with { Handle = 999 } })
        {
            group.Refresh([targets[0], impostor, targets[2]]);
            Equal(false, two.IsValid); Equal(true, two.Selected); Equal(3, two.Order);
        }
        group.Refresh([targets[0], targets[1] with { Title = "修订稿", Application = "Writer" }, targets[2]]);
        Equal(true, two.IsValid); Equal(3, two.Order);
        Equal("修订稿", two.Target.Title);
        Equal("1,2,3", string.Join(',', group.Candidates.Select(i => i.Target.Handle)));
        Equal("2", string.Join(',', group.Filter("修订").Select(i => i.Target.Handle)));
        Equal("2", string.Join(',', group.Filter(" writer ").Select(i => i.Target.Handle)));
        Equal(0, group.Filter("不存在").Count);
        Equal("1,3,2", string.Join(',', group.Selected.Select(i => i.Target.Handle)));
        return Task.CompletedTask;
    }

    private static Task CheckSelectionEligibility()
    {
        var targets = Enumerable.Range(1, 4).Select(SelectionTarget).ToArray();
        var group = new OverviewSelection(targets.Select(t => (t, new WindowRect(0, 0, 300, 300))),
            0, new WindowRect(0, 0, 1000, 800));
        Equal(false, group.Evaluate(ArrangementKind.Automatic).CanConfirm);
        group.SetSelected(group.Candidates[0], true);
        Equal(false, group.Evaluate(ArrangementKind.Automatic).CanConfirm);
        group.SetSelected(group.Candidates[1], true);
        var preview = group.Evaluate(ArrangementKind.Columns);
        Equal(true, preview.CanConfirm);
        Equal(new WindowRect(0, 0, 494, 800), preview.Areas[0]);
        Equal(new WindowRect(506, 0, 494, 800), preview.Areas[1]);
        group.Refresh([targets[0], targets[2], targets[3]]);
        Equal(false, group.Evaluate(ArrangementKind.Columns).CanConfirm);
        Equal(0, group.Evaluate(ArrangementKind.Columns).Areas.Count);
        group.Refresh(targets);
        Equal(true, group.Evaluate(ArrangementKind.Columns).CanConfirm);
        Equal(2, group.Selected[1].Order);
        group.SetSelected(group.Candidates[2], true); group.SetSelected(group.Candidates[3], true);
        Equal(true, group.Evaluate(ArrangementKind.Columns).CanConfirm); // Four columns: 241 pixels each.
        var small = new OverviewSelection(targets.Select(t => (t, new WindowRect())), 0, new WindowRect(0, 0, 999, 800));
        foreach (var item in small.Candidates) small.SetSelected(item, true);
        Equal(true, small.Evaluate(ArrangementKind.Columns).CanConfirm);
        var narrow = new OverviewSelection(targets.Select(t => (t, new WindowRect())), 0, new WindowRect(0, 0, 995, 800));
        foreach (var item in narrow.Candidates) narrow.SetSelected(item, true);
        Equal(false, narrow.Evaluate(ArrangementKind.Columns).CanConfirm);
        Equal(true, narrow.Evaluate(ArrangementKind.Grid).CanConfirm);
        foreach (int height in new[] { 159, 160 })
        {
            var shortGroup = new OverviewSelection(targets.Take(2).Select(t => (t, new WindowRect())), 0, new WindowRect(0, 0, 492, height));
            foreach (var item in shortGroup.Candidates) shortGroup.SetSelected(item, true);
            Equal(height == 160, shortGroup.Evaluate(ArrangementKind.Columns).CanConfirm);
        }
        var crowded = new OverviewSelection(targets.Select(t => (t, new WindowRect())), 0, new WindowRect(0, 0, 10, 10));
        foreach (var item in crowded.Candidates) crowded.SetSelected(item, true);
        Equal(false, crowded.Evaluate(ArrangementKind.Grid).CanConfirm);
        Equal(0, crowded.Evaluate(ArrangementKind.Grid).Areas.Count);
        return Task.CompletedTask;
    }

    private static Task CheckSelectionOrder()
    {
        var targets = Enumerable.Range(1, 5).Select(SelectionTarget).ToArray();
        WindowRect[] positions = [new(100, 0, 300, 300), new(0, 100, 300, 300), new(500, 500, 300, 300),
            new(100, 0, 300, 300), new(-100, 0, 300, 300)];
        var group = new OverviewSelection(targets.Select((t, i) => (t, positions[i])),
            targets[2].Handle, new WindowRect(0, 0, 1920, 1040));
        Equal("3,5,1,4,2", string.Join(',', group.Candidates.Select(i => i.Target.Handle)));
        var one = group.Candidates.Single(i => i.Target.Handle == 1);
        var two = group.Candidates.Single(i => i.Target.Handle == 2);
        group.SetSelected(one, true); group.SetSelected(two, true); group.SetSelected(two, true);
        group.Move(two, -1);
        Equal("3,2,1", string.Join(',', group.Selected.Select(i => i.Target.Handle)));
        group.SetSelected(two, false); group.SetSelected(two, true);
        Equal("3,1,2", string.Join(',', group.Selected.Select(i => i.Target.Handle)));
        Equal("1,2,3", string.Join(',', group.Selected.Select(i => i.Order)));
        group.Move(group.Selected[0], -1); group.Move(two, 1);
        Equal("3,1,2", string.Join(',', group.Selected.Select(i => i.Target.Handle)));
        Equal("3,5,1,4,2", string.Join(',', group.Candidates.Select(i => i.Target.Handle)));
        return Task.CompletedTask;
    }
}
