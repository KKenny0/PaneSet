using System.Windows;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckPointerPlacementSnapshots()
    {
        await using var fixture = await Fixture.Start(3);
        var observations = new PlacementObservations { AllowActivation = false };
        using var manager = new WindowManager(observations);
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var before = targets.Select(manager.ReadState).ToArray();
        var first = new Native.Point { X = before[0].Bounds.X + 40, Y = before[0].Bounds.Y + 80 };
        var later = new Native.Point { X = before[1].Bounds.X + 60, Y = before[1].Bounds.Y + 100 };
        var current = first; nint hit = targets[0].Handle;
        var panel = new MainWindow(manager, WindowManager.DisplayFor(targets[0].Handle), targets, enableShortcuts: false);
        panel.Show(); panel.WindowState = WindowState.Minimized;
        try
        {
            observations.Foreground = targets[2].Handle;
            int samples = 0; bool moved = false;
            observations.PointerObservation = () => { samples++; return (current, hit); };
            observations.PointHitObservation = p => p.X == first.X && p.Y == first.Y ? targets[0].Handle : targets[1].Handle;
            observations.CurrentObservation = (h, value) =>
            {
                if (h == targets[0].Handle) { current = later; hit = targets[1].Handle; moved = true; }
                return value;
            };
            await panel.PlaceFromPointerAsync(targets[2].Handle);
            await Until(() => !manager.HasPending);
            Equal(new WindowStateInfo(before[0].Bounds, false), manager.ReadState(targets[2]));
            Equal(true, moved); Equal(1, samples);
            Equal(before[0], manager.ReadState(targets[0])); Equal(before[1], manager.ReadState(targets[1]));
            PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync())); Equal(before[2], manager.ReadState(targets[2]));
            Console.WriteLine("POINTER SNAPSHOT: pointer moves during candidate observations; action keeps original point/target and source-only Undo. Pointer observations injected.");
        }
        finally { panel.Exit(); }
    }

    private static async Task CheckPointerPlacementSourceEnvironment()
    {
        var displays = WindowManager.ListDisplays();
        if (displays.Count < 2) throw new UnavailableException("Source context check needs two connected displays.");
        foreach (string scenario in new[] { "dpi", "configuration", "monitor", "success", "maximized" })
        {
            await using var fixture = await Fixture.Start(3);
            var observations = new PlacementObservations { AllowActivation = false };
            using var manager = new WindowManager(observations);
            var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
            var referenceDisplay = WindowManager.DisplayFor(targets[0].Handle);
            var sourceDisplay = displays.First(d => d.Handle != referenceDisplay.Handle);
            await fixture.Send($"position 2 {sourceDisplay.WorkArea.X + 120} {sourceDisplay.WorkArea.Y + 140}");
            PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets.Take(2).ToArray(), referenceDisplay)));
            if (scenario == "maximized") await fixture.Send("max 2");
            var before = targets.Select(manager.ReadState).ToArray();
            var expectedUnchanged = before[2]; bool injected = false; int samples = 0;
            observations.Foreground = targets[2].Handle;
            observations.PointerObservation = () =>
            {
                if (++samples == 2)
                {
                    if (scenario is "dpi" or "configuration")
                    {
                        observations.DisplayObservation = d => d.Display.Handle != sourceDisplay.Handle ? d
                            : scenario == "dpi" ? d with { Dpi = d.Dpi + 24 } : d with { Configuration = d.Configuration + 1 };
                        injected = true;
                    }
                    if (scenario == "monitor")
                    {
                        Equal(true, fixture.Owns(targets[2].Handle));
                        Equal(true, Native.SetWindowPos(targets[2].Handle, 0, referenceDisplay.WorkArea.X + 140, referenceDisplay.WorkArea.Y + 180, 0, 0, 0x15));
                        expectedUnchanged = manager.ReadState(targets[2]); injected = true;
                    }
                }
                return (new Native.Point { X = before[0].Bounds.X + 40, Y = before[0].Bounds.Y + 80 }, targets[0].Handle);
            };
            var panel = new MainWindow(manager, referenceDisplay, targets, enableShortcuts: false);
            panel.Show(); panel.WindowState = WindowState.Minimized;
            try
            {
                await panel.PlaceFromPointerAsync(targets[2].Handle);
                await Until(() => !manager.HasPending);
                observations.DisplayObservation = null;
                bool success = scenario is "success" or "maximized";
                Equal(success ? new WindowStateInfo(before[0].Bounds, false) : expectedUnchanged, manager.ReadState(targets[2]));
                Equal(before[0], manager.ReadState(targets[0])); Equal(before[1], manager.ReadState(targets[1]));
                if (!success) Equal(true, injected);
                var report = await PlacementResult(manager, manager.UndoAsync());
                PlacementSucceeded(report); Equal(success ? 1 : 2, report.Windows.Count);
                Equal(success ? before[2] : expectedUnchanged, manager.ReadState(targets[2]));
                Console.WriteLine("POINTER SOURCE ENV " + scenario + ": connected-screen source geometry/Undo; source context faults injected, not physical DPI-change acceptance.");
            }
            finally { panel.Exit(); }
        }
    }
}
