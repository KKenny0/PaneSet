using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PaneSet;

public partial class MainWindow
{
    private PlacementWindow? placementWindow;
    private WindowTarget? placementSource;
    private bool placing;
    private bool pointerPlacing;
    private PlacementFeedback? placementFeedback;
    private WindowRect? pointerTarget;
    private DisplayArea? pointerDisplay;

    private void ShowPointerFeedback(WindowRect? target, string message = "")
    {
        if (closed) return;
        placementFeedback?.Dispose(); placementFeedback = null;
        try { placementFeedback = new PlacementFeedback(pointerDisplay ?? WindowManager.DisplayFor(0), target, message); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { StatusText.Text += " 反馈显示失败：" + e.Message; }
    }

    internal async Task PlaceFromPointerAsync(nint foreground)
    {
        if (closed || Picking || settingsWindow != null) return;
        if (busy || manager.HasPending) { ShowPointerFeedback(null, "正在等待窗口响应，完成前不能再次放置。"); return; }
        busy = true; placing = pointerPlacing = true; arranging = false; requestedGroup = null; UpdateControls();
        placementFeedback?.Dispose(); placementFeedback = null; pointerTarget = null; pointerDisplay = null;
        try
        {
            var request = manager.CapturePointerPlacement(foreground);
            pointerTarget = request.Reference.State.Bounds; pointerDisplay = request.Reference.Environment.Display;
            ShowPointerFeedback(pointerTarget);
            if (placementFeedback == null) { placing = pointerPlacing = false; return; }
            ShowReport(await manager.PlaceFromPointerAsync(request));
        }
        catch (Exception e)
        {
            placing = pointerPlacing = false;
            if (!closed) { StatusText.Text = e.Message; ShowPointerFeedback(null, e.Message); }
        }
        finally { busy = false; UpdateControls(); }
    }
    private bool Picking => selectionWindow != null || placementWindow != null;

    private void CapturePlacementSource(IReadOnlyList<WindowTarget> candidates, nint foreground)
    {
        placementSource = candidates.FirstOrDefault(t => t.Handle == foreground);
        PlacementSourceText.Text = placementSource?.Title ?? "先从目标窗口唤起面板";
        PlacementSourceText.ToolTip = placementSource?.Title ?? "先从目标窗口唤起面板";
        UpdateControls();
    }
    private void ClearPlacementSource() => CapturePlacementSource([], 0);
    private void WatchPlacementSource()
    {
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) ClearPlacementSource(); };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (closed) return;
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out uint pid);
            if (pid != Environment.ProcessId) ClearPlacementSource();
        }));
    }
    private void PlaceWindowClick(object sender, RoutedEventArgs e) => OpenPlacement(0, panelSource: true);

    internal PlacementWindow? OpenPlacement(nint foreground, ShortcutBinding? chord = null, bool panelSource = false)
    {
        if (placementWindow != null) { placementWindow.Cancel(); return null; }
        if (closed || busy || manager.HasPending || selectionWindow != null || settingsWindow != null) return null;
        try
        {
            var source = panelSource ? placementSource : manager.ListWindows().FirstOrDefault(t => t.Handle == foreground);
            if (source == null) throw new InvalidOperationException("没有合格源窗口，请先从目标窗口唤起 PaneSet。");
            if (!manager.CanSelectReference(source, out _, out string message)) throw new InvalidOperationException(message);
            var positions = manager.RecentPositions;
            var references = manager.ListWindows().Where(t => t.Handle != source.Handle).ToArray();
            var picker = new PlacementWindow(this, manager, source, positions, chord, references);
            placementWindow = picker; UpdateControls();
            picker.Completed += choice =>
            {
                if (placementWindow == picker) placementWindow = null;
                if (closed) return;
                UpdateControls();
                if (choice.Reference is { } target) _ = PlaceSourceAsync(source, 0, 0, target);
                else if (choice.Index is int index && positions != null) _ = PlaceSourceAsync(source, positions.Version, index);
                else
                {
                    StatusText.Text = choice.Message;
                    if (choice.Failed) Native.ShowWindow(new WindowInteropHelper(this).Handle, 4);
                }
            };
            WindowState = WindowState.Minimized;
            picker.Show();
            if (picker.IsVisible && Native.GetForegroundWindow() != new WindowInteropHelper(picker).Handle)
                Native.SetForegroundWindow(new WindowInteropHelper(picker).Handle);
            return picker;
        }
        catch (Exception e)
        {
            placementWindow?.Cancel(); placementWindow = null;
            StatusText.Text = e.Message; UpdateControls();
            if (!closed) Native.ShowWindow(new WindowInteropHelper(this).Handle, 4);
            return null;
        }
    }

    private async Task PlaceSourceAsync(WindowTarget source, long version, int index, WindowTarget? reference = null)
    {
        if (closed || busy || manager.HasPending || Picking || settingsWindow != null) return;
        busy = true; placing = true; arranging = false; requestedGroup = null; UpdateControls();
        try { ShowReport(await (reference == null ? manager.PlaceAsync(source, version, index) : manager.PlaceAtReferenceAsync(source, reference))); }
        catch (Exception e) { placing = false; StatusText.Text = e.Message; if (!closed) Native.ShowWindow(new WindowInteropHelper(this).Handle, 4); }
        finally { busy = false; UpdateControls(); }
    }

    internal void DispatchShortcut(ShortcutAction action, nint foreground)
    {
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        if (placementWindow != null)
        {
            if (action == ShortcutAction.PlaceWindow) placementWindow.Cancel();
            else if (action == ShortcutAction.OpenPanel) placementWindow.Activate();
            return;
        }
        switch (action)
        {
            case ShortcutAction.OpenPanel: OpenForForeground(foreground); break;
            case ShortcutAction.Restore: UndoClick(this, new()); break;
            case ShortcutAction.ArrangeScreen: _ = ArrangeCurrentScreenAsync(foreground); break;
            case ShortcutAction.SelectScreen: OpenQuickSelection(foreground); break;
            case ShortcutAction.PlaceWindow: _ = PlaceFromPointerAsync(foreground); break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }
}
