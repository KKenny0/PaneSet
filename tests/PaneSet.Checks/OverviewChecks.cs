using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc, int x, int y);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sx, int sy, uint flags);
    private static void ComposedScreenshot(Window window, string name)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var origin = window.PointToScreen(new Point()); var client = ClientSize(window); var dpi = VisualTreeHelper.GetDpi(window);
        int width = (int)(client.Width * dpi.DpiScaleX), height = (int)(client.Height * dpi.DpiScaleY);
        foreach (int x in new[] { 1, width / 2, width - 2 }) foreach (int y in new[] { 1, height / 2, height - 2 })
            Equal(handle, WindowFromPoint(new Native.Point { X = (int)origin.X + x, Y = (int)origin.Y + y }));
        var source = GetDC(0); var dc = CreateCompatibleDC(source); var bitmap = CreateCompatibleBitmap(source, width, height); var previous = SelectObject(dc, bitmap);
        try
        {
            Equal(true, BitBlt(dc, 0, 0, width, height, source, (int)origin.X, (int)origin.Y, 0x40CC0020));
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            System.IO.Directory.CreateDirectory("artifacts"); using var stream = System.IO.File.Create("artifacts/" + name + ".png"); encoder.Save(stream);
        }
        finally { SelectObject(dc, previous); DeleteObject(bitmap); DeleteDC(dc); ReleaseDC(0, source); }
    }
    private static uint OwnedPixel(Window window, Point local)
    {
        var point = window.PointToScreen(local);
        var p = new Native.Point { X = (int)point.X, Y = (int)point.Y };
        var handle = new WindowInteropHelper(window).Handle;
        Equal(handle, WindowFromPoint(p));
        var dc = GetDC(0);
        try { uint pixel = GetPixel(dc, p.X, p.Y); if (pixel == uint.MaxValue) throw new Exception("GetPixel failed."); return pixel; }
        finally { ReleaseDC(0, dc); }
    }
    private static void CheckPickerBounds(QuickSelectWindow picker, DisplayArea display)
    {
        var handle = new WindowInteropHelper(picker).Handle;
        Equal(QuickSelectWindow.InitialBounds(display.WorkArea, Elements<CheckBox>(picker).Count(), Native.GetDpiForWindow(handle) / 96d), Native.Bounds(handle));
    }
    private static Task CheckOverviewSize()
    {
        var large = new WindowRect(0, 40, 2560, 1400);
        var two = QuickSelectWindow.InitialBounds(large, 2, 1);
        var three = QuickSelectWindow.InitialBounds(large, 3, 1);
        var eight = QuickSelectWindow.InitialBounds(large, 8, 1);
        Equal(true, two.Width < three.Width && two.Height < eight.Height);
        Equal(true, two.Width < large.Width / 2 && two.Height < large.Height / 2);
        foreach (var work in new[] { large, new WindowRect(-1920, -200, 1920, 1040), new WindowRect(2560, 40, 1280, 720) })
            foreach (double scale in new[] { 1d, 1.25, 1.5, 2 })
                foreach (int count in new[] { 0, 1, 2, 3, 4, 8, 30, 500 })
                {
                    var bounds = QuickSelectWindow.InitialBounds(work, count, scale);
                    CheckGeometry(work, [bounds]);
                    Equal(true, Math.Abs((bounds.X - work.X) * 2 + bounds.Width - work.Width) <= 1);
                    Equal(true, Math.Abs((bounds.Y - work.Y) * 2 + bounds.Height - work.Height) <= 1);
                    Equal(true, bounds.Width < work.Width && bounds.Height < work.Height);
                }
        return Task.CompletedTask;
    }
    private static async Task CheckOverviewSizeUi()
    {
        await using var fixture = await Fixture.Start(8);
        using var manager = new WindowManager(); var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var targets = fixture.Handles.Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
        var original = targets.Select(manager.ReadState).ToArray();
        var panel = new MainWindow(manager, display, targets, enableShortcuts: false); panel.Show();
        QuickSelectWindow? picker = null;
        try
        {
            foreach (int count in new[] { 2, 3, 8 })
            {
                await fixture.Send("top 0"); var bounds = manager.ReadState(targets[0]).Bounds;
                await ClickNativeOwned(targets[0].Handle, new Native.Point { X = bounds.X + 100, Y = bounds.Y + 12 });
                await Until(() => GetForegroundWindow() == targets[0].Handle);
                picker = panel.OpenQuickSelection(targets[0].Handle, targets.Take(count).ToArray())!;
                QuickSelectionResult? completion = null;
                picker.Completed += result => completion = result;
                await Ready(picker);
                CheckPickerBounds(picker, display);
                var initial = Native.Bounds(new WindowInteropHelper(picker).Handle);
                Toggle(Candidate(picker, targets[1].Title));
                var search = Elements<TextBox>(picker).Single(); search.Text = "修订"; await Task.Delay(250);
                Equal(initial, Native.Bounds(new WindowInteropHelper(picker).Handle));
                Equal(true, ButtonNamed(picker, "确认本屏选窗").IsEnabled);
                search.Text = ""; await Task.Delay(250);
                if (!picker.IsVisible) throw new Exception("Sizing session closed before viewport verification: " + completion?.Message);
                var button = ButtonNamed(picker, "确认本屏选窗"); var point = button.TranslatePoint(new Point(), picker); var client = ClientSize(picker);
                Equal(true, point.X >= 0 && point.Y >= 0 && point.X + button.ActualWidth <= client.Width && point.Y + button.ActualHeight <= client.Height);
                ComposedScreenshot(picker, "paneset-overview-fit-" + count);
                Invoke(ButtonNamed(picker, "取消本屏选窗")); await Until(() => !picker.IsVisible);
                Equal(false, manager.HasUndo);
                for (int i = 0; i < targets.Length; i++) Equal(original[i], manager.ReadState(targets[i]));
            }
        }
        finally { picker?.Close(); panel.Exit(); }
    }

    private static async Task CheckOverview()
    {
        await using var fixture = await Fixture.Start(12);
        using var manager = new WindowManager(); var display = WindowManager.DisplayFor(fixture.Handles[0]);
        for (int i = 0; i < 12; i++) await fixture.Send($"position {i} {display.WorkArea.X + 100 + i % 4 * 100} {display.WorkArea.Y + 100 + i / 4 * 100}");
        var targets = fixture.Handles.Select(h => manager.ListWindows(display).Single(t => t.Handle == h)).ToArray();
        var original = targets.Select(manager.ReadState).ToArray();
        var panel = new MainWindow(manager, display, targets, enableShortcuts: false);
        panel.Show();
        QuickSelectWindow? picker = null;
        try
        {
            await fixture.Send("top 5");
            var bounds = manager.ReadState(targets[5]).Bounds;
            await ClickNativeOwned(targets[5].Handle, new Native.Point { X = bounds.X + 100, Y = bounds.Y + 12 });
            await Until(() => GetForegroundWindow() == targets[5].Handle);
            picker = panel.OpenQuickSelection(targets[5].Handle, targets)!; await Ready(picker);
            var cards = Elements<CheckBox>(picker).ToArray();
            Equal(targets[5].Handle, ((WindowRow)cards[0].DataContext).Target.Handle);
            Equal(12, cards.Length);
            var order = cards.Select(c => ((WindowRow)c.DataContext).Target.Handle).ToArray();
            Equal(string.Join(',', new[] { 5, 0, 1, 2, 3, 4, 6, 7, 8, 9, 10, 11 }.Select(i => targets[i].Handle)), string.Join(',', order));
            await ClickOwned(picker, Candidate(picker, targets[1].Title));
            await Until(() => Candidate(picker, targets[1].Title).IsChecked == true);
            Equal(targets[1].Title, manager.ListWindows(display).Single(t => t.Handle == targets[1].Handle).Title);
            Toggle(Candidate(picker, targets[2].Title));
            var move = ButtonNamed(picker, "前移 · " + targets[2].Title); move.Focus();
            await Until(() => System.Windows.Input.Keyboard.FocusedElement == move && GetForegroundWindow() == new WindowInteropHelper(picker).Handle);
            await SendKeys(0x0D);
            await Until(() => ((WindowRow)Candidate(picker, targets[2].Title).DataContext).Order == 2);
            Invoke(ButtonNamed(picker, "前移 · " + targets[2].Title));
            await Until(() => ((WindowRow)Candidate(picker, targets[2].Title).DataContext).Order == 1);
            Equal(1, ((WindowRow)Candidate(picker, targets[2].Title).DataContext).Order);
            Equal(2, ((WindowRow)Candidate(picker, targets[5].Title).DataContext).Order);
            var search = Elements<TextBox>(picker).Single(); search.Text = "修订";
            Equal(1, Elements<CheckBox>(picker).Count(c => c.IsVisible));
            Equal(true, ButtonNamed(picker, "确认本屏选窗").IsEnabled);
            Equal(true, Elements<TextBlock>(picker).Any(t => t.Text == "1 · " + targets[2].Title));
            search.Focus(); await SendKeys(0x0D); Equal(true, picker.IsVisible); Equal(false, manager.HasUndo);
            search.Text = "";
            await fixture.Send("long");
            await Until(() => Elements<CheckBox>(picker).Any(c => ((WindowRow)c.DataContext).Target.Title.Length > 200));
            Equal(string.Join(',', order), string.Join(',', Elements<CheckBox>(picker).Select(c => ((WindowRow)c.DataContext).Target.Handle)));
            foreach (var size in new[] { (1440d, 900d), (1000d, 650d), (700d, 450d) })
            {
                picker.Width = size.Item1; picker.Height = size.Item2; await Task.Delay(250); picker.UpdateLayout();
                var button = ButtonNamed(picker, "确认本屏选窗"); var p = button.TranslatePoint(new Point(), picker);
                Equal(true, p.Y >= 0 && p.Y + button.ActualHeight <= ClientSize(picker).Height);
                Equal(true, p.X >= 0 && p.X + button.ActualWidth <= ClientSize(picker).Width);
                var selectedScroll = Elements<ScrollViewer>(picker).Single(s => s.Content is StackPanel);
                Equal(true, selectedScroll.ViewportHeight >= 70);
                var scroll = Elements<ScrollViewer>(picker).First(s => s.Content is WrapPanel); scroll.ScrollToBottom();
                await Until(() => scroll.VerticalOffset >= scroll.ScrollableHeight);
                ComposedScreenshot(picker, "paneset-overview-" + (int)size.Item2 + "-composed");
                Screenshot(picker, "paneset-overview-" + (int)size.Item2 + "-wpf-only");
            }
            await fixture.Send("min 2"); await Until(() => !ButtonNamed(picker, "确认本屏选窗").IsEnabled);
            Invoke(ButtonNamed(picker, "取消选择 · " + targets[2].Title));
            await Until(() => ButtonNamed(picker, "确认本屏选窗").IsEnabled);
            for (int i = 0; i < 12; i++) if (i != 2) Equal(original[i], manager.ReadState(targets[i]));
            Invoke(ButtonNamed(picker, "确认本屏选窗")); await Until(() => !picker.IsVisible && !manager.HasPending && manager.HasUndo);
            var areas = LayoutEngine.Arrange(display.WorkArea, 2, ArrangementKind.Automatic);
            Equal(areas[0], manager.ReadState(targets[5]).Bounds); Equal(areas[1], manager.ReadState(targets[1]).Bounds);
            Equal(true, (await manager.UndoAsync()).Success);
            Equal(original[5], manager.ReadState(targets[5])); Equal(original[1], manager.ReadState(targets[1]));
        }
        finally { picker?.Close(); panel.Exit(); }
    }

    private static async Task CheckThumbnail()
    {
        var source = new Window { Title = "PaneSet thumbnail fixture", Width = 400, Height = 300,
            WindowStyle = WindowStyle.None, Background = Brushes.Red, ShowInTaskbar = false };
        var destination = new Window { Title = "PaneSet thumbnail probe", Width = 700, Height = 500,
            WindowStyle = WindowStyle.None, Background = Brushes.White, Topmost = true, ShowInTaskbar = false };
        var area = WindowManager.ListDisplays()[0].WorkArea;
        source.Left = destination.Left = area.X + 100; source.Top = destination.Top = area.Y + 100;
        WindowThumbnail? thumbnail = null;
        try
        {
            source.Show(); destination.Show(); await Task.Delay(150);
            var sourceHandle = new WindowInteropHelper(source).Handle;
            var original = Native.Bounds(sourceHandle);
            thumbnail = new WindowThumbnail(new WindowInteropHelper(destination).Handle, sourceHandle);
            Equal(true, thumbnail.Available);
            var dpi = VisualTreeHelper.GetDpi(destination);
            Rect Physical(Rect r) => new(r.X * dpi.DpiScaleX, r.Y * dpi.DpiScaleY, r.Width * dpi.DpiScaleX, r.Height * dpi.DpiScaleY);
            Equal(true, thumbnail.Update(Physical(new Rect(40, 40, 400, 300)), Physical(new Rect(40, 40, 400, 300)), true));
            Native.DwmFlush(); await Task.Delay(150);
            Equal(0x0000FFu, OwnedPixel(destination, new Point(140, 140)));
            source.Background = Brushes.Lime; Native.DwmFlush(); await Task.Delay(150);
            Equal(0x00FF00u, OwnedPixel(destination, new Point(140, 140)));
            Equal(true, thumbnail.Update(Physical(new Rect(40, 40, 400, 300)), Physical(new Rect(40, 180, 400, 160)), true));
            Native.DwmFlush(); await Task.Delay(150);
            Equal(0xFFFFFFu, OwnedPixel(destination, new Point(140, 140)));
            Equal(0x00FF00u, OwnedPixel(destination, new Point(140, 240)));
            Equal(original, Native.Bounds(sourceHandle));
            thumbnail.Dispose(); Native.DwmFlush(); await Task.Delay(150);
            Equal(0xFFFFFFu, OwnedPixel(destination, new Point(140, 240)));
            using var unavailable = new WindowThumbnail(new WindowInteropHelper(destination).Handle, 0);
            Equal(false, unavailable.Available);
        }
        finally { thumbnail?.Dispose(); destination.Close(); source.Close(); }
    }
}
