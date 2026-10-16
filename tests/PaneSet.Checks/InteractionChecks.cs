using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Input;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Interop;
using PaneSet;

namespace PaneSet.Checks;

internal static partial class Program
{
    private sealed class UnavailableException(string message) : Exception(message);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct KeyInput
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Key;
        [FieldOffset(10)] public ushort Scan;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public uint Time;
        [FieldOffset(24)] public ulong Extra;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, KeyInput[] input, int size);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);

    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint key, nint data);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int show);
    private static async Task CheckSingleInstance()
    {
        if (Process.GetProcessesByName("PaneSet").Any()) throw new UnavailableException("An existing PaneSet belongs to the user.");
        string executable = Path.Combine(AppContext.BaseDirectory, "PaneSet.exe");
        if (!File.Exists(executable)) throw new Exception("Build PaneSet.Checks with its PaneSet project reference before this check.");
        static async Task<nint> Opened(Process process)
        {
            nint handle = 0;
            await Until(() => { if (process.HasExited) throw new Exception("PaneSet exited during startup."); process.Refresh(); return (handle = process.MainWindowHandle) != 0; });
            return handle;
        }
        static async Task ExitApp(Process process, nint handle)
        {
            ShowWindow(handle, 9);
            var root = AutomationElement.FromHandle(handle);
            var exit = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "退出"));
            if (exit == null) throw new Exception("Explicit exit button was not found.");
            ((InvokePattern)exit.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); Equal(0, process.ExitCode);
        }
        using var first = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false })!;
        try
        {
            nint handle = await Opened(first);
            PostMessage(handle, 0x10, 0, 0); await Until(() => IsIconic(handle));
            using var second = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false })!;
            try { await second.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); Equal(0, second.ExitCode); }
            finally { if (!second.HasExited) second.Kill(); }
            await Until(() => !IsIconic(handle)); Equal(false, first.HasExited);
            Equal(1, Process.GetProcessesByName("PaneSet").Length);
            await ExitApp(first, handle);
            using var restarted = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false })!;
            try { await ExitApp(restarted, await Opened(restarted)); }
            finally { if (!restarted.HasExited) restarted.Kill(); }
        }
        finally { if (!first.HasExited) first.Kill(); }
    }

    private static void CheckGeometry(WindowRect area, WindowRect[] rectangles)
    {
        for (int i = 0; i < rectangles.Length; i++)
        {
            var a = rectangles[i];
            if (a.Width <= 0 || a.Height <= 0 || a.X < area.X || a.Y < area.Y
                || (long)a.X + a.Width > (long)area.X + area.Width || (long)a.Y + a.Height > (long)area.Y + area.Height)
                throw new Exception("Layout escaped work area: " + a);
            foreach (var b in rectangles.Take(i))
                if (a.X < (long)b.X + b.Width && (long)a.X + a.Width > b.X && a.Y < (long)b.Y + b.Height && (long)a.Y + a.Height > b.Y)
                    throw new Exception("Layout cells overlap.");
        }
    }

    private static async Task CheckMonitors()
    {
        var displays = WindowManager.ListDisplays();
        if (displays.Count < 2) throw new UnavailableException("Needs two connected displays.");
        foreach (var display in displays) Console.WriteLine("MONITOR " + display.Name + " " + display.WorkArea);
        foreach (bool reverse in new[] { false, true })
            foreach (bool maximized in new[] { false, true })
            {
                await using var fixture = await Fixture.Start();
                var source = displays[reverse ? 1 : 0]; var destination = displays[reverse ? 0 : 1];
                await fixture.Send($"position 0 {source.WorkArea.X + 120} {source.WorkArea.Y + 100}");
                await fixture.Send($"position 1 {destination.WorkArea.X + 180} {destination.WorkArea.Y + 120}");
                if (maximized) await fixture.Send("max 0");
                Console.WriteLine($"TRAVEL {source.Name} -> {destination.Name}, maximized={maximized}, DPI={GetDpiForWindow(fixture.Handles[0])}/{GetDpiForWindow(fixture.Handles[1])}");
                using var manager = new WindowManager();
                var targets = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
                Equal(source.Handle, WindowManager.DisplayFor(targets[0].Handle).Handle);
                Equal(destination.Handle, WindowManager.DisplayFor(targets[1].Handle).Handle);
                var originals = targets.Select(manager.ReadState).ToArray();
                var result = await manager.ApplyAsync(targets.Take(2).ToArray(), destination);
                if (result.Pending) result = await manager.CompletePendingAsync();
                Equal(true, result.Success); Equal(2, result.Windows.Count);
                var layout = LayoutEngine.TwoColumns(destination.WorkArea);
                for (int i = 0; i < 2; i++) Equal(layout[i], manager.ReadState(targets[i]).Bounds);
                Equal(originals[2], manager.ReadState(targets[2]));
                Equal(true, (await manager.UndoAsync()).Success);
                for (int i = 0; i < 3; i++) Equal(originals[i], manager.ReadState(targets[i]));
                Equal(source.Handle, WindowManager.DisplayFor(targets[0].Handle).Handle);
            }
        await using var secondary = await Fixture.Start();
        for (int i = 0; i < 3; i++) await secondary.Send($"position {i} {displays[1].WorkArea.X + 120 + i * 90} {displays[1].WorkArea.Y + 90 + i * 40}");
        using var secondaryManager = new WindowManager();
        var secondaryTargets = secondaryManager.ListWindows(displays[1]).Where(t => secondary.Handles.Contains(t.Handle)).ToArray();
        Equal(3, secondaryTargets.Length);
        var secondaryOriginals = secondaryTargets.Select(secondaryManager.ReadState).ToArray();
        Equal(true, (await secondaryManager.ApplyAsync(secondaryTargets, displays[1])).Success);
        Equal(true, (await secondaryManager.UndoAsync()).Success);
        for (int i = 0; i < 3; i++) Equal(secondaryOriginals[i], secondaryManager.ReadState(secondaryTargets[i]));
    }

    private static void Toggle(CheckBox box) => ((IToggleProvider)new CheckBoxAutomationPeer(box).GetPattern(PatternInterface.Toggle)).Toggle();
    private static Task Chord(Window window, byte key) => Chord(KeyboardTarget(window), key);

    private static Task Chord(Fixture fixture, nint handle, byte key)
    {
        RequireFixtureTarget(fixture, handle);
        return Chord(handle, key);
    }

    private static Task Chord(nint handle, byte key)
    {
        if (IntPtr.Size != 8) throw new UnavailableException("Keyboard injection check requires x64.");
        RequireForegroundInput(handle);
        KeyInput[] keys = [new() { Type = 1, Key = 0x11 }, new() { Type = 1, Key = 0x12 }, new() { Type = 1, Key = key },
            new() { Type = 1, Key = key, Flags = 2 }, new() { Type = 1, Key = 0x12, Flags = 2 }, new() { Type = 1, Key = 0x11, Flags = 2 }];
        uint inserted = SendInput((uint)keys.Length, keys, Marshal.SizeOf<KeyInput>());
        int error = Marshal.GetLastWin32Error();
        if (inserted == 0 && error == 5) throw new UnavailableException("Windows denied keyboard injection (SendInput, error 5).");
        if (inserted != keys.Length)
        {
            SendInput(3, keys[3..], Marshal.SizeOf<KeyInput>());
            throw new Exception($"Keyboard injection inserted {inserted}/{keys.Length}, error={error}.");
        }
        return Task.CompletedTask;
    }

    private static void SavePanelInputSettings()
    {
        new ShortcutSettings { Shortcuts = new() {
            [ShortcutAction.OpenPanel] = new(3, 0x20), [ShortcutAction.Restore] = new(3, 0x5A),
            [ShortcutAction.ArrangeScreen] = null, [ShortcutAction.SelectScreen] = null } }.Save(TestSettingsPath);
    }

    private static async Task CheckShortcuts()
    {
        await using var fixture = await Fixture.Start();
        var screens = WindowManager.ListDisplays();
        if (screens.Count > 1) await fixture.Send($"position 0 {screens[1].WorkArea.X + 120} {screens[1].WorkArea.Y + 100}");
        using var manager = new WindowManager();
        var candidates = manager.ListWindows().Where(t => fixture.Handles.Contains(t.Handle)).ToArray();
        var originals = candidates.ToDictionary(t => t.Handle, manager.ReadState);
        SavePanelInputSettings();
        var window = new MainWindow(manager, WindowManager.DisplayFor(fixture.Handles[0]), candidates, settingsPath: TestSettingsPath);
        window.Show(); await Task.Delay(120);
        try
        {
            var hint = (TextBlock)window.FindName("ShortcutText");
            if (hint.Text.Contains("不可用")) throw new UnavailableException("Global shortcut already owned: " + hint.Text);
            window.WindowState = System.Windows.WindowState.Minimized;
            await Until(() => IsIconic(new WindowInteropHelper(window).Handle));
            SetForegroundWindow(fixture.Handles[0]); await Until(() => GetForegroundWindow() == fixture.Handles[0]);
            await Chord(fixture, fixture.Handles[0], 0x20);
            await Until(() => window.WindowState == System.Windows.WindowState.Normal && GetForegroundWindow() == new WindowInteropHelper(window).Handle);
            SetForegroundWindow(fixture.Handles[0]); await Until(() => GetForegroundWindow() == fixture.Handles[0]);
            await Chord(fixture, fixture.Handles[0], 0x20);
            await Until(() => window.WindowState == System.Windows.WindowState.Normal && GetForegroundWindow() == new WindowInteropHelper(window).Handle);
            Equal(WindowManager.DisplayFor(fixture.Handles[0]).Handle, ((DisplayArea)((ComboBox)window.FindName("DisplayPicker")).SelectedItem).Handle);
            Equal(WindowManager.DisplayFor(fixture.Handles[0]).Handle, WindowManager.DisplayFor(new WindowInteropHelper(window).Handle).Handle);
            int rendered = Elements<CheckBox>(window).Count(b => b.IsChecked == true);
            if (rendered != 1) Console.WriteLine($"SHORTCUT selection model={((ItemsControl)window.FindName("WindowList")).Items.Cast<WindowRow>().Count(r => r.Selected)}, rendered={rendered}");
            await Until(() => Elements<CheckBox>(window).Count(b => b.IsChecked == true) == 1);
            var chosen = Elements<CheckBox>(window).Where(b => b.IsChecked == true).ToArray();
            Equal(1, chosen.Length); Equal("采购合同 · 初稿", AutomationProperties.GetName(chosen[0]));
            Toggle(Elements<CheckBox>(window).Single(b => AutomationProperties.GetName(b) == "采购合同 · 修订稿"));
            await fixture.Send("stall");
            Invoke(ButtonNamed(window, "排布所选窗口"));
            await Until(() => manager.HasPending);
            await Chord(window, 0x5A); await Task.Delay(80);
            Equal(true, manager.HasPending);
            Equal(false, ((Button)window.FindName("UndoButton")).IsEnabled);
            await Until(() => window.WindowState == System.Windows.WindowState.Minimized && !manager.HasPending);
            SetForegroundWindow(fixture.Handles[0]); await Until(() => GetForegroundWindow() == fixture.Handles[0]);
            await Chord(fixture, fixture.Handles[0], 0x5A); await Until(() => !manager.HasUndo && !manager.HasPending);
            foreach (var target in candidates) Equal(originals[target.Handle], manager.ReadState(target));
            // WM_CLOSE and Esc keep the shortcut owner alive until explicit exit.
            window.WindowState = System.Windows.WindowState.Normal; window.Close();
            Equal(System.Windows.WindowState.Minimized, window.WindowState);
        }
        finally { window.Exit(); Directory.Delete(Path.GetDirectoryName(TestSettingsPath)!, true); }
    }

    private static async Task CheckShortcutRouting()
    {
        await using var fixture = await Fixture.Start();
        var displays = WindowManager.ListDisplays();
        var display = displays.Last();
        await fixture.Send($"position 0 {display.WorkArea.X + 100} {display.WorkArea.Y + 100}");
        using var manager = new WindowManager();
        var candidates = manager.ListWindows().Where(t => fixture.Handles.Contains(t.Handle)).ToArray();
        var originals = candidates.ToDictionary(t => t.Handle, manager.ReadState);
        SavePanelInputSettings();
        var window = new MainWindow(manager, display, candidates, settingsPath: TestSettingsPath);
        window.Show(); await Task.Delay(120);
        try
        {
            if (((TextBlock)window.FindName("ShortcutText")).Text.Contains("不可用")) throw new UnavailableException("Global shortcut already owned.");
            var handle = new WindowInteropHelper(window).Handle;
            window.WindowState = System.Windows.WindowState.Minimized;
            // Synthetic message validates the registered hook's routing, independently of keyboard injection.
            PostMessage(handle, 0x312, 1, 0);
            await Until(() => !IsIconic(handle));
            window.OpenForForeground(fixture.Handles[0]);
            Equal(display.Handle, ((DisplayArea)((ComboBox)window.FindName("DisplayPicker")).SelectedItem).Handle);
            Equal(display.Handle, WindowManager.DisplayFor(handle).Handle);
            await Until(() => Elements<CheckBox>(window).Count(b => b.IsChecked == true) == 1);
            Equal(1, Elements<CheckBox>(window).Count(b => b.IsChecked == true));
            window.OpenForForeground(handle, toggle: false);
            Equal(false, IsIconic(handle));
            Equal(1, Elements<CheckBox>(window).Count(b => b.IsChecked == true));
            window.OpenForForeground(handle); Equal(true, IsIconic(handle));
            window.OpenForForeground(fixture.Handles[0]);
            await Until(() => Elements<CheckBox>(window).Count(b => b.IsChecked == true) == 1);
            Toggle(Elements<CheckBox>(window).Single(b => AutomationProperties.GetName(b) == "采购合同 · 修订稿"));
            await fixture.Send("stall"); Invoke(ButtonNamed(window, "排布所选窗口"));
            await Until(() => manager.HasPending);
            PostMessage(handle, 0x312, 2, 0); await Task.Delay(80);
            Equal(true, manager.HasPending); Equal(false, ((Button)window.FindName("UndoButton")).IsEnabled);
            await Until(() => !manager.HasPending && IsIconic(handle));
            PostMessage(handle, 0x312, 2, 0); await Until(() => !manager.HasUndo && !manager.HasPending);
            foreach (var target in candidates) Equal(originals[target.Handle], manager.ReadState(target));
            window.WindowState = System.Windows.WindowState.Normal; window.Close(); Equal(true, IsIconic(handle));
        }
        finally { window.Exit(); Directory.Delete(Path.GetDirectoryName(TestSettingsPath)!, true); }
        var owner = new Window { Width = 300, Height = 200 }; owner.Show();
        var handleOwner = new WindowInteropHelper(owner).Handle;
        try
        {
            SavePanelInputSettings();
            Equal(true, RegisterHotKey(handleOwner, 10, 0x4003, 0x20));
            using var conflictManager = new WindowManager();
            var conflict = new MainWindow(conflictManager, display, candidates, settingsPath: TestSettingsPath);
            conflict.Show(); await Task.Delay(120);
            try
            {
                var hint = ((TextBlock)conflict.FindName("ShortcutText")).Text;
                Equal(true, hint.Contains("Ctrl+Alt+Space 打开 / 收起不可用（冲突）"));
                Equal(false, hint.Contains("Ctrl+Alt+Z 恢复不可用"));
                Equal(true, ((Button)conflict.FindName("RefreshButton")).IsEnabled);
            }
            finally { conflict.Exit(); }
            Equal(true, RegisterHotKey(handleOwner, 11, 0x4003, 0x5A));
        }
        finally { UnregisterHotKey(handleOwner, 10); UnregisterHotKey(handleOwner, 11); owner.Close(); Directory.Delete(Path.GetDirectoryName(TestSettingsPath)!, true); }
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetKeyboardState(byte[] state);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetKeyboardState(byte[] state);

    // Routed events and thread-local key state exercise WPF handlers, not system input delivery.
    private static bool RoutedKey(Window window, UIElement target, Key key, ModifierKeys modifiers, bool system = false)
    {
        var original = new byte[256];
        if (!GetKeyboardState(original)) throw new System.ComponentModel.Win32Exception();
        try
        {
            var state = new byte[256];
            foreach (var (modifier, generic, left) in new[] { (ModifierKeys.Alt, 0x12, 0xA4), (ModifierKeys.Control, 0x11, 0xA2), (ModifierKeys.Shift, 0x10, 0xA0), (ModifierKeys.Windows, 0x5B, 0x5B) })
                if ((modifiers & modifier) != 0) { state[generic] = 0x80; state[left] = 0x80; }
            if (!SetKeyboardState(state)) throw new System.ComponentModel.Win32Exception();
            Equal(modifiers, Keyboard.Modifiers);
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, Environment.TickCount, key)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            if (system) typeof(KeyEventArgs).GetMethod("MarkSystem", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(args, null);
            target.RaiseEvent(args);
            return args.Handled;
        }
        finally { if (!SetKeyboardState(original)) throw new System.ComponentModel.Win32Exception(); }
    }

    private static void CheckVisibleControls(MainWindow window)
    {
        window.UpdateLayout();
        var client = ClientSize(window);
        var preview = (FrameworkElement)window.FindName("PreviewView");
        var body = (FrameworkElement)window.FindName("BodyGrid");
        if (preview.IsVisible && preview.TranslatePoint(new(0, preview.ActualHeight), body).Y > body.ActualHeight)
            throw new Exception($"Preview escaped body: size={window.Width}x{window.Height}, expanded={((Expander)window.FindName("PreviewExpander")).IsExpanded}, bottom={preview.TranslatePoint(new(0, preview.ActualHeight), body).Y}, body={body.ActualHeight}");
        foreach (var control in Elements<FrameworkElement>(window).Where(e => e is Button or ComboBox or TextBox)
            .Where(e => !string.IsNullOrEmpty(AutomationProperties.GetName(e))))
        {
            var point = control.TranslatePoint(new(0, 0), window);
            if (control.ActualWidth <= 0 || control.ActualHeight <= 0 || point.X < 0 || point.Y < 0
                || point.X + control.ActualWidth > client.Width || point.Y + control.ActualHeight > client.Height)
                throw new Exception("Control clipped: " + AutomationProperties.GetName(control));
        }
    }

    private static async Task CheckLayoutKeys()
    {
        await using var fixture = await Fixture.Start(8);
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var originals = candidates.ToDictionary(t => t.Handle, manager.ReadState);
        var window = new MainWindow(manager, display, candidates, enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            var search = (TextBox)window.FindName("SearchBox");
            var picker = (ComboBox)window.FindName("LayoutPicker");
            var preview = (Expander)window.FindName("PreviewExpander");
            var scroll = (ScrollViewer)window.FindName("WindowScroll");
            int visible = Elements<CheckBox>(window).Count(box => box.TranslatePoint(new(0, 0), scroll).Y >= 0
                && box.TranslatePoint(new(0, 0), scroll).Y + box.ActualHeight <= scroll.ViewportHeight);
            Console.WriteLine($"PICKER visible={visible}, viewport={scroll.ViewportHeight:0}, row={Elements<CheckBox>(window).First().ActualHeight:0}");
            if (visible < 6) throw new Exception("Normal picker shows fewer than six full rows.");
            foreach (var size in new[] { (820d, 730d), (600d, 730d), (820d, 600d), (600d, 600d), (759d, 600d), (760d, 600d) })
            {
                window.Width = size.Item1; window.Height = size.Item2; await Task.Delay(60);
                CheckVisibleControls(window); Equal(size.Item1 >= 760, preview.IsExpanded);
                if (size.Item1 == 759 || size.Item1 == 760) Screenshot(window, "paneset-breakpoint-" + size.Item1);
            }
            window.Width = 820; window.Height = 730; await Task.Delay(60);
            foreach (var box in Elements<CheckBox>(window).Take(3)) Toggle(box);
            foreach (var (key, kind) in new[] { (Key.D1, ArrangementKind.Columns), (Key.D2, ArrangementKind.MainAndOthers), (Key.D3, ArrangementKind.Grid) })
            {
                Equal(true, RoutedKey(window, search, key, ModifierKeys.Alt, system: true));
                Equal(kind, (ArrangementKind)((ComboBoxItem)picker.SelectedItem).Tag);
                foreach (var target in candidates) Equal(originals[target.Handle], manager.ReadState(target));
                Invoke(ButtonNamed(window, "排布所选窗口")); await Until(() => window.WindowState == System.Windows.WindowState.Minimized && !manager.HasPending);
                var expected = LayoutEngine.Arrange(display.WorkArea, 3, kind);
                for (int i = 0; i < 3; i++) Equal(expected[i], manager.ReadState(candidates[i]).Bounds);
                for (int i = 3; i < candidates.Length; i++) Equal(originals[candidates[i].Handle], manager.ReadState(candidates[i]));
                window.WindowState = System.Windows.WindowState.Normal;
                Invoke(ButtonNamed(window, "恢复上次排布")); await Until(() => !manager.HasUndo && !manager.HasPending);
            }
            search.Text = "合同123中文";
            foreach (var mods in new[] { ModifierKeys.None, ModifierKeys.Control, ModifierKeys.Control | ModifierKeys.Alt, ModifierKeys.Alt | ModifierKeys.Shift })
            {
                Equal(false, RoutedKey(window, search, Key.D1, mods));
                Equal(ArrangementKind.Grid, (ArrangementKind)((ComboBoxItem)picker.SelectedItem).Tag);
                Equal("合同123中文", search.Text);
            }
            search.Text = "";
            Equal(true, RoutedKey(window, picker, Key.D2, ModifierKeys.Alt, system: true));
            Equal(ArrangementKind.MainAndOthers, (ArrangementKind)((ComboBoxItem)picker.SelectedItem).Tag);
            await fixture.Send("stall"); Invoke(ButtonNamed(window, "排布所选窗口"));
            await Until(() => manager.HasPending);
            Equal(false, RoutedKey(window, search, Key.D1, ModifierKeys.Alt, system: true));
            Equal(ArrangementKind.MainAndOthers, (ArrangementKind)((ComboBoxItem)picker.SelectedItem).Tag);
            await Until(() => !manager.HasPending && window.WindowState == System.Windows.WindowState.Minimized);
            window.WindowState = System.Windows.WindowState.Normal;
            Invoke(ButtonNamed(window, "恢复上次排布")); await Until(() => !manager.HasUndo && !manager.HasPending);
            foreach (var target in candidates) Equal(originals[target.Handle], manager.ReadState(target));
        }
        finally { window.Exit(); }
    }

    private static async Task SendKeys(nint handle, params byte[] down)
    {
        if (IntPtr.Size != 8) throw new UnavailableException("Local key injection check requires x64.");
        RequireForegroundInput(handle);
        KeyInput[] input = down.Select(key => new KeyInput { Type = 1, Key = key })
            .Concat(down.Reverse().Select(key => new KeyInput { Type = 1, Key = key, Flags = 2 })).ToArray();
        uint inserted = SendInput((uint)input.Length, input, Marshal.SizeOf<KeyInput>());
        int error = Marshal.GetLastWin32Error();
        Console.WriteLine($"KEY INPUT requested={input.Length}, inserted={inserted}, error={(inserted == input.Length ? 0 : error)}");
        if (inserted == 0 && error == 5) throw new UnavailableException("Windows denied local key injection (SendInput, error 5).");
        if (inserted != input.Length)
        {
            var releases = down.Reverse().Select(key => new KeyInput { Type = 1, Key = key, Flags = 2 }).ToArray();
            SendInput((uint)releases.Length, releases, Marshal.SizeOf<KeyInput>());
            throw new Exception($"Local key injection inserted {inserted}/{input.Length}, error={error}.");
        }
        await Task.Delay(60);
    }

    private static async Task CheckLocalLayoutInput()
    {
        await using var fixture = await Fixture.Start(4);
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var originals = candidates.ToDictionary(t => t.Handle, manager.ReadState);
        var window = new MainWindow(manager, display, candidates, enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            foreach (var box in Elements<CheckBox>(window).Take(3)) Toggle(box);
            var search = (TextBox)window.FindName("SearchBox");
            var picker = (ComboBox)window.FindName("LayoutPicker");
            foreach (var (key, kind) in new[] { ((byte)0x31, ArrangementKind.Columns), ((byte)0x32, ArrangementKind.MainAndOthers), ((byte)0x33, ArrangementKind.Grid) })
            {
                window.WindowState = System.Windows.WindowState.Normal; window.Activate();
                SetForegroundWindow(new WindowInteropHelper(window).Handle);
                await Until(() => GetForegroundWindow() == new WindowInteropHelper(window).Handle);
                UIElement inputTarget = kind == ArrangementKind.MainAndOthers ? picker : search;
                inputTarget.Focus(); await Until(() => Keyboard.FocusedElement == inputTarget);
                await SendLocalLayoutKeys(window, inputTarget, 0x12, key);
                await Until(() => Equals(((ComboBoxItem)picker.SelectedItem).Tag, kind));
                foreach (var target in candidates) Equal(originals[target.Handle], manager.ReadState(target));
                await SendLocalLayoutKeys(window, inputTarget, 0x0D);
                await Until(() => window.WindowState == System.Windows.WindowState.Minimized && !manager.HasPending);
                var expected = LayoutEngine.Arrange(display.WorkArea, 3, kind);
                for (int i = 0; i < 3; i++) Equal(expected[i], manager.ReadState(candidates[i]).Bounds);
                Equal(originals[candidates[3].Handle], manager.ReadState(candidates[3]));
                window.WindowState = System.Windows.WindowState.Normal;
                Invoke(ButtonNamed(window, "恢复上次排布")); await Until(() => !manager.HasUndo && !manager.HasPending);
            }
            window.Activate(); SetForegroundWindow(new WindowInteropHelper(window).Handle);
            await Until(() => GetForegroundWindow() == new WindowInteropHelper(window).Handle);
            search.Focus(); await Until(() => Keyboard.FocusedElement == search);
            await SendLocalLayoutKeys(window, search, 0x31); await Until(() => search.Text == "1");
            Equal(ArrangementKind.Grid, (ArrangementKind)((ComboBoxItem)picker.SelectedItem).Tag);
            foreach (var target in candidates) Equal(originals[target.Handle], manager.ReadState(target));
        }
        catch (Exception e) when (e is not UnavailableException)
        {
            throw new Exception($"Local layout input failed: {WindowEvidence(window)}; search={((TextBox)window.FindName("SearchBox")).Text}", e);
        }
        finally { window.Exit(); }
    }

    private static async Task CheckSelectionRefresh()
    {
        await using var fixture = await Fixture.Start();
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = manager.ListWindows().Where(t => fixture.Handles.Contains(t.Handle)).ToArray();
        var window = new MainWindow(manager, display, candidates, enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            window.OpenForForeground(fixture.Handles[0]);
            var items = (ItemsControl)window.FindName("WindowList");
            Equal(1, items.Items.Cast<WindowRow>().Count(r => r.Selected));
            Console.WriteLine($"SELECTION model=1, immediate-rendered={Elements<CheckBox>(window).Count(b => b.IsChecked == true)}");
            await Until(() => Elements<CheckBox>(window).Count(b => b.IsChecked == true) == 1);
            Equal("采购合同 · 初稿", AutomationProperties.GetName(Elements<CheckBox>(window).Single(b => b.IsChecked == true)));
            Toggle(Elements<CheckBox>(window).Single(b => AutomationProperties.GetName(b) == "采购合同 · 修订稿"));
            window.OpenForForeground(new WindowInteropHelper(window).Handle, toggle: false);
            Equal(System.Windows.WindowState.Normal, window.WindowState);
            Equal(2, items.Items.Cast<WindowRow>().Count(r => r.Selected));
            Invoke(ButtonNamed(window, "刷新窗口"));
            await Until(() => Elements<CheckBox>(window).Count(b => b.IsChecked == true) == 2);
            Equal(2, items.Items.Cast<WindowRow>().Count(r => r.Selected));
            Equal(false, manager.HasUndo);
        }
        finally { window.Exit(); }
    }

    private static async Task CheckManyUi()
    {
        await using var fixture = await Fixture.Start(8);
        using var manager = new WindowManager();
        var display = WindowManager.DisplayFor(fixture.Handles[0]);
        var candidates = fixture.Handles.Select(h => manager.ListWindows().Single(t => t.Handle == h)).ToArray();
        var originals = candidates.ToDictionary(t => t.Handle, manager.ReadState);
        var window = new MainWindow(manager, display, candidates, enableShortcuts: false);
        window.Show(); await Task.Delay(120);
        try
        {
            foreach (var box in Elements<CheckBox>(window).Take(3)) Toggle(box);
            var picker = (ComboBox)window.FindName("DisplayPicker");
            var layout = (ComboBox)window.FindName("LayoutPicker");
            layout.SelectedIndex = 2; Screenshot(window, "paneset-three-main");
            var labels = ((TextBlock)window.FindName("SelectionLabel")).Text;
            Invoke(ButtonNamed(window, "轮换顺序")); await Task.Delay(60);
            Equal(false, labels == ((TextBlock)window.FindName("SelectionLabel")).Text);
            var ordered = ((TextBlock)window.FindName("SelectionLabel")).Text;
            if (picker.Items.Count > 1) picker.SelectedIndex = (picker.SelectedIndex + 1) % picker.Items.Count;
            Equal(ordered, ((TextBlock)window.FindName("SelectionLabel")).Text);
            Invoke(ButtonNamed(window, "刷新窗口")); await Task.Delay(80);
            Equal(ordered, ((TextBlock)window.FindName("SelectionLabel")).Text);
            Equal(3, Elements<CheckBox>(window).Count(b => b.IsChecked == true));
            Invoke(ButtonNamed(window, "排布所选窗口")); await Until(() => window.WindowState == System.Windows.WindowState.Minimized);
            window.WindowState = System.Windows.WindowState.Normal;
            Invoke(ButtonNamed(window, "恢复上次排布"));
            await Until(() => !manager.HasUndo && !manager.HasPending && ((ItemsControl)window.FindName("WindowList")).IsEnabled);
            foreach (var box in Elements<CheckBox>(window).Where(b => b.IsChecked != true && fixture.Handles.Contains(((WindowRow)b.DataContext).Target.Handle))) Toggle(box);
            layout.SelectedIndex = 0;
            // Fixture.Start uses this host executable: apphost and `dotnet ...dll` have different process names.
            ((TextBox)window.FindName("SearchBox")).Text = Path.GetFileNameWithoutExtension(Environment.ProcessPath!); await Task.Delay(80);
            Equal(8, Elements<CheckBox>(window).Count(b => b.IsChecked == true));
            window.Width = 820; window.Height = 730; await Task.Delay(100); Screenshot(window, "paneset-grid");
            window.Width = 600; window.Height = 600; await Task.Delay(100); Screenshot(window, "paneset-grid-minimum");
            foreach (var button in Elements<Button>(window).Where(b => AutomationProperties.GetName(b) is "排布所选窗口" or "退出"))
            {
                var point = button.TranslatePoint(new(0, 0), window); var client = ClientSize(window);
                if (point.Y + button.ActualHeight > client.Height || point.X + button.ActualWidth > client.Width) throw new Exception("Multi-window action clipped.");
            }
            foreach (var target in candidates) Equal(originals[target.Handle], manager.ReadState(target));
            ((TextBox)window.FindName("SearchBox")).Text = "采购合同";
            var hint = (TextBlock)window.FindName("ShortcutText");
            hint.Text = "Ctrl+Alt+Space 打开 / 收起 · Ctrl+Alt+Z 恢复 · Enter 排布 · Alt+S 轮换";
            window.Width = 600; window.Height = 600; await Task.Delay(80); Screenshot(window, "paneset-shortcuts-minimum");
            foreach (var button in Elements<Button>(window).Where(b => AutomationProperties.GetName(b) is "排布所选窗口" or "退出"))
            {
                var point = button.TranslatePoint(new(0, 0), window); var client = ClientSize(window);
                if (point.Y + button.ActualHeight > client.Height || point.X + button.ActualWidth > client.Width) throw new Exception("Shortcut panel action clipped.");
            }
        }
        finally { window.Exit(); }
    }
}
