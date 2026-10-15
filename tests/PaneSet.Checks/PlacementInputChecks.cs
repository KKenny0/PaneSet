using System.IO;
using System.Windows;
using System.Windows.Controls;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private static async Task CheckPlacementInput()
    {
        string path = Path.Combine(Path.GetTempPath(), "paneset-placement-input-" + Guid.NewGuid().ToString("N") + ".json");
        var binding = new ShortcutBinding(7, 0x87);
        var settings = new ShortcutSettings { Shortcuts = Enum.GetValues<ShortcutAction>().ToDictionary(a => a, _ => (ShortcutBinding?)null) };
        settings.Shortcuts[ShortcutAction.PlaceWindow] = binding; settings.Save(path);
        try
        {
            await using var fixture = await Fixture.Start(4);
            using var manager = new WindowManager();
            var display = WindowManager.DisplayFor(fixture.Handles[0]);
            var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
            var panel = new MainWindow(manager, display, targets, targets[3].Handle, settingsPath: path);
            panel.Show();
            try
            {
                string hint = ((TextBlock)panel.FindName("ShortcutText")).Text;
                if (!hint.Contains(binding + " 单窗放置") || hint.Contains("不可用"))
                    throw new UnavailableException("Placement shortcut is not registered; no real keys injected.");
                await PreparePickerHost(panel);
                SelectWindows(panel, targets[0].Handle, targets[1].Handle, targets[2].Handle);
                ChooseLayout(panel, ArrangementKind.MainAndOthers);
                await PanelAction(panel, manager, "排布所选窗口");
                var positions = manager.RecentPositions!;
                var group = SavedGroup(panel);
                var before = targets.Select(manager.ReadState).ToArray();
                await fixture.Send("top 3");
                var bounds = manager.ReadState(targets[3]).Bounds;
                try { await ClickNativeOwned(fixture, targets[3].Handle, new Native.Point { X = bounds.X + 100, Y = bounds.Y + 12 }); }
                finally { await fixture.Send("untop 3"); }
                await Until(() => Native.GetForegroundWindow() == targets[3].Handle);
                await SendKeys(fixture, targets[3].Handle, 0x11, 0x12, 0x10, 0x87);
                await Until(() => Application.Current.Windows.OfType<PlacementWindow>().Any());
                var picker = Application.Current.Windows.OfType<PlacementWindow>().Single();
                await Until(() => picker.IsReady || !picker.IsVisible);
                Equal(true, picker.IsReady); Equal(targets[3], picker.Source);
                await SendKeys(picker, 0x32);
                await Until(() => !picker.IsVisible && !manager.HasPending);
                Equal(new WindowStateInfo(positions.Regions[1], false), manager.ReadState(targets[3]));
                Equal(targets[3].Handle, Native.GetForegroundWindow()); Equal(group, SavedGroup(panel));
                for (int i = 0; i < 3; i++) Equal(before[i], manager.ReadState(targets[i]));
                await SendKeys(fixture, targets[3].Handle, 0x11, 0x12, 0x10, 0x87);
                await Until(() => Application.Current.Windows.OfType<PlacementWindow>().Any(w => w.IsReady));
                picker = Application.Current.Windows.OfType<PlacementWindow>().Single();
                await SendKeys(picker, 0x1B); await Until(() => !picker.IsVisible);
                Equal(new WindowStateInfo(positions.Regions[1], false), manager.ReadState(targets[3]));
                PlacementSucceeded(await PlacementResult(manager, manager.UndoAsync()));
                Equal(before[3], manager.ReadState(targets[3])); Equal(positions.Version, manager.RecentPositions!.Version);
            }
            finally { panel.Exit(); }
        }
        finally { File.Delete(path); }
    }
}
