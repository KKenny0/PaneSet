using System.Windows;
using System.Windows.Interop;

namespace PaneSet;

public partial class MainWindow
{
    private DragPlacementMonitor? dragMonitor;
    private PlacementFeedback? dragFeedback;
    private bool dragPlacing;
    private WindowRect? dragZone;
    private DisplayArea? dragDisplay;

    private bool Picking => selectionWindow != null;

    internal void StartDragPlacementMonitor()
    {
        if (dragMonitor != null || closed) return;
        try
        {
            dragMonitor = new(manager, () => shortcuts?.DragModifiers ?? ShortcutSettings.DefaultDragModifiers,
                PlaceDragged, ShowDragNote);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { StatusText.Text = "拖动投放不可用：" + e.Message; }
    }

    private void ShowDragNote(string message)
    {
        if (closed) return;
        StatusText.Text = message;
        dragFeedback?.Dispose();
        try { dragFeedback = new PlacementFeedback(WindowManager.DisplayFor(0), null, message); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    private void ShowDragFeedback(WindowRect? zone)
    {
        if (closed) return;
        dragFeedback?.Dispose();
        try { dragFeedback = new PlacementFeedback(dragDisplay ?? WindowManager.DisplayFor(0), zone); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    private async void PlaceDragged(DragSessionStart session, DragPlacementCommit commit, DisplayArea destination)
    {
        if (closed) return;
        if (busy || manager.HasPending || Picking || settingsWindow != null)
        { ShowDragNote("正在等待其他窗口操作，本次未投放。"); return; }
        busy = true; dragPlacing = true; arranging = false; requestedGroup = null; UpdateControls();
        dragZone = commit.Zone; dragDisplay = destination;
        try { ShowReport(await manager.PlaceDraggedAsync(session, commit.Zone, destination)); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { dragPlacing = false; StatusText.Text = e.Message; }
        finally { busy = false; UpdateControls(); }
    }

    internal void DispatchShortcut(ShortcutAction action, nint foreground)
    {
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        if (selectionWindow != null)
        {
            if (action == ShortcutAction.OpenPanel) selectionWindow.Activate();
            return;
        }
        switch (action)
        {
            case ShortcutAction.OpenPanel: OpenForForeground(foreground); break;
            case ShortcutAction.Restore: UndoClick(this, new()); break;
            case ShortcutAction.ArrangeScreen: _ = ArrangeCurrentScreenAsync(foreground); break;
            case ShortcutAction.SelectScreen: OpenQuickSelection(foreground); break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }
}
