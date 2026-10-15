using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PaneSet;

public partial class MainWindow
{
    private PlacementWindow? placementWindow;
    private WindowTarget? placementSource;
    private bool placing;
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
            var positions = manager.RecentPositions ?? throw new InvalidOperationException("先用 PaneSet 排布一次窗口，再使用单窗放置。");
            if (!manager.CanPlace(source, positions.Version, out string message)) throw new InvalidOperationException(message);
            var picker = new PlacementWindow(this, manager, source, positions, chord);
            placementWindow = picker; UpdateControls();
            picker.Completed += choice =>
            {
                if (placementWindow == picker) placementWindow = null;
                if (closed) return;
                UpdateControls();
                if (choice.Index is int index) _ = PlaceSourceAsync(source, positions.Version, index);
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

    private async Task PlaceSourceAsync(WindowTarget source, long version, int index)
    {
        if (closed || busy || manager.HasPending || Picking || settingsWindow != null) return;
        busy = true; placing = true; arranging = false; requestedGroup = null; UpdateControls();
        try { ShowReport(await manager.PlaceAsync(source, version, index)); }
        catch (Exception e) { placing = false; StatusText.Text = e.Message; if (!closed) Native.ShowWindow(new WindowInteropHelper(this).Handle, 4); }
        finally { busy = false; UpdateControls(); }
    }

    private void DispatchShortcut(ShortcutAction action, nint foreground)
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
            case ShortcutAction.PlaceWindow: OpenPlacement(foreground, shortcuts?.BindingFor(action)); break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }
}
