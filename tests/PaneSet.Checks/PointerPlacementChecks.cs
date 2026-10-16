using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckPointerPlacementFallback()
    {
        await using var fixture = await Fixture.Start(3);
        var observations = new PlacementObservations();
        using var manager = new WindowManager(observations);
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var before = targets.Select(manager.ReadState).ToArray();
        var panel = new MainWindow(manager, WindowManager.DisplayFor(targets[0].Handle), targets, targets[2].Handle, enableShortcuts: false);
        panel.Show();
        try
        {
            var button = (Button)panel.FindName("PlaceWindowButton");
            Equal("选择其他位置…", button.Content.ToString());
            Equal(targets[2].Title, ((TextBlock)panel.FindName("PlacementSourceText")).Text);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var picker = Application.Current.Windows.OfType<PlacementWindow>().Single();
            Equal(targets[2], picker.Source); Equal(true, picker.IsReferenceMode);
            picker.Cancel();
            Equal(false, manager.HasUndo);
            for (int i = 0; i < targets.Length; i++) Equal(before[i], manager.ReadState(targets[i]));
            // Manual selection legitimately returns focus from its own UI before placement.
            observations.Foreground = targets[1].Handle;
            PlacementSucceeded(await PlacementResult(manager, manager.PlaceAtReferenceAsync(targets[2], targets[0])));
            Equal(new WindowStateInfo(before[0].Bounds, false), manager.ReadState(targets[2]));
            PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync()));
            Equal(before[2], manager.ReadState(targets[2]));
            Console.WriteLine("POINTER FALLBACK: real panel button keeps captured source; cancellation untouched; manual worker activation remains valid. No real-keyboard claim.");
        }
        finally { panel.Exit(); }
    }

    private static async Task CheckPointerPlacementGuards()
    {
        foreach (string scenario in new[] { "invalid", "self", "outside", "disabled", "moved", "focus", "dpi", "desktop", "pending", "pending-focus", "partial", "late-focus", "unknown", "noop" })
        {
            await using var fixture = await Fixture.Start(3);
            var observations = new PlacementObservations();
            using var manager = new WindowManager(observations);
            var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
            var display = WindowManager.DisplayFor(targets[0].Handle);
            PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets.Take(2).ToArray(), display)));
            var positions = manager.RecentPositions!;
            if (scenario == "partial") await fixture.Send("max 2");
            if (scenario == "disabled") await fixture.Send("disable 0");
            if (scenario == "noop")
            {
                var placement = new Native.Placement { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.Placement>() };
                Equal(true, Native.GetWindowPlacement(targets[0].Handle, ref placement));
                Equal(true, Native.SetWindowPlacement(targets[2].Handle, ref placement));
            }
            var before = targets.Select(manager.ReadState).ToArray();
            observations.AllowActivation = false; observations.Foreground = targets[2].Handle;
            int reads = 0; bool changed = false;
            observations.PointerObservation = () =>
            {
                if (++reads == 2)
                {
                    if (scenario == "moved")
                    {
                        Equal(true, fixture.Owns(targets[0].Handle));
                        Equal(true, Native.SetWindowPos(targets[0].Handle, 0, before[0].Bounds.X + 24, before[0].Bounds.Y + 20, 0, 0, 0x15));
                        changed = true;
                    }
                    if (scenario == "focus") { observations.Foreground = targets[1].Handle; changed = true; }
                    if (scenario == "dpi") { observations.DisplayObservation = d => d with { Dpi = d.Dpi + 24 }; changed = true; }
                    if (scenario == "desktop") { observations.DesktopObservation = (h, id) => h == targets[0].Handle ? Guid.NewGuid() : id; changed = true; }
                    if (scenario is "pending" or "pending-focus") { changed = true; Thread.Sleep(1400); }
                }
                return (new Native.Point { X = before[0].Bounds.X + (scenario == "outside" ? -40 : 40), Y = before[0].Bounds.Y + 80 },
                    scenario == "invalid" ? 0 : scenario == "self" ? targets[2].Handle : targets[0].Handle);
            };
            observations.StateObservation = (h, state) =>
            {
                if (h == targets[2].Handle && scenario == "partial" && !state.Maximized)
                { observations.Foreground = targets[1].Handle; changed = true; }
                if (h == targets[2].Handle && scenario == "late-focus" && state.Bounds == before[0].Bounds)
                { observations.Foreground = targets[1].Handle; changed = true; }
                if (h == targets[2].Handle && scenario == "unknown" && state != before[2])
                { changed = true; throw new InvalidOperationException("Injected unknown pointer placement readback."); }
                return state;
            };
            var panel = new MainWindow(manager, display, targets, enableShortcuts: false);
            panel.Show(); panel.WindowState = WindowState.Minimized;
            try
            {
                await panel.PlaceFromPointerAsync(targets[2].Handle);
                if (scenario is "pending" or "pending-focus")
                {
                    Equal(true, manager.HasPending);
                    if (scenario == "pending-focus") observations.Foreground = targets[1].Handle;
                    panel.DispatchShortcut(ShortcutAction.PlaceWindow, targets[1].Handle);
                    Equal(true, (await manager.UndoAsync()).Pending);
                    await Until(() => !manager.HasPending);
                }
                observations.StateObservation = null; observations.DisplayObservation = null; observations.DesktopObservation = null;
                if (scenario == "disabled") await fixture.Send("enable 0");
                bool sourceUndo = scenario is "partial" or "late-focus" or "unknown" or "pending";
                Equal(WindowState.Minimized, panel.WindowState);
                Equal(false, Application.Current.Windows.OfType<PlacementWindow>().Any());
                Equal(positions.Version, manager.RecentPositions!.Version);
                if (!sourceUndo) Equal(before[2], manager.ReadState(targets[2]));
                if (scenario != "moved") Equal(before[0], manager.ReadState(targets[0]));
                Equal(before[1], manager.ReadState(targets[1]));
                if (scenario is "pending" or "late-focus") Equal(new WindowStateInfo(before[0].Bounds, false), manager.ReadState(targets[2]));
                if (scenario == "late-focus")
                {
                    string status = ((TextBlock)panel.FindName("StatusText")).Text;
                    Equal(true, status.Contains("位置已放置") && status.Contains("未保持前台"));
                    Equal(targets[1].Handle, observations.Foreground);
                    Equal(true, Native.GetWindowRect(FeedbackHandles().Single(), out var hint)); Equal(false, hint.Value == before[0].Bounds);
                }
                if (scenario is "focus" or "pending-focus" or "partial")
                    Equal(true, ((TextBlock)panel.FindName("StatusText")).Text.Contains("前台"));
                if (scenario is "moved" or "focus" or "dpi" or "desktop" or "pending" or "pending-focus" or "partial" or "late-focus" or "unknown") Equal(true, changed);
                var restored = await PlacementResult(manager, manager.UndoAsync());
                PlacementSucceeded(restored); Equal(sourceUndo ? 1 : 2, restored.Windows.Count);
                Equal(before[2], manager.ReadState(targets[2]));
                if (sourceUndo) Equal(targets[2], restored.Windows.Single().Target);
                Console.WriteLine("POINTER GUARD " + scenario + ": real source geometry/Undo, observations injected.");
            }
            finally { panel.Exit(); }
        }
    }

    private static async Task CheckPointerPlacementHit()
    {
        var work = WindowManager.ListDisplays()[0].WorkArea;
        var point = new Native.Point { X = work.X + 180, Y = work.Y + 180 };
        using var environment = new PlacementEnvironment();
        HwndSource Surface(string name, bool transparent = false) => new(new HwndSourceParameters(name)
        {
            PositionX = work.X + 100, PositionY = work.Y + 100, Width = 400, Height = 280,
            WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = transparent ? 0x080800A8 : 0x08000088,
            UsesPerPixelOpacity = transparent
        }) { RootVisual = new Border { Background = Brushes.White } };
        using var reference = Surface("PaneSet owned hit reference");
        using var cover = Surface("PaneSet owned hit cover");
        Equal(true, Native.SetWindowPos(reference.Handle, -1, work.X + 100, work.Y + 100, 400, 280, 0x50));
        Equal(true, Native.SetWindowPos(cover.Handle, -1, work.X + 100, work.Y + 100, 400, 280, 0x50));
        Native.EnableWindow(cover.Handle, false);
        await Task.Delay(100);
        var raw = Native.GetAncestor(Native.WindowFromPoint(point), 2);
        if (raw != reference.Handle && raw != cover.Handle)
            throw new UnavailableException("Owned native hit surfaces are obscured; read-only query, no input sent.");
        Equal(false, Native.IsWindowEnabled(cover.Handle));
        Equal((nint)0, environment.WindowAtPoint(point));
        Native.ShowWindow(cover.Handle, 0);
        Equal(reference.Handle, environment.WindowAtPoint(point));
        using var transparent = Surface("PaneSet owned transparent cover", transparent: true);
        Equal(true, Native.SetWindowPos(transparent.Handle, -1, work.X + 100, work.Y + 100, 400, 280, 0x50));
        await Task.Delay(100);
        var transparentRaw = Native.GetAncestor(Native.WindowFromPoint(point), 2);
        if (transparentRaw != reference.Handle && transparentRaw != transparent.Handle)
            throw new UnavailableException("Native transparent-cover control is obscured; no input sent.");
        Equal(false, environment.WindowAtPoint(point) == reference.Handle);
        Native.ShowWindow(transparent.Handle, 0);
        Equal(reference.Handle, environment.WindowAtPoint(point));
        Console.WriteLine($"POINTER HIT: transparent cover raw={(transparentRaw == reference.Handle ? "underlying; Z barrier exercised" : "cover root")}; no selection through it.");
        Console.WriteLine($"POINTER HIT: native disabled cover blocks selection ({(raw == cover.Handle ? "disabled root" : "skipped cover")}); unobstructed enabled positive control accepted; owned HWND queries only, no input injection.");
    }

    private static async Task CheckPointerPlacementFlow()
    {
        await using var fixture = await Fixture.Start(3);
        var observations = new PlacementObservations { AllowActivation = false };
        using var manager = new WindowManager(observations);
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var before = targets.Select(manager.ReadState).ToArray();
        observations.Foreground = targets[2].Handle;
        observations.PointerObservation = () => (new Native.Point { X = before[0].Bounds.X + 40, Y = before[0].Bounds.Y + 80 }, targets[0].Handle);
        var panel = new MainWindow(manager, WindowManager.DisplayFor(targets[2].Handle), targets, targets[2].Handle, enableShortcuts: false);
        panel.Show(); panel.WindowState = WindowState.Minimized;
        try
        {
            panel.DispatchShortcut(ShortcutAction.PlaceWindow, targets[2].Handle);
            await Task.Delay(100);
            if (Application.Current.Windows.OfType<PlacementWindow>().Any())
                throw new Exception("Pointer placement opened a picker instead of borrowing directly.");
            await Until(() => !manager.HasPending && manager.ReadState(targets[2]) == new WindowStateInfo(before[0].Bounds, false));
            Equal(true, manager.HasUndo);
            Equal(before[0], manager.ReadState(targets[0])); Equal(before[1], manager.ReadState(targets[1]));
            Equal<LayoutPositions?>(null, manager.RecentPositions);
            Equal(WindowState.Minimized, panel.WindowState);
            Equal(true, ((TextBlock)panel.FindName("StatusText")).Text.Contains("已放置"));
            var feedback = new List<nint>();
            Native.EnumWindows((h, _) => { Native.GetWindowThreadProcessId(h, out uint pid); if (pid == Environment.ProcessId && Native.GetProp(h, Native.PlacementFeedbackProperty) == 1) feedback.Add(h); return true; }, 0);
            if (feedback.Count != 1) throw new Exception("Direct placement has no single nonactivating target feedback surface.");
            Equal(true, Native.GetWindowRect(feedback[0], out var feedbackBounds));
            Equal(before[0].Bounds, feedbackBounds.Value);
            var restored = await PlacementResult(manager, manager.UndoAsync());
            PlacementSucceeded(restored); Equal(1, restored.Windows.Count);
            for (int i = 0; i < targets.Length; i++) Equal(before[i], manager.ReadState(targets[i]));
            Console.WriteLine("POINTER FLOW: actual production action, cold start, real fixture geometry and source-only Undo; pointer/foreground observations injected, not physical shortcut acceptance.");
        }
        finally { panel.Exit(); }
    }
}
