using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shell;

namespace PaneSet;

internal static class ThemedChrome
{
    internal static void Install(Window window)
    {
        if (window.Content is not UIElement body) throw new InvalidOperationException("窗口内容尚未创建。");
        window.Content = null;
        window.WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 36,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        });

        var root = new DockPanel { Background = UiPalette.Canvas };
        var caption = new Grid { Height = 36, Background = UiPalette.Header };
        caption.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        caption.ColumnDefinitions.Add(new ColumnDefinition());
        caption.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        caption.Children.Add(new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/PaneSet;component/Assets/Brand.png")),
            Width = 17, Height = 17, Margin = new Thickness(12, 0, 7, 0)
        });
        var title = new TextBlock { Foreground = UiPalette.HeaderInk, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        title.SetBinding(TextBlock.TextProperty, new Binding(nameof(Window.Title)) { Source = window });
        Grid.SetColumn(title, 1); caption.Children.Add(title);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Button(window, "\uE921", "最小化", () => SystemCommands.MinimizeWindow(window)));
        var maximize = Button(window, "\uE922", "最大化或还原", () =>
        {
            if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window);
            else SystemCommands.MaximizeWindow(window);
        });
        window.StateChanged += (_, _) => maximize.Content = window.WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        buttons.Children.Add(maximize);
        buttons.Children.Add(Button(window, "\uE8BB", "关闭", window.Close));
        Grid.SetColumn(buttons, 2); caption.Children.Add(buttons);
        DockPanel.SetDock(caption, Dock.Top);
        root.Children.Add(caption);
        root.Children.Add(body);
        window.Content = root;
    }

    private static Button Button(Window window, string glyph, string name, Action action)
    {
        var button = new Button
        {
            Content = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Style = (Style)window.FindResource("ChromeButtonStyle"),
            Focusable = false,
            ToolTip = name
        };
        AutomationProperties.SetName(button, name);
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        button.Click += (_, _) => action();
        return button;
    }
}
