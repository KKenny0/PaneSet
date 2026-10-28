using System.IO;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static void CheckDragZones()
    {
        var area = new WindowRect(0, 0, 1920, 1040);
        void EqualZones(WindowRect[] expected, WindowRect[] actual)
        {
            Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++) Equal(expected[i], actual[i]);
        }
        Equal(2, LayoutEngine.Zones(area, ZoneTemplate.TwoColumns).Length);
        Equal(3, LayoutEngine.Zones(area, ZoneTemplate.ThreeColumns).Length);
        Equal(2, LayoutEngine.Zones(area, ZoneTemplate.MainAndSide).Length);
        Equal(2, LayoutEngine.Zones(area, ZoneTemplate.TwoRows).Length);
        Equal(4, LayoutEngine.Zones(area, ZoneTemplate.Grid).Length);
        EqualZones(LayoutEngine.Arrange(area, 2, ArrangementKind.Columns), LayoutEngine.Zones(area, ZoneTemplate.TwoColumns));
        EqualZones(LayoutEngine.Arrange(area, 3, ArrangementKind.Columns), LayoutEngine.Zones(area, ZoneTemplate.ThreeColumns));
        EqualZones(LayoutEngine.Arrange(area, 2, ArrangementKind.MainAndOthers), LayoutEngine.Zones(area, ZoneTemplate.MainAndSide));
        EqualZones(LayoutEngine.Arrange(area, 4, ArrangementKind.Grid), LayoutEngine.Zones(area, ZoneTemplate.Grid));

        // The main-and-side template keeps its 2/3 to 1/3 split on odd widths.
        var main = LayoutEngine.Zones(new WindowRect(0, 0, 1921, 1041), ZoneTemplate.MainAndSide);
        Equal((1921 - 12) * 2 / 3, main[0].Width);
        Equal(main[0].Width + main[1].Width + 12, 1921);

        // Two rows split vertically across the full width.
        var rows = LayoutEngine.Zones(area, ZoneTemplate.TwoRows);
        Equal(area.Width, rows[0].Width); Equal(area.Width, rows[1].Width);
        Equal(0, rows[0].Y - area.Y); Equal(true, rows[1].Y > rows[0].Y);

        foreach (var work in new[] { area, new WindowRect(0, 0, 1921, 1041), new WindowRect(-2560, -1440, 2559, 1399) })
            foreach (var template in Enum.GetValues<ZoneTemplate>())
            {
                var zones = LayoutEngine.Zones(work, template);
                Equal(ZoneTemplateCount(template), zones.Length);
                CheckGeometry(work, zones);
            }

        // Tiny work areas still produce positive rectangles; the spec has no minimum-size
        // pre-rejection for placement zones.
        foreach (var template in Enum.GetValues<ZoneTemplate>())
            foreach (var zone in LayoutEngine.Zones(new WindowRect(0, 0, 320, 200), template))
                Equal(true, zone.Width > 0 && zone.Height > 0);

        Equal(5, ZoneTemplateCatalog.Order.Distinct().Count());
        foreach (var template in ZoneTemplateCatalog.Order) Equal(true, ZoneTemplateCatalog.Label(template).Length > 0);

        var slots = DragThumbSlotLayout.For(area, 1);
        Equal(5, slots.Length);
        for (int i = 0; i < slots.Length; i++)
        {
            Equal(true, slots[i].Rect.X >= area.X && slots[i].Rect.Y >= area.Y);
            Equal(true, (long)slots[i].Rect.X + slots[i].Rect.Width <= (long)area.X + area.Width);
            if (i > 0) Equal(true, slots[i].Rect.X > slots[i - 1].Rect.X);
        }
        var scaled = DragThumbSlotLayout.For(area, 1.5);
        Equal(true, scaled[0].Rect.Width > slots[0].Rect.Width);
    }

    private static int ZoneTemplateCount(ZoneTemplate template) => template switch
    {
        ZoneTemplate.TwoColumns => 2, ZoneTemplate.ThreeColumns => 3, ZoneTemplate.MainAndSide => 2,
        ZoneTemplate.TwoRows => 2, _ => 4,
    };

    private static void CheckDragState()
    {
        var machine = new DragPlacementStateMachine();
        var work = new WindowRect(0, 0, 2000, 1000);
        var slots = DragThumbSlotLayout.For(work, 1);
        WindowRect[] Zones(ZoneTemplate template) => LayoutEngine.Zones(work, template);
        DragZoneView View(ZoneTemplate template) => new(work, Zones(template), slots);
        Native.Point At(int x, int y) => new() { X = x, Y = y };
        var dragStart = new WindowRect(60, 90, 460, 360);

        // Held modifier, hit zone, observed release: commits.
        machine.SetActiveTemplate(ZoneTemplate.TwoRows);
        var view = View(ZoneTemplate.TwoRows);
        machine.Start(leftDown: true, maximized: false, dragStart);
        machine.Sample(true, true, At(1500, 600), dragStart, view);
        Equal(true, machine.SessionEngaged);
        var commit = machine.ShouldCommit(view, leftReleaseObserved: true, leftDownNow: false);
        Equal(ZoneTemplate.TwoRows, commit!.Template);
        Equal(view.Zones[1], commit.Zone);

        // Modifier released before the drag ends: no placement.
        machine.Start(true, false, dragStart);
        machine.Sample(true, true, At(1500, 600), dragStart, view);
        machine.Sample(false, true, At(1500, 600), dragStart, view);
        Equal(false, machine.SessionEngaged);
        Equal(null, machine.ShouldCommit(view, true, false));

        // Escape cancels permanently, including the delayed-end case where the key is seen
        // after the button release but before the end event is delivered.
        machine.Start(true, false, dragStart);
        machine.Sample(true, true, At(1500, 600), dragStart, view);
        machine.ObserveCancel(escape: true, rightButton: false);
        Equal(false, machine.SessionEngaged);
        Equal(null, machine.ShouldCommit(view, true, false));

        // Right button during the drag cancels as well.
        machine.Start(true, false, dragStart);
        machine.Sample(true, true, At(1500, 600), dragStart, view);
        machine.ObserveCancel(false, true);
        Equal(null, machine.ShouldCommit(view, true, false));

        // A border resize never engages; a maximized window dragged off its caption may change
        // size and still engages.
        machine.Start(true, false, dragStart);
        machine.Sample(true, true, At(1500, 600), new WindowRect(60, 90, 700, 360), view);
        Equal(false, machine.SessionEngaged);
        machine.Start(true, true, new WindowRect(0, 0, 2560, 1400));
        machine.Sample(true, true, At(1500, 600), dragStart, view);
        Equal(true, machine.SessionEngaged);

        // Keyboard-driven moves (no button down at the start) never engage.
        machine.Start(leftDown: false, maximized: false, dragStart);
        machine.Sample(true, false, At(1500, 600), dragStart, view);
        Equal(false, machine.SessionEngaged);
        Equal(null, machine.ShouldCommit(view, true, false));

        // Uncertain endings never commit: the release was not observed, or the button is still
        // down when the loop ends (system cancelled the drag).
        machine.Start(true, false, dragStart);
        machine.Sample(true, true, At(1500, 600), dragStart, view);
        Equal(null, machine.ShouldCommit(view, leftReleaseObserved: false, leftDownNow: false));
        Equal(null, machine.ShouldCommit(view, leftReleaseObserved: true, leftDownNow: true));

        // No zone hit, no commit.
        machine.Start(true, false, new WindowRect(-4000, 3000, 460, 360));
        machine.Sample(true, true, At(-3800, 3200), new WindowRect(-4000, 3000, 460, 360), view);
        Equal(true, machine.SessionEngaged);
        Equal(null, machine.ShouldCommit(view, true, false));

        // Passing over a thumbnail slot switches the sticky template for the next samples.
        machine.Start(true, false, dragStart);
        var slotCenterX = (slots[2].Rect.X + slots[2].Rect.Width / 2, slots[2].Rect.Y + slots[2].Rect.Height / 2);
        machine.Sample(true, true, At(slotCenterX.Item1, slotCenterX.Item2), dragStart, view);
        Equal(ZoneTemplateCatalog.Order[2], machine.HoverTemplate(At(slotCenterX.Item1, slotCenterX.Item2), slots));
        Equal(ZoneTemplateCatalog.Order[2], machine.ActiveTemplate);
        Equal(null, machine.HoverTemplate(At(1500, 600), slots));
        Equal(ZoneTemplateCatalog.Order[2], machine.ActiveTemplate);

        // Zone hits follow the pointer across templates.
        var columns = View(ZoneTemplate.ThreeColumns);
        machine.Start(true, false, dragStart);
        machine.Sample(true, true, At(700, 500), dragStart, columns);
        Equal(1, machine.HitZoneIndex);
        machine.Sample(true, true, At(1800, 500), dragStart, columns);
        Equal(2, machine.HitZoneIndex);
    }

    private static void CheckDragSettings()
    {
        string path = Path.Combine(Path.GetTempPath(), "paneset-drag-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            // A settings file from the removed placement entry loads without losing anything else.
            File.WriteAllText(path, "{\"version\":1,\"shortcuts\":{\"openPanel\":{\"modifiers\":2,\"key\":32}," +
                "\"restore\":{\"modifiers\":2,\"key\":90},\"placeWindow\":{\"modifiers\":3,\"key\":51}},\"theme\":\"Dark\",\"dragModifiers\":1}");
            var settings = ShortcutSettings.Load(path, out string notice);
            Equal("", notice);
            Equal(AppearanceMode.Dark, settings.Theme);
            Equal(1u, settings.DragModifiers);
            Equal(new ShortcutBinding(2, 32), settings.For(ShortcutAction.OpenPanel));
            Equal(new ShortcutBinding(2, 90), settings.For(ShortcutAction.Restore));
            Equal(2, settings.Shortcuts.Count);
            settings.Save(path);
            var reloaded = ShortcutSettings.Load(path, out notice);
            Equal("", notice);
            Equal(1u, reloaded.DragModifiers);
            Equal(AppearanceMode.Dark, reloaded.Theme);
            Equal(false, reloaded.Shortcuts.ContainsKey(default)); // unknown keys never resurrect

            // Fresh file: default modifier is Shift, and the action set no longer includes placement.
            File.Delete(path);
            var fresh = ShortcutSettings.Load(path, out _);
            Equal(ShortcutSettings.DefaultDragModifiers, fresh.DragModifiers);
            Equal(4, Enum.GetValues<ShortcutAction>().Length);

            // Invalid modifier masks fall back to defaults with a notice.
            File.WriteAllText(path, "{\"version\":1,\"shortcuts\":{},\"dragModifiers\":0}");
            var invalid = ShortcutSettings.Load(path, out string warning);
            Equal(true, warning.Length > 0);
            Equal(ShortcutSettings.DefaultDragModifiers, invalid.DragModifiers);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
