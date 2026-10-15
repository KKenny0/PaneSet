using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckPlacementFlow()
    {
        var input = new PlacementInput();
        input.Update(false, true);
        Equal<int?>(null, input.PositionKey(Key.D3, ModifierKeys.None, false));
        input.Update(true, false);
        Equal<int?>(null, input.PositionKey(Key.D3, ModifierKeys.None, false));
        input.Update(true, true);
        Equal(2, input.PositionKey(Key.D3, ModifierKeys.None, false));
        Equal(2, input.PositionKey(Key.NumPad3, ModifierKeys.None, false));
        Equal<int?>(null, input.PositionKey(Key.D0, ModifierKeys.None, false));
        Equal<int?>(null, input.PositionKey(Key.NumPad0, ModifierKeys.None, false));
        Equal<int?>(null, input.PositionKey(Key.D3, ModifierKeys.None, true));
        Equal<int?>(null, input.PositionKey(Key.D3, ModifierKeys.Alt, false));
        Equal<int?>(null, input.PositionKey(Key.Enter, ModifierKeys.None, false));
        input.Update(false, true);
        Equal<int?>(null, input.PositionKey(Key.D1, ModifierKeys.None, false));

        await using var fixture = await Fixture.Start(4);
        using var manager = new WindowManager(new PlacementObservations());
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var panel = new MainWindow(manager, display, targets, targets[3].Handle, enableShortcuts: false);
        Equal(true, ((Button)panel.FindName("PlaceWindowButton")).IsEnabled);
        Equal(targets[3].Title, ((TextBlock)panel.FindName("PlacementSourceText")).Text);
        panel.Show();
        try
        {
            SelectWindows(panel, targets[0].Handle, targets[1].Handle, targets[2].Handle);
            ChooseLayout(panel, ArrangementKind.MainAndOthers);
            await PanelAction(panel, manager, "排布所选窗口");
            var group = SavedGroup(panel);
            var positions = manager.RecentPositions!;
            var before = targets.Select(manager.ReadState).ToArray();
            var picker = panel.OpenPlacement(targets[3].Handle)!;
            Equal(true, picker != null);
            Equal(targets[3], picker!.Source);
            Equal(true, manager.IsPlacementDesktop(new System.Windows.Interop.WindowInteropHelper(picker).Handle, positions.DesktopId));
            Equal(false, ((Button)panel.FindName("ApplyButton")).IsEnabled);
            await panel.ArrangeCurrentScreenAsync(targets[0].Handle, targets);
            Equal(positions.Version, manager.RecentPositions!.Version);
            picker.UpdateLayout(); Screenshot(picker, "paneset-placement-light");
            UiPalette.Apply(true); picker.UpdateLayout(); Screenshot(picker, "paneset-placement-dark"); UiPalette.Apply(false);
            Equal<PlacementWindow?>(null, panel.OpenPlacement(targets[0].Handle));
            Equal(false, picker.IsVisible);
            Equal(group, SavedGroup(panel));
            Equal(positions.Version, manager.RecentPositions!.Version);
            Equal(true, manager.HasUndo);
            for (int i = 0; i < targets.Length; i++) Equal(before[i], manager.ReadState(targets[i]));
            panel.WindowState = WindowState.Normal;
            Equal(false, ((Button)panel.FindName("PlaceWindowButton")).IsEnabled);
            await panel.ArrangeCurrentScreenAsync(targets[0].Handle, targets);
            Equal(4, manager.RecentPositions!.Regions.Count);
            Equal(WindowManager.ListDisplays().Single(d => d.Handle == display.Handle).Name, manager.RecentPositions.Display.Name);
            Equal(true, manager.RecentPositions.Version > positions.Version);
            for (int i = 0; i < 4; i++) Equal(manager.RecentPositions.Regions[i], manager.ReadState(SavedTargets(panel)[i]).Bounds);
            Console.WriteLine("PLACEMENT FLOW: command routing and captures only; real input is checked separately.");
        }
        finally { panel.Exit(); }
        Equal<LayoutPositions?>(null, manager.RecentPositions); Equal(false, manager.HasUndo);
        await CheckPlacementLargeDiagram();
    }

    private static async Task CheckPlacementLargeDiagram()
    {
        await using var fixture = await Fixture.Start(12);
        using var manager = new WindowManager(new PlacementObservations());
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets.Take(11).ToArray(), display)));
        var reference = new MainWindow(manager, display, targets, enableShortcuts: false);
        var picker = new PlacementWindow(reference, manager, targets[11], manager.RecentPositions!, null);
        try
        {
            picker.Show(); picker.UpdateLayout();
            var buttons = Elements<Button>(picker).Where(b => System.Windows.Automation.AutomationProperties.GetName(b).StartsWith("放到位置 ")).ToArray();
            Equal(11, buttons.Length);
            Equal(true, buttons.All(b => b.IsTabStop && b.Focusable && b.ActualWidth >= 44 && b.ActualHeight >= 44));
            Equal(true, ButtonNamed(picker, "放到位置 11").IsEnabled);
            Screenshot(picker, "paneset-placement-eleven");
            picker.Width = 340; picker.Height = 240; picker.UpdateLayout(); Screenshot(picker, "paneset-placement-small");
        }
        finally { picker.Cancel(); reference.Exit(); }
    }
}
