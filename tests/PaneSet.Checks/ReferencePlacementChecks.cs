using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckReferencePlacement()
    {
        await using var fixture = await Fixture.Start(3);
        using var manager = new WindowManager(new PlacementObservations());
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var before = targets.Select(manager.ReadState).ToArray();
        Equal<LayoutPositions?>(null, manager.RecentPositions);
        PlacementSucceeded(await PlacementResult(manager, manager.PlaceAtReferenceAsync(targets[2], targets[0])));
        Equal(new WindowStateInfo(before[0].Bounds, false), manager.ReadState(targets[2]));
        Equal(before[0], manager.ReadState(targets[0])); Equal(before[1], manager.ReadState(targets[1]));
        Equal<LayoutPositions?>(null, manager.RecentPositions);
        var restored = await PlacementResult(manager, manager.UndoAsync());
        PlacementSucceeded(restored); Equal(1, restored.Windows.Count); Equal(targets[2], restored.Windows.Single().Target);
        for (int i = 0; i < targets.Length; i++) Equal(before[i], manager.ReadState(targets[i]));
        Equal(false, manager.HasUndo);
        var display = WindowManager.DisplayFor(targets[0].Handle);
        var other = WindowManager.ListDisplays().FirstOrDefault(d => d.Handle != display.Handle);
        if (other != null)
        {
            await fixture.Send($"position 0 {other.WorkArea.X + 100} {other.WorkArea.Y + 100}");
            var reference = manager.ReadState(targets[0]);
            foreach (bool maximized in new[] { false, true })
            {
                if (maximized) await fixture.Send("max 2");
                var source = manager.ReadState(targets[2]);
                PlacementSucceeded(await PlacementResult(manager, manager.PlaceAtReferenceAsync(targets[2], targets[0])));
                Equal(new WindowStateInfo(reference.Bounds, false), manager.ReadState(targets[2]));
                Equal(reference, manager.ReadState(targets[0])); Equal(before[1], manager.ReadState(targets[1]));
                PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync()));
                Equal(source, manager.ReadState(targets[2])); Equal<LayoutPositions?>(null, manager.RecentPositions);
            }
            Console.WriteLine($"REFERENCE CROSS SCREEN: {display.Name} -> {other.Name}; normal and maximized source restored.");
        }
        else Console.WriteLine("REFERENCE CROSS SCREEN: not run; no second connected display.");
        await fixture.Send("max 0");
        var maximizedReference = manager.ReadState(targets[0]);
        var sourceBeforeMaxReference = manager.ReadState(targets[2]);
        PlacementSucceeded(await PlacementResult(manager, manager.PlaceAtReferenceAsync(targets[2], targets[0])));
        Equal(new WindowStateInfo(maximizedReference.Bounds, false), manager.ReadState(targets[2]));
        Equal(maximizedReference, manager.ReadState(targets[0]));
        PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync()));
        Equal(sourceBeforeMaxReference, manager.ReadState(targets[2]));
        Console.WriteLine("REFERENCE PLACEMENT: cold start, owned HWND geometry/Undo, foreground observations simulated.");
    }

    private static async Task CheckReferencePlacementGuards()
    {
        foreach (string scenario in new[] { "same", "identity", "outside", "activate", "desktop", "dpi", "moved", "closed", "partial", "late-move", "unknown", "pending", "noop", "current" })
        {
            await using var fixture = await Fixture.Start(3);
            var observations = new PlacementObservations();
            using var manager = new WindowManager(observations);
            var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
            var display = WindowManager.DisplayFor(targets[0].Handle);
            var originals = targets.Select(manager.ReadState).ToArray();
            PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets.Take(2).ToArray(), display)));
            var positions = manager.RecentPositions!;
            var reference = targets[0];
            if (scenario is "current" or "outside")
                await fixture.Send($"position 0 {display.WorkArea.X + (scenario == "current" ? 24 : -24)} {display.WorkArea.Y}");
            observations.AllowActivation = scenario != "activate";
            var referenceBefore = manager.ReadState(reference);
            if (scenario == "noop")
            {
                var value = new Native.Placement { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.Placement>() };
                Equal(true, Native.GetWindowPlacement(reference.Handle, ref value));
                Equal(true, Native.SetWindowPlacement(targets[2].Handle, ref value));
                await Until(() => manager.ReadState(targets[2]) == referenceBefore);
            }
            if (scenario == "partial") await fixture.Send("max 2");
            var sourceBefore = manager.ReadState(targets[2]);
            bool injected = false;
            void MoveReference()
            {
                Equal(true, fixture.Owns(reference.Handle));
                Equal(true, Native.SetWindowPos(reference.Handle, 0, referenceBefore.Bounds.X + 24, referenceBefore.Bounds.Y + 16, 0, 0, 0x15));
                injected = true;
            }
            observations.Activated = () =>
            {
                if (scenario == "desktop") { observations.DesktopObservation = (h, id) => h == reference.Handle ? Guid.NewGuid() : id; injected = true; }
                if (scenario == "dpi") { observations.DisplayObservation = d => d with { Dpi = d.Dpi + 24 }; injected = true; }
                if (scenario == "moved") MoveReference();
                if (scenario == "closed")
                {
                    Equal(true, fixture.Owns(reference.Handle)); Equal(true, PostMessage(reference.Handle, 0x10, 0, 0));
                    if (!SpinWait.SpinUntil(() => !Native.IsWindow(reference.Handle), 1000)) throw new Exception("Owned reference did not close.");
                    injected = true;
                }
                if (scenario == "pending") { injected = true; Thread.Sleep(1200); }
            };
            observations.StateObservation = (h, state) =>
            {
                if (h == targets[2].Handle && scenario == "partial" && !state.Maximized && !injected) MoveReference();
                if (h == targets[2].Handle && scenario == "late-move" && state != sourceBefore && !injected) MoveReference();
                if (h == targets[2].Handle && scenario == "unknown" && state != sourceBefore)
                { injected = true; throw new InvalidOperationException("Injected unknown placement result."); }
                return state;
            };
            var selected = scenario == "same" ? targets[2] : scenario == "identity" ? reference with { Generation = reference.Generation + 1 } : reference;
            var report = await manager.PlaceAtReferenceAsync(targets[2], selected);
            if (scenario == "pending")
            {
                Equal(true, report.Pending); Equal(true, manager.HasPending);
                Equal(true, (await manager.UndoAsync()).Pending);
                Equal(true, (await manager.PlaceAtReferenceAsync(targets[2], reference)).Pending);
                report = await manager.CompletePendingAsync();
            }
            else if (report.Pending) report = await manager.CompletePendingAsync();
            observations.Activated = null; observations.StateObservation = null;
            observations.DisplayObservation = null; observations.DesktopObservation = null;
            Equal(scenario is "pending" or "noop" or "current", report.Success);
            bool singleUndo = scenario is "partial" or "late-move" or "unknown" or "pending" or "current";
            Equal(scenario is not ("same" or "identity" or "noop" or "current" or "outside" or "activate"), injected);
            if (report.Success) Equal(new WindowStateInfo(referenceBefore.Bounds, false), manager.ReadState(targets[2]));
            if (scenario == "current") Equal(false, positions.Regions[0] == referenceBefore.Bounds);
            if (!singleUndo) Equal(sourceBefore, manager.ReadState(targets[2]));
            var expectedReference = scenario == "closed" ? null : manager.ReadState(reference);
            if (scenario is not ("closed" or "moved" or "partial" or "late-move")) Equal(referenceBefore, expectedReference);
            Equal(positions.Version, manager.RecentPositions!.Version);
            var restored = await PlacementResult(manager, manager.UndoAsync());
            Equal(singleUndo ? 1 : 2, restored.Windows.Count);
            if (singleUndo)
            {
                PlacementSucceeded(restored); Equal(targets[2], restored.Windows.Single().Target);
                Equal(sourceBefore, manager.ReadState(targets[2])); Equal(expectedReference, manager.ReadState(reference));
                Equal(positions.Version, manager.RecentPositions!.Version);
            }
            else
            {
                Equal(sourceBefore, manager.ReadState(targets[2]));
                Equal(scenario != "closed", restored.Success);
                for (int i = 0; i < 2; i++) if (scenario != "closed" || i != 0) Equal(originals[i], manager.ReadState(targets[i]));
            }
            Console.WriteLine("REFERENCE GUARD " + scenario + ": owned fixture geometry and Undo verified.");
        }
    }
}
