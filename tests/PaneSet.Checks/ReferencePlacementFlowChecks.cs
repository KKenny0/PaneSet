using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckReferencePlacementFlow()
    {
        await using var fixture = await Fixture.Start(12);
        using var manager = new WindowManager(new PlacementObservations());
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var before = targets.Select(manager.ReadState).ToArray();
        var display = WindowManager.DisplayFor(targets[0].Handle);
        var owner = new MainWindow(manager, display, targets, targets[11].Handle, enableShortcuts: false);
        owner.Show();
        try
        {
            // Production entry must work before there has ever been an arrangement.
            var opened = owner.OpenPlacement(targets[11].Handle) ?? throw new Exception("Cold-start entry did not open.");
            Equal(true, opened.IsReferenceMode); Equal(targets[11], opened.Source);
            Equal<LayoutPositions?>(null, opened.Positions);
            Equal(false, Elements<Button>(opened).Any(b => AutomationProperties.GetName(b) == "上次布局位置"));
            Equal<PlacementWindow?>(null, owner.OpenPlacement(targets[0].Handle));
            Equal(false, opened.IsVisible); Equal(false, manager.HasUndo);
            for (int i = 0; i < targets.Length; i++) Equal(before[i], manager.ReadState(targets[i]));

            var picker = new PlacementWindow(owner, manager, targets[11], null, null, targets);
            bool dark = UiPalette.IsDark;
            try
            {
                picker.Show(); picker.UpdateLayout();
                var buttons = Elements<Button>(picker).Where(b => AutomationProperties.GetName(b).StartsWith("借用窗口 ")).ToArray();
                Equal(11, buttons.Length);
                Equal(true, buttons.All(b => b.IsEnabled && b.IsTabStop && b.Focusable && b.ActualHeight >= 44));
                Equal(true, buttons.All(b => ((FrameworkElement)b.Content).ActualWidth >= b.ActualWidth - 26));
                Equal(true, ButtonNamed(picker, $"借用窗口 11 · {targets[10].Application} · {targets[10].Title}").IsEnabled);
                UiPalette.Apply(false); picker.UpdateLayout(); Screenshot(picker, "paneset-reference-light");
                UiPalette.Apply(true); picker.UpdateLayout(); Screenshot(picker, "paneset-reference-dark");
                var search = Elements<TextBox>(picker).Single(b => AutomationProperties.GetName(b) == "搜索参照窗口");
                search.Text = targets[9].Title;
                Equal(true, ButtonNamed(picker, $"借用窗口 10 · {targets[9].Application} · {targets[9].Title}").IsEnabled);
                Equal(1, Elements<Button>(picker).Count(b => AutomationProperties.GetName(b).StartsWith("借用窗口 ")));
                search.Text = "no matching reference window";
                Equal(0, Elements<Button>(picker).Count(b => AutomationProperties.GetName(b).StartsWith("借用窗口 ")));
                Equal(true, Elements<TextBlock>(picker).Any(t => t.Text.Contains("没有匹配的窗口")));
                search.Text = "";
                Equal(11, Elements<Button>(picker).Count(b => AutomationProperties.GetName(b).StartsWith("借用窗口 ")));
                picker.Width = 340; picker.Height = 260; picker.UpdateLayout(); Screenshot(picker, "paneset-reference-small");
            }
            finally { picker.Cancel(); UiPalette.Apply(dark); }
            Equal(false, manager.HasUndo); Equal<LayoutPositions?>(null, manager.RecentPositions);
            for (int i = 0; i < targets.Length; i++) Equal(before[i], manager.ReadState(targets[i]));

            var empty = new PlacementWindow(owner, manager, targets[11], null, null, []);
            try
            {
                empty.Show(); empty.UpdateLayout(); Screenshot(empty, "paneset-reference-empty");
                Equal(true, Elements<TextBlock>(empty).Any(t => t.Text.Contains("没有其他合格窗口")));
                Equal(0, Elements<Button>(empty).Count(b => AutomationProperties.GetName(b).StartsWith("借用窗口 ")));
            }
            finally { empty.Cancel(); }
            owner.WindowState = WindowState.Normal;
            SelectWindows(owner, targets[0].Handle, targets[1].Handle);
            await PanelAction(owner, manager, "排布所选窗口");
            var positions = manager.RecentPositions!;
            var combined = new PlacementWindow(owner, manager, targets[11], positions, null, targets);
            try
            {
                combined.Show(); combined.UpdateLayout();
                Equal(true, combined.IsReferenceMode);
                ButtonNamed(combined, "上次布局位置").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Equal(false, combined.IsReferenceMode); Equal(positions, combined.Positions);
                combined.UpdateLayout();
                Equal(2, Elements<Button>(combined).Count(b => AutomationProperties.GetName(b).StartsWith("放到位置 ")));
                combined.UpdateLayout(); Screenshot(combined, "paneset-reference-history");
                ButtonNamed(combined, "借用窗口位置").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Equal(true, combined.IsReferenceMode);
            }
            finally { combined.Cancel(); }
            Equal(positions.Version, manager.RecentPositions!.Version);
            Console.WriteLine("REFERENCE FLOW: cold-start entry, frozen source, filter/stable numbers, empty/history, owned WPF captures; no real-input claim.");
        }
        finally { owner.Exit(); }
    }
}
