using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static object? SavedGroup(MainWindow window) => typeof(MainWindow).GetField("recentGroup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
    private static WindowTarget[] SavedTargets(MainWindow window) => (WindowTarget[])SavedGroup(window)!.GetType().GetProperty("Targets")!.GetValue(SavedGroup(window))!;
    private static ArrangementKind SavedLayout(MainWindow window) => (ArrangementKind)SavedGroup(window)!.GetType().GetProperty("Layout")!.GetValue(SavedGroup(window))!;
    private static nint[] SelectedHandles(MainWindow window) => ((ItemsControl)window.FindName("WindowList")).Items.Cast<WindowRow>()
        .Where(row => row.Selected).OrderBy(row => row.Order).Select(row => row.Target.Handle).ToArray();
    private static void EqualHandles(IEnumerable<nint> expected, IEnumerable<nint> actual) => Equal(string.Join(",", expected), string.Join(",", actual));
    private static void ChooseLayout(MainWindow window, ArrangementKind kind)
    {
        var picker = (ComboBox)window.FindName("LayoutPicker");
        picker.SelectedItem = picker.Items.Cast<ComboBoxItem>().Single(item => Equals(item.Tag, kind));
    }
    private static void SelectWindows(MainWindow window, params nint[] handles)
    {
        ((TextBox)window.FindName("SearchBox")).Clear(); window.UpdateLayout();
        foreach (var box in Elements<CheckBox>(window).Where(box => box.IsChecked == true).ToArray()) Toggle(box);
        foreach (var handle in handles) Toggle(Elements<CheckBox>(window).Single(box => ((WindowRow)box.DataContext).Target.Handle == handle));
    }
    private static async Task PanelAction(MainWindow window, WindowManager manager, string name)
    {
        var status = (TextBlock)window.FindName("StatusText");
        string marker = Guid.NewGuid().ToString(); status.Text = marker;
        Invoke(ButtonNamed(window, name));
        await Until(() => status.Text != marker && !manager.HasPending && ((Button)window.FindName("RefreshButton")).IsEnabled);
    }
    private static async Task LoadRecent(MainWindow window)
    {
        var status = (TextBlock)window.FindName("StatusText");
        string marker = Guid.NewGuid().ToString(); status.Text = marker;
        Invoke(ButtonNamed(window, "调整最近组"));
        await Until(() => status.Text != marker); window.UpdateLayout();
    }
    private static void ReplaceSavedGroup(MainWindow window, WindowTarget[]? targets = null, DisplayArea? destination = null)
    {
        // Only invalid-identity/monitor cases seed metadata; success and failure publication use real UI operations.
        var field = typeof(MainWindow).GetField("recentGroup", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var group = field.GetValue(window)!;
        var type = group.GetType();
        field.SetValue(window, Activator.CreateInstance(type, targets ?? SavedTargets(window),
            destination ?? (DisplayArea)type.GetProperty("Destination")!.GetValue(group)!, SavedLayout(window)));
    }

    private static async Task CheckRecentGroup()
    {
        await using var fixture = await Fixture.Start(4);
        using var manager = new WindowManager();
        var display = WindowManager.ListDisplays().Last();
        var candidates = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var window = new MainWindow(manager, display, candidates, enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            Equal(false, ((Button)window.FindName("RecentButton")).IsEnabled);
            SelectWindows(window, fixture.Handles[2], fixture.Handles[0], fixture.Handles[1]);
            ChooseLayout(window, ArrangementKind.Columns);
            await PanelAction(window, manager, "排布所选窗口");
            EqualHandles([fixture.Handles[2], fixture.Handles[0], fixture.Handles[1]], SavedTargets(window).Select(t => t.Handle));
            var first = candidates.ToDictionary(t => t.Handle, manager.ReadState);
            window.OpenForForeground(fixture.Handles[3]); window.UpdateLayout();
            EqualHandles([fixture.Handles[3]], SelectedHandles(window));
            ((TextBox)window.FindName("SearchBox")).Text = "not found";
            await LoadRecent(window);
            Equal("", ((TextBox)window.FindName("SearchBox")).Text);
            EqualHandles([fixture.Handles[2], fixture.Handles[0], fixture.Handles[1]], SelectedHandles(window));
            Equal(display.Handle, ((DisplayArea)((ComboBox)window.FindName("DisplayPicker")).SelectedItem).Handle);
            Equal(true, manager.HasUndo);
            foreach (var target in candidates) Equal(first[target.Handle], manager.ReadState(target));
            ChooseLayout(window, ArrangementKind.MainAndOthers);
            await PanelAction(window, manager, "排布所选窗口");
            Equal(ArrangementKind.MainAndOthers, SavedLayout(window));
            window.WindowState = WindowState.Normal;
            await PanelAction(window, manager, "恢复上次排布");
            foreach (var target in candidates) Equal(first[target.Handle], manager.ReadState(target));
            await fixture.Send($"position 0 {display.WorkArea.X + 150} {display.WorkArea.Y + 100}");
            var moved = candidates.ToDictionary(t => t.Handle, manager.ReadState);
            await LoadRecent(window);
            foreach (var target in candidates) Equal(moved[target.Handle], manager.ReadState(target));
            await PanelAction(window, manager, "排布所选窗口");
            window.WindowState = WindowState.Normal;
            await PanelAction(window, manager, "恢复上次排布");
            foreach (var target in candidates) Equal(moved[target.Handle], manager.ReadState(target));
            ((TextBox)window.FindName("SearchBox")).Text = "PaneSet.Checks";
            window.Width = 600; window.Height = 600; await Task.Delay(60);
            CheckVisibleControls(window); Screenshot(window, "paneset-recent-minimum");
            window.Width = 820; window.Height = 730; await Task.Delay(60);
            Screenshot(window, "paneset-recent");
            window.Close(); Equal(WindowState.Minimized, window.WindowState); Equal(true, SavedGroup(window) != null);
        }
        finally { window.Exit(); }
        Equal(true, SavedGroup(window) == null);
    }

    private static async Task CheckRecentNoop()
    {
        await using var fixture = await Fixture.Start();
        await fixture.Send("max 0");
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var originals = candidates.ToDictionary(t => t.Handle, manager.ReadState);
        var window = new MainWindow(manager, display, candidates, enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            SelectWindows(window, fixture.Handles[0], fixture.Handles[1]); ChooseLayout(window, ArrangementKind.Columns);
            await PanelAction(window, manager, "排布所选窗口");
            var before = candidates.ToDictionary(t => t.Handle, manager.ReadState);
            window.WindowState = WindowState.Normal; ChooseLayout(window, ArrangementKind.Grid);
            await PanelAction(window, manager, "排布所选窗口");
            Equal(ArrangementKind.Grid, SavedLayout(window)); Equal(true, manager.HasUndo);
            foreach (var target in candidates) Equal(before[target.Handle], manager.ReadState(target));
            window.WindowState = WindowState.Normal; await PanelAction(window, manager, "恢复上次排布");
            foreach (var target in candidates) Equal(originals[target.Handle], manager.ReadState(target));
            await LoadRecent(window); EqualHandles(fixture.Handles.Take(2), SelectedHandles(window));
        }
        finally { window.Exit(); }
    }

    private static async Task CheckRecentMissing()
    {
        await using var fixture = await Fixture.Start(4);
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var window = new MainWindow(manager, display, candidates, enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            SelectWindows(window, fixture.Handles[0], fixture.Handles[1], fixture.Handles[2]);
            await PanelAction(window, manager, "排布所选窗口"); window.WindowState = WindowState.Normal;
            var originalGroup = SavedTargets(window).ToArray();
            await fixture.Send("long"); await LoadRecent(window);
            EqualHandles(fixture.Handles.Take(3), SelectedHandles(window));
            var current = ((ItemsControl)window.FindName("WindowList")).Items.Cast<WindowRow>().Single(row => row.Target.Handle == fixture.Handles[0]);
            Equal(false, current.Target.Title == originalGroup[0].Title);
            ReplaceSavedGroup(window, [originalGroup[0] with { Generation = originalGroup[0].Generation + 100 }, originalGroup[1], originalGroup[2]]);
            await LoadRecent(window); EqualHandles([fixture.Handles[1], fixture.Handles[2]], SelectedHandles(window));
            Equal(true, ((TextBlock)window.FindName("StatusText")).Text.Contains(originalGroup[0].Title));
            ReplaceSavedGroup(window, originalGroup, new DisplayArea(-1, display.WorkArea));
            await LoadRecent(window); Equal(true, ((ComboBox)window.FindName("DisplayPicker")).SelectedItem == null);
            Equal(false, ((Button)window.FindName("ApplyButton")).IsEnabled);
            Invoke(ButtonNamed(window, "刷新窗口")); await Task.Delay(60);
            Equal(true, ((ComboBox)window.FindName("DisplayPicker")).SelectedItem == null);
            ((ComboBox)window.FindName("DisplayPicker")).SelectedIndex = 0;
            Equal(true, ((Button)window.FindName("ApplyButton")).IsEnabled);
            ReplaceSavedGroup(window, destination: display with { WorkArea = display.WorkArea with { Height = display.WorkArea.Height - 80 } });
            await LoadRecent(window);
            Equal(display.WorkArea.Height, (int)((System.Windows.Controls.Canvas)window.FindName("PreviewCanvas")).Height);
            await fixture.Send("min 1"); await LoadRecent(window); EqualHandles([fixture.Handles[0], fixture.Handles[2]], SelectedHandles(window));
            await fixture.Send("close 2"); await LoadRecent(window); EqualHandles([fixture.Handles[0]], SelectedHandles(window));
            Equal(false, ((Button)window.FindName("ApplyButton")).IsEnabled);
            Equal(true, manager.HasUndo);
            ((TextBox)window.FindName("SearchBox")).Text = "PaneSet.Checks"; await Task.Delay(60);
            Screenshot(window, "paneset-recent-missing");
        }
        finally { window.Exit(); }
    }

    private static async Task CheckRecentFailures()
    {
        await using var fixture = await Fixture.Start(4);
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var window = new MainWindow(manager, display, candidates, enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            SelectWindows(window, fixture.Handles[0], fixture.Handles[1]); ChooseLayout(window, ArrangementKind.Columns);
            await PanelAction(window, manager, "排布所选窗口"); window.WindowState = WindowState.Normal;
            var initial = candidates.ToDictionary(t => t.Handle, manager.ReadState);
            SelectWindows(window, fixture.Handles[2], fixture.Handles[3]); ChooseLayout(window, ArrangementKind.MainAndOthers);
            await fixture.Send("block 3"); await PanelAction(window, manager, "排布所选窗口");
            EqualHandles(fixture.Handles.Take(2), SavedTargets(window).Select(t => t.Handle)); Equal(true, manager.HasUndo);
            await fixture.Send("unblock"); await PanelAction(window, manager, "恢复上次排布");
            foreach (var target in candidates) Equal(initial[target.Handle], manager.ReadState(target));
            SelectWindows(window, fixture.Handles[1], fixture.Handles[2]);
            await fixture.Send("stall"); Invoke(ButtonNamed(window, "排布所选窗口"));
            await Until(() => manager.HasPending);
            Equal(false, ((Button)window.FindName("RecentButton")).IsEnabled);
            typeof(MainWindow).GetMethod("RecentClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
            EqualHandles([fixture.Handles[1], fixture.Handles[2]], SelectedHandles(window));
            EqualHandles(fixture.Handles.Take(2), SavedTargets(window).Select(t => t.Handle));
            await Until(() => !manager.HasPending && ((Button)window.FindName("RefreshButton")).IsEnabled && window.WindowState == WindowState.Minimized);
            EqualHandles([fixture.Handles[1], fixture.Handles[2]], SavedTargets(window).Select(t => t.Handle));
            window.WindowState = WindowState.Normal;
            SelectWindows(window, fixture.Handles[0], fixture.Handles[3]);
            await fixture.Send("block 3"); await fixture.Send("stall");
            await PanelAction(window, manager, "排布所选窗口");
            EqualHandles([fixture.Handles[1], fixture.Handles[2]], SavedTargets(window).Select(t => t.Handle));
            await fixture.Send("unblock"); await PanelAction(window, manager, "恢复上次排布");
            await fixture.Send("close 3");
            await PanelAction(window, manager, "排布所选窗口");
            EqualHandles([fixture.Handles[1], fixture.Handles[2]], SavedTargets(window).Select(t => t.Handle));
        }
        finally { window.Exit(); }
    }

    private static async Task CheckRecentUiError()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var window = new MainWindow(manager, display, candidates, enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            await fixture.Send("close 0");
            bool failed = false;
            try { typeof(MainWindow).GetMethod("SetCandidates", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [candidates, (nint)0, false, null]); }
            catch (TargetInvocationException e) when (e.InnerException is System.ComponentModel.Win32Exception or InvalidOperationException) { failed = true; }
            Equal(true, failed);
            Equal(false, (bool)typeof(MainWindow).GetField("updating", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!);
            Invoke(ButtonNamed(window, "刷新窗口")); await Task.Delay(60);
            SelectWindows(window, fixture.Handles[1], fixture.Handles[2]);
            Equal(true, ((Button)window.FindName("ApplyButton")).IsEnabled);
        }
        finally { window.Exit(); }
    }

    private delegate bool EnumWindowProc(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int count);
    private static async Task CheckRecentExit()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var window = new MainWindow(manager, display, candidates, enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            SelectWindows(window, fixture.Handles[0], fixture.Handles[1]);
            await fixture.Send("hang"); Invoke(ButtonNamed(window, "排布所选窗口")); await Until(() => manager.HasPending);
            nint owner = new WindowInteropHelper(window).Handle;
            var confirm = Task.Run(async () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    nint dialog = 0;
                    EnumWindows((handle, _) =>
                    {
                        if (GetWindow(handle, 4) != owner) return true;
                        GetWindowThreadProcessId(handle, out uint pid);
                        var name = new StringBuilder(64); GetClassName(handle, name, name.Capacity);
                        if (pid == Environment.ProcessId && name.ToString() == "#32770") { dialog = handle; return false; }
                        return true;
                    }, 0);
                    if (dialog != 0) { Equal(true, PostMessage(dialog, 0x111, 6, 0)); return; }
                    await Task.Delay(25);
                }
                throw new Exception("Owned pending-exit dialog did not appear.");
            });
            window.Exit(); await confirm;
            Equal(true, SavedGroup(window) == null);
            fixture.Terminate(); await manager.CompletePendingAsync().WaitAsync(TimeSpan.FromSeconds(5));
            typeof(MainWindow).GetMethod("ShowReport", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window,
                [new OperationReport(candidates.Take(2).Select(t => new WindowOutcome(t, true, "late result")).ToArray())]);
            Equal(true, SavedGroup(window) == null);
        }
        finally { fixture.Terminate(); if (window.IsVisible) window.Exit(); }
    }
}
