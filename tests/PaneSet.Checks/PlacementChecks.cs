using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    // System observations/faults are injectable; geometry, identity and Undo use real fixture HWNDs.
    private sealed class PlacementObservations : PlacementEnvironment
    {
        public bool AllowActivation = true;
        public nint Foreground;
        public Func<(Native.Point Position, nint Window)>? PointerObservation;
        public Func<Native.Point, nint>? PointHitObservation;
        public override (Native.Point Position, nint Window) ReadPointer() => PointerObservation?.Invoke() ?? base.ReadPointer();
        public override nint WindowAtPoint(Native.Point point) => PointHitObservation?.Invoke(point) ?? PointerObservation?.Invoke().Window ?? base.WindowAtPoint(point);
        public Action? Activated;
        public Func<PlacementDisplay, PlacementDisplay>? DisplayObservation;
        public Func<nint, Guid, Guid>? DesktopObservation;
        public Func<nint, bool, bool>? CurrentObservation;
        public Func<nint, WindowStateInfo, WindowStateInfo>? StateObservation;
        public override bool Activate(nint window)
        {
            if (!AllowActivation) return false;
            Foreground = window; Activated?.Invoke(); return true;
        }
        public override bool IsForeground(nint window) => Foreground == window;
        public override PlacementDisplay Capture(DisplayArea display)
        {
            var value = base.Capture(display); return DisplayObservation?.Invoke(value) ?? value;
        }
        public override Guid DesktopFor(nint window)
        {
            var value = base.DesktopFor(window); return DesktopObservation?.Invoke(window, value) ?? value;
        }
        public override bool IsCurrent(nint window)
        {
            bool value = base.IsCurrent(window); return CurrentObservation?.Invoke(window, value) ?? value;
        }
        public override WindowStateInfo ReadState(nint window)
        {
            var value = base.ReadState(window); return StateObservation?.Invoke(window, value) ?? value;
        }
    }

    private static async Task CheckPlacement()
    {
        await using var fixture = await Fixture.Start(4);
        using var manager = new WindowManager(new PlacementObservations());
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var original = manager.ReadState(targets[3]);
        var report = await manager.ApplyAsync(targets.Take(3).ToArray(), display, ArrangementKind.MainAndOthers);
        if (report.Pending) report = await manager.CompletePendingAsync();
        Equal(true, report.Success);
        var positions = manager.RecentPositions ?? throw new Exception("Successful arrangement did not remember positions.");
        Equal(3, positions.Regions.Count);
        Equal(new WindowRect(display.WorkArea.X, display.WorkArea.Y, (display.WorkArea.Width - 12) * 2 / 3, display.WorkArea.Height), positions.Regions[0]);
        var arranged = targets.Take(3).Select(manager.ReadState).ToArray();
        Console.WriteLine("PLACEMENT geometry uses real HWNDs; foreground observations simulated, not an input pass.");
        report = await manager.PlaceAsync(targets[3], positions.Version, 2);
        if (report.Pending) report = await manager.CompletePendingAsync();
        if (!report.Success) throw new Exception("Placement failed: " + string.Join(" | ", report.Windows.Select(w => w.Message)));
        Equal(positions.Regions[2], manager.ReadState(targets[3]).Bounds);
        for (int i = 0; i < 3; i++) Equal(arranged[i], manager.ReadState(targets[i]));
        report = await manager.UndoAsync();
        if (report.Pending) report = await manager.CompletePendingAsync();
        Equal(true, report.Success);
        Equal(original, manager.ReadState(targets[3]));
        Equal(positions.Version, manager.RecentPositions!.Version);
        Equal(false, manager.HasUndo);
        PlacementSucceeded(await PlacementResult(manager, manager.PlaceAsync(targets[0], positions.Version, 2)));
        Equal(positions.Regions[2], manager.ReadState(targets[0]).Bounds);
        Equal(arranged[1], manager.ReadState(targets[1])); Equal(arranged[2], manager.ReadState(targets[2]));
        PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync())); Equal(arranged[0], manager.ReadState(targets[0]));
        await fixture.Send($"position 1 {display.WorkArea.X + 100} {display.WorkArea.Y + 100}");
        Equal(positions.Regions[1], manager.RecentPositions!.Regions[1]);
        await fixture.Send("close 0"); await fixture.Send("close 1");
        var remaining = manager.ReadState(targets[2]);
        PlacementSucceeded(await PlacementResult(manager, manager.PlaceAsync(targets[3], positions.Version, 1)));
        Equal(positions.Regions[1], manager.ReadState(targets[3]).Bounds); Equal(remaining, manager.ReadState(targets[2]));
        PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync())); Equal(original, manager.ReadState(targets[3]));
        var other = WindowManager.ListDisplays().FirstOrDefault(d => d.Handle != display.Handle);
        if (other != null)
        {
            foreach (bool maximize in new[] { false, true })
            {
                await fixture.Send($"position 3 {other.WorkArea.X + 100} {other.WorkArea.Y + 100}");
                if (maximize) await fixture.Send("max 3");
                var sourceBefore = manager.ReadState(targets[3]);
                Equal(other.Handle, WindowManager.DisplayFor(targets[3].Handle).Handle);
                PlacementSucceeded(await PlacementResult(manager, manager.PlaceAsync(targets[3], positions.Version, 0)));
                Equal(new WindowStateInfo(positions.Regions[0], false), manager.ReadState(targets[3]));
                Equal(remaining, manager.ReadState(targets[2]));
                PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync()));
                Equal(sourceBefore, manager.ReadState(targets[3])); Equal(other.Handle, WindowManager.DisplayFor(targets[3].Handle).Handle);
            }
            Console.WriteLine("PLACEMENT cross-display normal/maximized placement and restore passed at the actual display DPI.");
        }
        else Console.WriteLine("UNAVAILABLE placement cross-display: only one display connected.");
    }

    private static async Task<OperationReport> PlacementResult(WindowManager manager, Task<OperationReport> operation)
    {
        var report = await operation;
        return report.Pending ? await manager.CompletePendingAsync() : report;
    }
    private static void PlacementSucceeded(OperationReport report)
    {
        if (!report.Success) throw new Exception(string.Join(" | ", report.Windows.Select(w => w.Message)) + report.Notice);
    }

    private static async Task CheckPlacementGuards()
    {
        await using var fixture = await Fixture.Start(4);
        var observations = new PlacementObservations();
        using var manager = new WindowManager(observations);
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var originals = targets.Select(manager.ReadState).ToArray();
        Equal(false, manager.CanPlace(targets[3], 0, out _));
        Equal(false, (await manager.PlaceAsync(targets[3], 0, 0)).Success);
        Equal(false, manager.HasUndo);
        PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets.Take(3).ToArray(), display, ArrangementKind.Grid)));
        var positions = manager.RecentPositions!;
        Equal(3, positions.Regions.Count); // A three-window grid exposes three slots, not its empty fourth cell.
        var arranged = targets.Select(manager.ReadState).ToArray();
        async Task Reject(WindowTarget? source = null, long? version = null, int index = 0)
        {
            Equal(false, (await PlacementResult(manager, manager.PlaceAsync(source ?? targets[3], version ?? positions.Version, index))).Success);
            Equal(positions.Version, manager.RecentPositions!.Version); Equal(true, manager.HasUndo);
            for (int i = 0; i < targets.Length; i++) Equal(arranged[i], manager.ReadState(targets[i]));
        }
        await Reject(targets[3] with { Generation = targets[3].Generation + 1 });
        await Reject(targets[3] with { ThreadId = targets[3].ThreadId + 1 });
        await Reject(version: positions.Version - 1); await Reject(index: 3);
        foreach (var mismatch in new Func<PlacementDisplay, PlacementDisplay>[] {
            d => d with { Device = "missing-device" }, d => d with { Configuration = d.Configuration + 1 },
            d => d with { Bounds = d.Bounds with { X = d.Bounds.X + 1 } }, d => d with { Dpi = d.Dpi + 24 },
            d => d with { Display = d.Display with { WorkArea = d.Display.WorkArea with { Y = d.Display.WorkArea.Y + 1 } } },
            _ => throw new InvalidOperationException("display query failed") })
        {
            observations.DisplayObservation = mismatch; await Reject(); observations.DisplayObservation = null;
        }
        observations.CurrentObservation = (h, current) => h == targets[3].Handle ? false : current;
        await Reject(); observations.CurrentObservation = null;
        observations.DesktopObservation = (h, id) => h == targets[3].Handle ? Guid.NewGuid() : id;
        await Reject(); observations.DesktopObservation = (_, _) => throw new InvalidOperationException("desktop query failed");
        await Reject(); observations.DesktopObservation = null;
        observations.Activated = () => observations.DisplayObservation = d => d with { Dpi = d.Dpi + 24 };
        await Reject(); observations.Activated = null; observations.DisplayObservation = null;
        observations.DisplayObservation = _ => throw new InvalidOperationException("metadata unavailable");
        var successfulNoop = await PlacementResult(manager, manager.ApplyAsync(targets.Take(3).ToArray(), display, ArrangementKind.Grid));
        PlacementSucceeded(successfulNoop); Equal(true, successfulNoop.Notice.Contains("位置未能记住"));
        Equal(positions.Version, manager.RecentPositions!.Version); observations.DisplayObservation = null;
        await fixture.Send("close 3");
        Equal(false, (await manager.PlaceAsync(targets[3], positions.Version, 0)).Success);
        PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync()));
        Equal<LayoutPositions?>(null, manager.RecentPositions);
        for (int i = 0; i < 3; i++) Equal(originals[i], manager.ReadState(targets[i]));
        Console.WriteLine("PLACEMENT GUARDS: display/desktop mismatch observations injected; no physical topology or DPI claim.");
    }

    private static async Task CheckPlacementUndo()
    {
        await using var fixture = await Fixture.Start(4);
        var observations = new PlacementObservations();
        using var manager = new WindowManager(observations);
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var original = targets.Select(manager.ReadState).ToArray();
        var originalPlacements = targets.Take(2).Select(t =>
        {
            var value = new Native.Placement { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.Placement>() };
            Equal(true, Native.GetWindowPlacement(t.Handle, ref value)); return value;
        }).ToArray();
        async Task<LayoutPositions> Arrange()
        {
            PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets.Take(2).ToArray(), display)));
            return manager.RecentPositions!;
        }
        var positions = await Arrange();
        PlacementSucceeded(await PlacementResult(manager, manager.PlaceAsync(targets[0], positions.Version, 0)));
        Equal(true, manager.HasUndo);
        observations.AllowActivation = false;
        Equal(false, (await manager.PlaceAsync(targets[2], positions.Version, 1)).Success);
        Equal(original[2], manager.ReadState(targets[2]));
        observations.AllowActivation = true;
        var restoredGroup = await PlacementResult(manager, manager.UndoAsync()); PlacementSucceeded(restoredGroup);
        Equal(2, restoredGroup.Windows.Count); Equal<LayoutPositions?>(null, manager.RecentPositions);
        for (int i = 0; i < 4; i++) Equal(original[i], manager.ReadState(targets[i]));

        positions = await Arrange();
        for (int i = 0; i < 2; i++)
        {
            var value = originalPlacements[i]; Equal(true, Native.SetWindowPlacement(targets[i].Handle, ref value));
        }
        await Until(() => targets.Take(2).Select(manager.ReadState).SequenceEqual(original.Take(2)));
        PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync()));
        Equal(positions.Version, manager.RecentPositions!.Version); // A restore with no actual change preserves slots.
        positions = await Arrange();
        await fixture.Send("max 2");
        var maximized = manager.ReadState(targets[2]); Equal(true, maximized.Maximized);
        await fixture.Send("block 2");
        Equal(false, (await PlacementResult(manager, manager.PlaceAsync(targets[2], positions.Version, 1))).Success);
        Equal(false, manager.ReadState(targets[2]).Maximized); Equal(true, manager.HasUndo);
        observations.DisplayObservation = d => d with { Dpi = d.Dpi + 24 };
        Equal(false, (await PlacementResult(manager, manager.UndoAsync())).Success); Equal(true, manager.HasUndo);
        observations.DisplayObservation = null; await fixture.Send("unblock");
        var restored = await PlacementResult(manager, manager.UndoAsync()); PlacementSucceeded(restored);
        Equal(1, restored.Windows.Count); Equal(maximized, manager.ReadState(targets[2]));
        Equal(false, manager.HasUndo); Equal(positions.Version, manager.RecentPositions!.Version);

        await fixture.Send("normal 2");
        var beforeUnknown = manager.ReadState(targets[2]);
        observations.StateObservation = (h, state) => h == targets[2].Handle && state != beforeUnknown
            ? throw new InvalidOperationException("state readback unavailable") : state;
        var unknown = await PlacementResult(manager, manager.PlaceAsync(targets[2], positions.Version, 0));
        Equal(false, unknown.Success); Equal(true, unknown.Windows.Single().Message.Contains("结果未能确认")); Equal(true, manager.HasUndo);
        observations.StateObservation = null;
        Equal(positions.Regions[0], manager.ReadState(targets[2]).Bounds);
        PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync())); Equal(beforeUnknown, manager.ReadState(targets[2]));
        Equal(positions.Version, manager.RecentPositions!.Version);

        observations.Activated = () => Thread.Sleep(1300);
        Equal(true, (await manager.PlaceAsync(targets[2], positions.Version, 1)).Pending);
        Equal(true, (await manager.UndoAsync()).Pending);
        observations.Activated = null;
        PlacementSucceeded(await manager.CompletePendingAsync());
        PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync()));
        Equal(beforeUnknown, manager.ReadState(targets[2]));

        await fixture.Send("stall");
        var waiting = await manager.PlaceAsync(targets[2], positions.Version, 1);
        Equal(true, waiting.Pending); Equal(true, manager.HasPending);
        Equal(true, (await manager.UndoAsync()).Pending);
        Equal(true, (await manager.PlaceAsync(targets[3], positions.Version, 0)).Pending);
        PlacementSucceeded(await manager.CompletePendingAsync());
        PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync()));
        Equal(beforeUnknown, manager.ReadState(targets[2]));
        Equal(original[3], manager.ReadState(targets[3]));
        await CheckPlacementFinalReadback();
        Console.WriteLine("PLACEMENT UNDO: native geometry/state/pending; activation and failure observations injected.");
    }

    private static async Task CheckPlacementFinalReadback()
    {
        foreach (var (confirmedChange, loseIdentity) in new[] { (true, true), (false, true), (true, false) })
        {
            await using var fixture = await Fixture.Start();
            var observations = new PlacementObservations();
            using var manager = new WindowManager(observations);
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
            var originals = targets.Select(manager.ReadState).ToArray();
            var originalPlacement = new Native.Placement { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.Placement>() };
            Equal(true, Native.GetWindowPlacement(targets[2].Handle, ref originalPlacement));
            PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets.Take(2).ToArray(), display)));
            var positions = manager.RecentPositions!;
            var arranged = targets.Take(2).Select(manager.ReadState).ToArray();
            bool changedRead = false, settled = false;
            void CloseSource()
            {
                Equal(true, fixture.Owns(targets[2].Handle));
                Equal(true, PostMessage(targets[2].Handle, 0x10, 0, 0));
                if (!SpinWait.SpinUntil(() => !Native.IsWindow(targets[2].Handle), 1000))
                    throw new Exception("Owned placement source did not close.");
                settled = true;
            }
            observations.StateObservation = (handle, state) =>
            {
                if (handle == targets[2].Handle && state != originals[2])
                {
                    if (!confirmedChange) { CloseSource(); throw new InvalidOperationException("Source identity lost before a successful changed-state read."); }
                    changedRead = true;
                }
                return state;
            };
            observations.DesktopObservation = (handle, desktop) =>
            {
                // The successful WaitFor observation has returned before the final desktop validation.
                if (handle == targets[2].Handle && changedRead && !settled)
                {
                    if (loseIdentity) CloseSource();
                    else
                    {
                        Equal(true, Native.SetWindowPlacement(handle, ref originalPlacement));
                        if (!SpinWait.SpinUntil(() => Native.Bounds(handle) == originals[2].Bounds && !Native.IsZoomed(handle), 1000))
                            throw new Exception("Owned placement source did not revert.");
                        settled = true;
                    }
                }
                return desktop;
            };
            Equal(false, (await PlacementResult(manager, manager.PlaceAsync(targets[2], positions.Version, 1))).Success);
            Equal(true, settled);
            observations.StateObservation = null; observations.DesktopObservation = null;
            var restored = await PlacementResult(manager, manager.UndoAsync());
            bool sourceUndo = confirmedChange && loseIdentity;
            Equal(sourceUndo ? 1 : 2, restored.Windows.Count);
            Equal(!sourceUndo, restored.Success); Equal(false, manager.HasUndo);
            for (int i = 0; i < 2; i++) Equal(sourceUndo ? arranged[i] : originals[i], manager.ReadState(targets[i]));
            if (sourceUndo) Equal(positions.Version, manager.RecentPositions!.Version);
        }
    }

    private static async Task CheckPlacementSettings()
    {
        Equal(true, Enum.TryParse<ShortcutAction>("PlaceWindow", out var action));
        Equal(5, (int)action);
        Equal(new ShortcutBinding(3, 0x33), ShortcutSettings.Default(action));
        Equal("单窗放置", PanelShortcuts.Label(action));
        string path = Path.Combine(Path.GetTempPath(), "paneset-placement-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            const string legacy = "{\"version\":1,\"shortcuts\":{\"OpenPanel\":{\"modifiers\":3,\"key\":51},\"Restore\":null,\"ArrangeScreen\":{\"modifiers\":7,\"key\":65},\"SelectScreen\":null},\"theme\":\"Dark\"}";
            File.WriteAllText(path, legacy);
            var loaded = ShortcutSettings.Load(path, out string notice);
            Equal(AppearanceMode.Dark, loaded.Theme);
            Equal(new ShortcutBinding(3, 0x33), loaded.For(ShortcutAction.OpenPanel));
            Equal(new ShortcutBinding(7, 0x41), loaded.For(ShortcutAction.ArrangeScreen));
            Equal<ShortcutBinding?>(null, loaded.For(ShortcutAction.Restore));
            Equal<ShortcutBinding?>(null, loaded.For(action));
            Equal(true, notice.Contains("单窗放置"));
            Equal(legacy, File.ReadAllText(path));
            File.WriteAllText(path, "{\"version\":1,\"shortcuts\":{},\"theme\":\"Light\"}");
            loaded = ShortcutSettings.Load(path, out notice);
            Equal(AppearanceMode.Light, loaded.Theme);
            Equal(ShortcutSettings.Default(action), loaded.For(action));
            loaded.Shortcuts[action] = null; loaded.Save(path);
            Equal<ShortcutBinding?>(null, ShortcutSettings.Load(path, out _).For(action));
            var custom = new ShortcutBinding(7, 0x87);
            var oldBinding = new ShortcutBinding(7, 0x86);
            var configured = new ShortcutSettings { Theme = AppearanceMode.Dark,
                Shortcuts = Enum.GetValues<ShortcutAction>().ToDictionary(a => a, _ => (ShortcutBinding?)null) };
            configured.Shortcuts[ShortcutAction.OpenPanel] = oldBinding; configured.Shortcuts[action] = custom; configured.Save(path);
            using var manager = new WindowManager();
            var owner = new MainWindow(manager, WindowManager.ListDisplays()[0], [], enableShortcuts: false, settingsPath: path);
            var blocker = new Window();
            nint ownerHandle = new WindowInteropHelper(owner).EnsureHandle(), blockerHandle = new WindowInteropHelper(blocker).EnsureHandle();
            if (!Native.RegisterHotKey(blockerHandle, 93, 0x4000 | custom.Modifiers, custom.Key))
            { owner.Exit(); blocker.Close(); throw new UnavailableException("Placement check shortcut already owned externally; not reconfigured."); }
            try
            {
                int dispatched = 0;
                using (var shortcuts = new PanelShortcuts(ownerHandle, (a, _) => { if (a == action) dispatched++; }, path, Enum.GetValues<ShortcutAction>()))
                {
                    Equal(0, shortcuts.RegisteredId(action)); Equal(true, shortcuts.StatusFor(action).Contains("冲突"));
                    Equal(true, shortcuts.RegisteredId(ShortcutAction.OpenPanel) != 0);
                    Equal(custom, shortcuts.BindingFor(action)); Equal(AppearanceMode.Dark, ShortcutSettings.Load(path, out _).Theme);
                    Native.UnregisterHotKey(blockerHandle, 93);
                    Equal("", shortcuts.Change(action, custom));
                    int active = shortcuts.RegisteredId(action); Equal(true, active != 0);
                    PostMessage(ownerHandle, 0x312, active, 0); await Until(() => dispatched == 1);
                    Equal(true, shortcuts.Change(action, oldBinding).Contains("其他动作"));
                    using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        Equal(true, shortcuts.Change(action, new(7, 0x85)).Contains("未保存"));
                        Equal(custom, shortcuts.BindingFor(action)); Equal(active, shortcuts.RegisteredId(action));
                        Equal(custom, ShortcutSettings.Load(path, out _).For(action));
                    }
                    var settings = new SettingsWindow(owner, shortcuts, AppearanceMode.Dark, _ => ""); settings.Show();
                    try
                    {
                        Equal(5, Elements<ComboBox>(settings).Count(c => AutomationProperties.GetName(c).EndsWith(" 主键")));
                        Equal(true, Elements<TextBlock>(settings).Any(t => t.Text == "单窗放置 · 全局"));
                    }
                    finally { settings.Close(); }
                    Equal("", shortcuts.Change(action, null));
                    Equal<ShortcutBinding?>(null, ShortcutSettings.Load(path, out _).For(action));
                    Equal(true, Native.RegisterHotKey(blockerHandle, 94, 0x4000 | custom.Modifiers, custom.Key));
                    Native.UnregisterHotKey(blockerHandle, 94);
                    Equal("", shortcuts.Change(action, custom));
                }
                using (var restarted = new PanelShortcuts(ownerHandle, (_, _) => { }, path, Enum.GetValues<ShortcutAction>()))
                { Equal(custom, restarted.BindingFor(action)); Equal(true, restarted.RegisteredId(action) != 0); }
                Equal(true, Native.RegisterHotKey(blockerHandle, 95, 0x4000 | custom.Modifiers, custom.Key));
                Native.UnregisterHotKey(blockerHandle, 95);
                Equal(oldBinding, ShortcutSettings.Load(path, out _).For(ShortcutAction.OpenPanel));
                Equal(AppearanceMode.Dark, ShortcutSettings.Load(path, out _).Theme);
            }
            finally
            {
                for (int id = 93; id <= 95; id++) Native.UnregisterHotKey(blockerHandle, id);
                owner.Exit(); blocker.Close();
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
