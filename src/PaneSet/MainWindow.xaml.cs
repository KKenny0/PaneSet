using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PaneSet;

public partial class MainWindow : Window
{
    private readonly WindowManager manager;
    private DisplayArea display;
    private readonly ObservableCollection<WindowRow> rows = [];
    private readonly List<WindowRow> selected = [];
    private readonly ICollectionView view;
    private readonly DispatcherTimer timer;
    private bool updating, busy, arranging;

    public MainWindow(WindowManager manager, DisplayArea display, IReadOnlyList<WindowTarget> candidates, nint foreground = 0)
    {
        this.manager = manager;
        this.display = display;
        InitializeComponent();
        view = CollectionViewSource.GetDefaultView(rows);
        view.Filter = item => item is WindowRow row && (row.Target.Title.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase)
            || row.Target.Application.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase));
        WindowList.ItemsSource = view;
        SetCandidates(candidates, foreground);
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, async (_, _) =>
        {
            if (!busy && manager.CanCollect)
            {
                busy = true;
                try { ShowReport(await manager.CompletePendingAsync()); }
                catch (Exception e) { StatusText.Text = e.Message; }
                finally { busy = false; UpdateControls(); }
            }
        }, Dispatcher);
        Closing += (_, e) =>
        {
            if (busy || manager.HasPending)
            {
                e.Cancel = true;
                if (MessageBox.Show(this, "目标窗口仍未响应。退出后无法恢复这次排布，目标稍后仍可能被移动。是否退出？",
                    "退出 PaneSet", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
                    Environment.Exit(0);
            }
        };
        SizeChanged += (_, _) => PreviewView.Height = ActualHeight < 680 ? 85 : 130;
        Closed += (_, _) => { timer.Stop(); manager.Dispose(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
            else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
        StatusText.Text = "可选择被遮挡的窗口。恢复记录保留到应用退出。";
    }

    private void SetCandidates(IReadOnlyList<WindowTarget> candidates, nint foreground)
    {
        updating = true;
        selected.Clear(); rows.Clear();
        foreach (var candidate in candidates) rows.Add(new WindowRow(candidate, SelectionChanged));
        updating = false;
        var initial = rows.FirstOrDefault(r => r.Target.Handle == foreground);
        if (initial != null) initial.Selected = true;
        UpdatePreview();
    }

    private void SelectionChanged(WindowRow row)
    {
        if (updating) return;
        if (row.Selected)
        {
            if (selected.Count == 2)
            {
                updating = true; row.Selected = false; updating = false;
                StatusText.Text = "已选两扇窗口，请取消一扇后再选择。";
                return;
            }
            selected.Add(row);
        }
        else selected.Remove(row);
        UpdatePreview();
    }

    private void UpdateControls()
    {
        bool available = !busy && !manager.HasPending;
        ApplyButton.IsEnabled = available && selected.Count == 2;
        UndoButton.IsEnabled = available && manager.HasUndo;
        RefreshButton.IsEnabled = available;
        SwapButton.IsEnabled = available && selected.Count == 2;
        WindowList.IsEnabled = available;
        ChosenCount.Text = $"已选 {selected.Count} / 2";
    }

    private void UpdatePreview()
    {
        foreach (var row in rows) row.Order = selected.IndexOf(row) + 1;
        ScreenLabel.Text = $"当前桌面 · 目标显示器 {display.WorkArea.Width} × {display.WorkArea.Height}";
        PreviewCanvas.Width = display.WorkArea.Width;
        PreviewCanvas.Height = display.WorkArea.Height;
        PreviewCanvas.Children.Clear();
        var layout = LayoutEngine.TwoColumns(display.WorkArea);
        for (int i = 0; i < 2; i++)
        {
            var cell = new Border
            {
                Width = layout[i].Width, Height = layout[i].Height, CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(i < selected.Count ? "#D4E4D0" : "#F8FAF4")),
                Child = new TextBlock { Text = (i + 1).ToString(), FontSize = 216, Foreground = new SolidColorBrush(Color.FromRgb(39, 102, 78)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            Canvas.SetLeft(cell, layout[i].X - display.WorkArea.X);
            PreviewCanvas.Children.Add(cell);
        }
        static string Caption(string title) => title.Length > 28 ? title[..28] : title;
        LeftLabel.Text = selected.Count > 0 ? "左侧 · " + Caption(selected[0].Target.Title) : "左侧 · 等待选择";
        RightLabel.Text = selected.Count > 1 ? "右侧 · " + Caption(selected[1].Target.Title) : "右侧 · 等待选择";
        LeftLabel.ToolTip = selected.Count > 0 ? selected[0].Target.Title : null;
        RightLabel.ToolTip = selected.Count > 1 ? selected[1].Target.Title : null;
        EmptyLabel.Visibility = view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        EmptyLabel.Text = rows.Count == 0 ? "没有可选窗口，请打开同一显示器中的普通窗口后刷新。" : "没有匹配的窗口，试试其他标题或应用名称。";
        UpdateControls();
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (view == null) return;
        view.Refresh();
        UpdatePreview();
    }

    private void SwapClick(object sender, RoutedEventArgs e) { selected.Reverse(); UpdatePreview(); }
    private void RefreshClick(object sender, RoutedEventArgs e)
    {
        try
        {
            display = new(display.Handle, Native.Monitor(display.Handle).Work.Value);
            SetCandidates(manager.ListWindows(display), 0);
            StatusText.Text = "窗口列表已刷新。";
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private async void ApplyClick(object sender, RoutedEventArgs e)
    {
        if (busy || manager.HasPending || selected.Count != 2) return;
        busy = true; arranging = true; UpdateControls();
        try { ShowReport(await manager.ApplyAsync(selected.Select(r => r.Target).ToArray(), display)); }
        catch (Exception exception) { StatusText.Text = exception.Message; }
        finally { busy = false; UpdateControls(); }
    }

    private async void UndoClick(object sender, RoutedEventArgs e)
    {
        if (busy || manager.HasPending) return;
        busy = true; arranging = false; UpdateControls();
        try { ShowReport(await manager.UndoAsync()); }
        catch (Exception exception) { StatusText.Text = exception.Message; }
        finally { busy = false; UpdateControls(); }
    }

    private void ShowReport(OperationReport report)
    {
        if (report.Pending) { StatusText.Text = "正在等待目标窗口响应，完成前暂停排布和恢复。"; return; }
        StatusText.Text = string.Join("\n", report.Windows.Select(w => w.Target.Title + "：" + w.Message));
        if (report.NeedsRefresh) return;
        if (report.Success && arranging)
        {
            StatusText.Text = "已排布。需要恢复时，从任务栏打开 PaneSet。恢复记录保留到退出。";
            WindowState = System.Windows.WindowState.Minimized;
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}

public sealed class WindowRow(WindowTarget target, Action<WindowRow> changed) : INotifyPropertyChanged
{
    public WindowTarget Target { get; } = target;
    public string DisplayTitle => Target.Title.Length > 64 ? Target.Title[..64] : Target.Title;
    private bool selected;
    private int order;
    public bool Selected
    {
        get => selected;
        set { if (selected == value) return; selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); changed(this); }
    }
    public int Order
    {
        get => order;
        set { if (order == value) return; order = value; PropertyChanged?.Invoke(this, new(nameof(OrderText))); }
    }
    public string OrderText => order > 0 ? order.ToString() : "·";
    public event PropertyChangedEventHandler? PropertyChanged;
}
