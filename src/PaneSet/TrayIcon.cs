using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PaneSet;

/// Notification-area presence: a small tray icon that survives Explorer restarts. Pure native
/// shell integration, no extra process or dependency; failure to create leaves the caller with
/// its previous taskbar-based entry.
internal sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = 0x8000 + 0x50;
    private readonly nint window;
    private readonly Action leftClick;
    private readonly Action rightClick;
    private readonly uint taskbarCreated;
    private readonly nint icon;
    private readonly HwndSource? source;
    private bool added;

    public bool Created => added;

    public TrayIcon(nint owner, Action leftClick, Action rightClick)
    {
        window = owner;
        this.leftClick = leftClick;
        this.rightClick = rightClick;
        taskbarCreated = Native.RegisterWindowMessageW("TaskbarCreated");
        icon = LoadModuleIcon();
        source = HwndSource.FromHwnd(owner);
        source?.AddHook(Receive);
        added = Add();
    }

    private static nint LoadModuleIcon()
    {
        nint module = Native.GetModuleHandleW(null);
        nint group = 0;
        // RT_GROUP_ICON = 14; the project icon embedded by the build is the first icon group.
        Native.EnumResourceNamesW(module, 14, (_, _, name, _) => { group = name; return false; }, 0);
        if (group != 0 && Native.LoadImageW(module, group, 1, 0, 0, 0x8000) != 0)
            return Native.LoadImageW(module, group, 1, 0, 0, 0x8000);
        // Fallback: the system application icon keeps the tray usable without an embedded resource.
        return Native.LoadImageW(0, 32512, 1, 0, 0, 0x8000);
    }

    private bool Add()
    {
        if (icon == 0) return false;
        var data = new Native.NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<Native.NotifyIconData>(),
            Window = window, Id = 1, Flags = 7, // NIF_MESSAGE | NIF_ICON | NIF_TIP
            Message = CallbackMessage, Icon = icon, Tip = "PaneSet"
        };
        return Native.Shell_NotifyIcon(0, ref data);
    }

    private nint Receive(nint handle, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == (int)CallbackMessage)
        {
            handled = true;
            if (lparam == 0x202) leftClick();
            else if (lparam == 0x205) rightClick();
        }
        else if (taskbarCreated != 0 && message == (int)taskbarCreated)
        {
            // Explorer restarted and the tray was recreated; register the icon again.
            handled = true;
            if (added) added = Add();
        }
        return 0;
    }

    public void Dispose()
    {
        if (added)
        {
            var data = new Native.NotifyIconData
            { Size = (uint)Marshal.SizeOf<Native.NotifyIconData>(), Window = window, Id = 1 };
            Native.Shell_NotifyIcon(2, ref data);
            added = false;
        }
        source?.RemoveHook(Receive);
    }
}
