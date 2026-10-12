using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PaneSet;

internal sealed class SettingsWindow : Window
{
    private readonly PanelShortcuts? shortcuts;
    private readonly TextBlock notice = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkRed };
    public SettingsWindow(Window owner, PanelShortcuts? shortcuts)
    {
        this.shortcuts = shortcuts; Owner = owner; Title = "PaneSet · 设置";
        Width = 700; Height = 700; MinWidth = 600; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(244, 245, 241));
        FontFamily = owner.FontFamily; FontSize = 14;
        Resources[typeof(Button)] = owner.FindResource(typeof(Button));
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        heading.Children.Add(new TextBlock { Text = "快捷键设置", FontSize = 24, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "全局键在其他应用中也可使用；面板内键保持固定。", Margin = new Thickness(0, 6, 0, 0) });
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var done = new Button { Content = "完成", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        done.Click += (_, _) => Close(); DockPanel.SetDock(done, Dock.Bottom); root.Children.Add(done);
        var body = new StackPanel(); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        body.Children.Add(notice); notice.Text = shortcuts?.Notice ?? "此检查窗口未启用全局注册。";
        if (shortcuts != null) foreach (var action in shortcuts.Actions.ToArray()) body.Children.Add(CreateRow(action));
        body.Children.Add(new TextBlock { Text = "面板内固定键", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 8) });
        body.Children.Add(new TextBlock { Text = "Alt+1 等宽 · Alt+2 主辅 · Alt+3 网格\nCtrl+F 搜索 · Alt+S 轮换\nEnter 确认 · Esc 收起面板 / 取消快速选窗", TextWrapping = TextWrapping.Wrap, LineHeight = 24 });
    }
    private FrameworkElement CreateRow(ShortcutAction action)
    {
        var row = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        row.Children.Add(new TextBlock { Text = PanelShortcuts.Label(action) + " · 全局", FontWeight = FontWeights.SemiBold });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6) }; row.Children.Add(status);
        var editor = new WrapPanel(); row.Children.Add(editor);
        var enabled = new CheckBox { Content = "启用", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        editor.Children.Add(enabled);
        var modifiers = new Dictionary<uint, CheckBox>();
        foreach (var (value, text) in new[] { (2u, "Ctrl"), (1u, "Alt"), (4u, "Shift"), (8u, "Win") })
        {
            var box = new CheckBox { Content = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            modifiers[value] = box; editor.Children.Add(box);
        }
        var keys = Enumerable.Range(1, 255).Where(k => new ShortcutBinding(1, (uint)k).Valid)
            .Select(k => new KeyChoice((uint)k, KeyInterop.KeyFromVirtualKey(k).ToString())).GroupBy(k => k.Label).Select(g => g.First()).ToArray();
        var key = new ComboBox { ItemsSource = keys, Width = 100, MinHeight = 32, DisplayMemberPath = nameof(KeyChoice.Label) };
        AutomationProperties.SetName(key, action + " 主键"); editor.Children.Add(key);
        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; row.Children.Add(buttons);
        var save = new Button { Content = "保存", Padding = new Thickness(12, 6, 12, 6), MinHeight = 32 };
        var reset = new Button { Content = "恢复默认", Padding = new Thickness(12, 6, 12, 6), MinHeight = 32, Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetName(save, action + " 保存"); AutomationProperties.SetName(reset, action + " 恢复默认");
        buttons.Children.Add(save); buttons.Children.Add(reset);
        void Refresh()
        {
            var binding = shortcuts!.BindingFor(action); enabled.IsChecked = binding != null;
            foreach (var pair in modifiers) pair.Value.IsChecked = (binding?.Modifiers & pair.Key) != 0 && binding != null;
            key.SelectedItem = keys.FirstOrDefault(k => k.Key == (binding?.Key ?? ShortcutSettings.Default(action).Key));
            status.Text = "保存值：" + (binding?.ToString() ?? "禁用") + " · " + shortcuts.StatusFor(action);
        }
        save.Click += (_, _) =>
        {
            var binding = enabled.IsChecked == true ? new ShortcutBinding(modifiers.Where(p => p.Value.IsChecked == true).Aggregate(0u, (n, p) => n | p.Key), (key.SelectedItem as KeyChoice)?.Key ?? 0) : null;
            string error = shortcuts!.Change(action, binding);
            notice.Text = error.Length == 0 ? "已保存。" : error;
            if (error.Length == 0) Refresh(); else status.Text = "保存值：" + (shortcuts.BindingFor(action)?.ToString() ?? "禁用") + " · " + shortcuts.StatusFor(action);
        };
        reset.Click += (_, _) => { string error = shortcuts!.Change(action, ShortcutSettings.Default(action)); notice.Text = error.Length == 0 ? "已恢复默认。" : error; Refresh(); };
        Refresh(); return row;
    }
    private sealed record KeyChoice(uint Key, string Label);
}
