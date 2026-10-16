using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PaneSet;

internal sealed class SettingsWindow : Window
{
    private readonly PanelShortcuts? shortcuts;
    private readonly TextBlock notice = new() { TextWrapping = TextWrapping.Wrap, Foreground = UiPalette.Muted };
    public SettingsWindow(Window owner, PanelShortcuts? shortcuts, AppearanceMode theme, Func<AppearanceMode, string> changeTheme)
    {
        this.shortcuts = shortcuts; Owner = owner; Title = "PaneSet · 设置";
        Width = 700; Height = 700; MinWidth = 600; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiPalette.Canvas; Foreground = UiPalette.Ink;
        FontFamily = owner.FontFamily; FontSize = 14;
        Resources[typeof(Button)] = owner.FindResource(typeof(Button));
        var root = new DockPanel { Margin = new Thickness(20) }; Content = root;
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = "设置", FontSize = 23, FontWeight = FontWeights.SemiBold, Foreground = UiPalette.HeaderInk });
        heading.Children.Add(new TextBlock { Text = "外观与快捷键", Margin = new Thickness(0, 5, 0, 0), Foreground = UiPalette.HeaderSub });
        var banner = new Border { Child = heading, Background = UiPalette.Header, BorderBrush = UiPalette.Line, BorderThickness = new Thickness(0, 0, 0, 1), CornerRadius = new CornerRadius(8), Padding = new Thickness(18, 15, 18, 15), Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(banner, Dock.Top); root.Children.Add(banner);
        var done = new Button { Content = "完成", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Background = UiPalette.Primary, Foreground = UiPalette.PrimaryInk, BorderBrush = UiPalette.Primary, FontWeight = FontWeights.SemiBold };
        done.Click += (_, _) => Close(); DockPanel.SetDock(done, Dock.Bottom); root.Children.Add(done);
        var body = new StackPanel(); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        body.Children.Add(new TextBlock { Text = "外观", FontWeight = FontWeights.SemiBold, Foreground = UiPalette.Ink, Margin = new Thickness(0, 0, 0, 8) });
        var choices = new[] { new ThemeChoice(AppearanceMode.System, "跟随系统"), new ThemeChoice(AppearanceMode.Light, "亮色 · 雾白工作台"), new ThemeChoice(AppearanceMode.Dark, "暗色 · 夜航工作台") };
        var themePicker = new ComboBox { ItemsSource = choices, DisplayMemberPath = nameof(ThemeChoice.Label), SelectedItem = choices.Single(choice => choice.Mode == theme),
            MinHeight = 36, Foreground = UiPalette.Ink, Background = UiPalette.Surface, BorderBrush = UiPalette.Line };
        AutomationProperties.SetName(themePicker, "外观模式");
        themePicker.SelectionChanged += (_, _) =>
        {
            if (themePicker.SelectedItem is not ThemeChoice choice || choice.Mode == theme) return;
            string error = changeTheme(choice.Mode);
            if (error.Length > 0) { notice.Text = error; themePicker.SelectedItem = choices.Single(item => item.Mode == theme); }
            else { theme = choice.Mode; notice.Text = "外观已保存。"; }
        };
        body.Children.Add(new Border { Child = themePicker, Background = UiPalette.Panel, BorderBrush = UiPalette.Line,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 14) });
        body.Children.Add(notice); notice.Text = shortcuts?.Notice ?? "此检查窗口未启用全局注册。";
        body.Children.Add(new TextBlock { Text = "快捷键", FontWeight = FontWeights.SemiBold, Foreground = UiPalette.Ink, Margin = new Thickness(0, 14, 0, 8) });
        body.Children.Add(new TextBlock { Text = "单窗放置：保持要移动的窗口激活，将鼠标指向另一扇窗口后按放置键。被遮挡的位置可从主面板“选择其他位置…”进入。",
            Foreground = UiPalette.Muted, TextWrapping = TextWrapping.Wrap, LineHeight = 20, Margin = new Thickness(0, 0, 0, 12) });
        if (shortcuts != null) foreach (var action in shortcuts.Actions.ToArray()) body.Children.Add(CreateRow(action));
        body.Children.Add(new TextBlock { Text = "面板内固定键", FontWeight = FontWeights.SemiBold, Foreground = UiPalette.Ink, Margin = new Thickness(0, 18, 0, 8) });
        body.Children.Add(new Border { Background = UiPalette.Slate, CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Child =
            new TextBlock { Text = "Alt+1 等宽 · Alt+2 主辅 · Alt+3 网格\nCtrl+F 搜索 · Alt+S 轮换\nEnter 确认 · Esc 收起面板 / 取消快速选窗", Foreground = UiPalette.Ink, TextWrapping = TextWrapping.Wrap, LineHeight = 24 } });
        ThemedChrome.Install(this);
    }
    private FrameworkElement CreateRow(ShortcutAction action)
    {
        var row = new StackPanel();
        row.Children.Add(new TextBlock { Text = PanelShortcuts.Label(action) + " · 全局", FontWeight = FontWeights.SemiBold, Foreground = UiPalette.Ink, FontSize = 15 });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 9), Foreground = UiPalette.Muted, FontSize = 12 }; row.Children.Add(status);
        var editor = new WrapPanel(); row.Children.Add(editor);
        var enabled = new CheckBox { Content = "启用", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Foreground = UiPalette.Ink };
        editor.Children.Add(enabled);
        var modifiers = new Dictionary<uint, CheckBox>();
        foreach (var (value, text) in new[] { (2u, "Ctrl"), (1u, "Alt"), (4u, "Shift"), (8u, "Win") })
        {
            var box = new CheckBox { Content = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Foreground = UiPalette.Ink };
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
            notice.Text = error.Length == 0 ? "已保存。" + shortcuts.Notice : error;
            if (error.Length == 0) Refresh(); else status.Text = "保存值：" + (shortcuts.BindingFor(action)?.ToString() ?? "禁用") + " · " + shortcuts.StatusFor(action);
        };
        reset.Click += (_, _) => { string error = shortcuts!.Change(action, ShortcutSettings.Default(action)); notice.Text = error.Length == 0 ? "已恢复默认。" + shortcuts.Notice : error; Refresh(); };
        Refresh(); return new Border { Child = row, Background = UiPalette.Surface, BorderBrush = UiPalette.Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 9) };
    }
    private sealed record KeyChoice(uint Key, string Label)
    {
        public override string ToString() => Label;
    }
    private sealed record ThemeChoice(AppearanceMode Mode, string Label)
    {
        public override string ToString() => Label;
    }
}
