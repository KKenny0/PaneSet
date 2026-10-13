using System.IO;
using System.Security;
using System.Windows;
using Microsoft.Win32;

namespace PaneSet;

internal static class ThemeSupport
{
    private const string Personalize = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static bool controlsLoaded;

    internal static void EnsureControls()
    {
        if (controlsLoaded || Application.Current == null) return;
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("pack://application:,,,/PaneSet;component/ThemeControls.xaml") });
        controlsLoaded = true;
    }

    internal static bool ResolveDark(AppearanceMode mode) => mode switch
    {
        AppearanceMode.Dark => true,
        AppearanceMode.Light => false,
        _ => WindowsAppDark()
    };

    private static bool WindowsAppDark()
    {
        try { return Registry.GetValue(Personalize, "AppsUseLightTheme", 1) is int value && value == 0; }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        { return false; }
    }

}
