using System.Runtime.InteropServices;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckPlacementFailureUndo()
    {
        int rejectedBeforeChange = 0, unknownAfterChange = 0;
        // Sweep observation boundaries rather than depending on one private guard's call number.
        for (int failAt = 1; failAt <= 32; failAt++)
        {
            await using var fixture = await Fixture.Start(3);
            var observations = new PlacementObservations();
            using var manager = new WindowManager(observations);
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
            var originals = targets.Select(manager.ReadState).ToArray();
            PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets.Take(2).ToArray(), display)));
            var positions = manager.RecentPositions!;
            var arranged = targets.Take(2).Select(manager.ReadState).ToArray();
            Equal(false, originals[2].Maximized);
            Equal(true, originals[2].Bounds != positions.Regions[1]);
            int captures = 0;
            bool injected = false;
            observations.DisplayObservation = value =>
            {
                if (++captures == failAt)
                {
                    injected = true;
                    throw new InvalidOperationException("Injected display-query failure.");
                }
                return value;
            };
            observations.StateObservation = (_, state) => injected
                ? throw new InvalidOperationException("Readback unavailable after display-query failure.") : state;
            var placed = await PlacementResult(manager, manager.PlaceAsync(targets[2], positions.Version, 1));
            observations.DisplayObservation = null; observations.StateObservation = null;
            bool changed = manager.ReadState(targets[2]) != originals[2];
            if (injected && !changed) { rejectedBeforeChange++; Equal(false, placed.Success); }
            if (injected && changed && !placed.Success) unknownAfterChange++;
            for (int i = 0; i < 2; i++) Equal(arranged[i], manager.ReadState(targets[i]));
            var restored = await PlacementResult(manager, manager.UndoAsync());
            Console.WriteLine($"PLACEMENT FAILURE boundary={failAt}, injected={injected}, changed={changed}, undoWindows={restored.Windows.Count}");
            PlacementSucceeded(restored);
            Equal(changed ? 1 : 2, restored.Windows.Count);
            Equal(false, manager.HasUndo);
            Equal(originals[2], manager.ReadState(targets[2]));
            for (int i = 0; i < 2; i++) Equal(changed ? arranged[i] : originals[i], manager.ReadState(targets[i]));
            if (changed)
            {
                Equal(targets[2], restored.Windows.Single().Target);
                Equal(positions.Version, manager.RecentPositions!.Version);
            }
            else Equal<LayoutPositions?>(null, manager.RecentPositions);
            if (!injected)
            {
                PlacementSucceeded(placed);
                Equal(true, changed);
                Equal(true, rejectedBeforeChange > 0);
                Equal(true, unknownAfterChange > 0);
                return;
            }
        }
        throw new Exception("No fault-free placement within the display-query failure sweep budget.");
    }

    private static async Task CheckPlacementRestoreReadback()
    {
        foreach (var (change, unreadable, revert) in new[]
            { (true, false, false), (true, true, false), (false, false, false), (false, true, false), (true, false, true) })
        {
            await using var fixture = await Fixture.Start(2);
            var observations = new PlacementObservations();
            using var manager = new WindowManager(observations);
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
            var original = manager.ReadState(targets[0]);
            var originalPlacement = new Native.Placement { Length = Marshal.SizeOf<Native.Placement>() };
            Equal(true, Native.GetWindowPlacement(targets[0].Handle, ref originalPlacement));
            PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets, display)));
            var retained = manager.ReadState(targets[1]);
            Equal(true, Native.SetWindowPlacement(targets[0].Handle, ref originalPlacement));
            await Until(() => manager.ReadState(targets[0]) == original);
            // Only the first window changes in this arrangement, so Undo has one affected member.
            PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets, display)));
            var positions = manager.RecentPositions!;
            var arranged = manager.ReadState(targets[0]);
            Equal(true, arranged != original);
            var arrangedPlacement = new Native.Placement { Length = Marshal.SizeOf<Native.Placement>() };
            Equal(true, Native.GetWindowPlacement(targets[0].Handle, ref arrangedPlacement));
            if (!change)
            {
                Equal(true, Native.SetWindowPlacement(targets[0].Handle, ref originalPlacement));
                await Until(() => manager.ReadState(targets[0]) == original);
            }
            int restoredReads = 0;
            bool injected = false;
            observations.StateObservation = (handle, state) =>
            {
                // Let the restored state be observed successfully before altering its final readback.
                if (handle == targets[0].Handle && state == original && ++restoredReads > (change ? 1 : 2))
                {
                    if (unreadable) { injected = true; throw new InvalidOperationException("Final restore readback unavailable."); }
                    if (revert)
                    {
                        injected = true;
                        Equal(true, Native.SetWindowPlacement(handle, ref arrangedPlacement));
                        if (!SpinWait.SpinUntil(() => Native.Bounds(handle) == arranged.Bounds && Native.IsZoomed(handle) == arranged.Maximized, 1000))
                            throw new Exception("Owned restore fixture did not return to its pre-restore state.");
                        return arranged;
                    }
                }
                return state;
            };
            var restored = await PlacementResult(manager, manager.UndoAsync());
            observations.StateObservation = null;
            PlacementSucceeded(restored);
            Equal(1, restored.Windows.Count); Equal(targets[0], restored.Windows.Single().Target);
            Equal(false, manager.HasUndo); Equal(unreadable || revert, injected);
            Equal(true, restoredReads >= (change ? 2 : 3));
            Equal(revert ? arranged : original, manager.ReadState(targets[0]));
            Equal(retained, manager.ReadState(targets[1]));
            Console.WriteLine($"RESTORE READBACK change={change}, unreadable={unreadable}, revert={revert}, positionsRetained={manager.RecentPositions != null}");
            if (change && !revert)
            {
                Equal<LayoutPositions?>(null, manager.RecentPositions);
                Equal(false, manager.CanPlace(targets[0], positions.Version, out _));
            }
            else
            {
                Equal(positions.Version, manager.RecentPositions!.Version);
                Equal(true, manager.CanPlace(targets[0], positions.Version, out _));
            }
        }
    }
}
