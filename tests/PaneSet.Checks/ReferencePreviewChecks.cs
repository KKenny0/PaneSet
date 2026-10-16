using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckReferencePositionPreview()
    {
        await using var fixture = await Fixture.Start(3);
        using var manager = new WindowManager(new PlacementObservations());
        var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var display = WindowManager.DisplayFor(targets[0].Handle);
        PlacementSucceeded(await PlacementResult(manager, manager.ApplyAsync(targets.Take(2).ToArray(), display)));
        var positions = manager.RecentPositions!;
        var before = targets.Select(manager.ReadState).ToArray();
        var owner = new MainWindow(manager, display, targets, enableShortcuts: false);
        var picker = new PlacementWindow(owner, manager, targets[2], null, null, targets);
        bool dark = UiPalette.IsDark;
        try
        {
            owner.Show(); picker.Show(); picker.UpdateLayout();
            var diagram = Elements<Canvas>(picker).SingleOrDefault(c => AutomationProperties.GetName(c) == "单窗放置位置预览")
                ?? throw new Exception("Reference placement has no screen-position preview.");
            var frame = diagram.Children.OfType<Border>().Single(b => AutomationProperties.GetName(b) == "屏幕工作区");
            var region = diagram.Children.OfType<Border>().Single(b => AutomationProperties.GetName(b) == "放置目标区域");
            var buttons = Elements<Button>(picker).Where(b => AutomationProperties.GetName(b).StartsWith("借用窗口 ")).ToArray();
            Equal(2, buttons.Length);
            buttons[0].RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
            picker.UpdateLayout();
            double left = Canvas.GetLeft(region);
            Near(Canvas.GetLeft(frame), left);
            UiPalette.Apply(false); Screenshot(picker, "paneset-position-left-light");
            buttons[1].RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, null, buttons[1])
                { RoutedEvent = Keyboard.GotKeyboardFocusEvent });
            picker.UpdateLayout();
            Equal(true, Canvas.GetLeft(region) > left + frame.Width / 3);
            Near(region.Width / before[1].Bounds.Width, region.Height / before[1].Bounds.Height);
            Screenshot(picker, "paneset-position-right-light");
            UiPalette.Apply(true); Screenshot(picker, "paneset-position-right-dark");
            var search = Elements<TextBox>(picker).Single(b => AutomationProperties.GetName(b) == "搜索参照窗口");
            search.Text = targets[0].Title; picker.UpdateLayout(); Near(left, Canvas.GetLeft(region));
            search.Text = "no matching position preview"; picker.UpdateLayout();
            Equal(Visibility.Collapsed, region.Visibility);
            search.Text = ""; picker.UpdateLayout(); Near(left, Canvas.GetLeft(region));
            picker.Width = 580; picker.UpdateLayout(); Screenshot(picker, "paneset-position-narrow");
            var currentFirstButton = Elements<Button>(picker).First(b => AutomationProperties.GetName(b).StartsWith("借用窗口 "));
            Equal(true, diagram.TransformToAncestor(picker).Transform(new Point()).Y < currentFirstButton.TransformToAncestor(picker).Transform(new Point()).Y);
            picker.Width = 340; picker.Height = 260; picker.UpdateLayout(); Screenshot(picker, "paneset-position-small");
            for (int i = 0; i < targets.Length; i++) Equal(before[i], manager.ReadState(targets[i]));
            Equal(positions.Version, manager.RecentPositions!.Version); Equal(true, manager.HasUndo);
            Console.WriteLine("POSITION PREVIEW: equal-sized left/right owned windows differ spatially; hover/focus/filter/resize do not change native geometry or history. Routed events, not real input acceptance.");
        }
        finally { picker.Cancel(); UiPalette.Apply(dark); owner.Exit(); }
    }

    private static Task CheckReferencePreviewGeometry()
    {
        var preview = new ReferencePositionPreview();
        preview.Measure(new Size(300, 220)); preview.Arrange(new Rect(0, 0, 300, 220)); preview.UpdateLayout();
        var frame = preview.Children.OfType<Border>().Single(b => AutomationProperties.GetName(b) == "屏幕工作区");
        var region = preview.Children.OfType<Border>().Single(b => AutomationProperties.GetName(b) == "放置目标区域");
        var negative = new WindowRect(-1920, 40, 1920, 1040);
        preview.ShowPosition(negative, new(-1920, 40, 960, 1040));
        Near(Canvas.GetLeft(frame), Canvas.GetLeft(region)); Near(Canvas.GetTop(frame), Canvas.GetTop(region));
        Near(frame.Width / 2, region.Width); Near(frame.Height, region.Height);
        preview.ShowPosition(negative, new(-960, 40, 960, 1040));
        Near(Canvas.GetLeft(frame) + frame.Width / 2, Canvas.GetLeft(region));
        Near(region.Width / 960, region.Height / 1040);
        var portrait = new WindowRect(240, -2560, 1080, 2480);
        preview.ShowPosition(portrait, new(780, -1320, 540, 1240));
        Near(frame.Width / 1080, frame.Height / 2480);
        Near(Canvas.GetLeft(frame) + frame.Width / 2, Canvas.GetLeft(region));
        Near(Canvas.GetTop(frame) + frame.Height / 2, Canvas.GetTop(region));
        preview.Measure(new Size(180, 220)); preview.Arrange(new Rect(0, 0, 180, 220)); preview.UpdateLayout();
        preview.ShowPosition(negative, new(-960, 40, 960, 1040));
        Near(Canvas.GetLeft(frame) + frame.Width / 2, Canvas.GetLeft(region));
        Near(frame.Width / 2, region.Width); Near(region.Width / 960, region.Height / 1040);
        foreach (var invalid in new[] { new WindowRect(-1921, 40, 960, 1040), new WindowRect(-1920, 0, 960, 1040), new WindowRect(-960, 40, 961, 1040) })
        { preview.ShowPosition(negative, invalid); Equal(Visibility.Collapsed, region.Visibility); }
        preview.ShowPosition(negative, new(-1920, 40, 960, 1040)); Equal(Visibility.Visible, region.Visibility);
        preview.Clear(); Equal(Visibility.Collapsed, region.Visibility);
        Console.WriteLine("POSITION GEOMETRY: same-size left/right, negative origin, top taskbar offset, portrait, resize and invalid bounds verified.");
        return Task.CompletedTask;
    }

    private static void Near(double expected, double actual)
    {
        if (!double.IsFinite(actual) || Math.Abs(expected - actual) > 0.001) throw new Exception($"Expected approximately {expected}, got {actual}.");
    }
}
