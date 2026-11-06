using System.Windows;
using Microsoft.Win32;
using NPUtil.Core.Settings;

namespace NPUtil.App.Theme;

public class ThemeManager
{
    private static AppTheme _currentTheme = AppTheme.System;

    public static event EventHandler<AppTheme>? ThemeChanged;

    public static AppTheme CurrentTheme => _currentTheme;

    public static void ApplyTheme(AppTheme theme)
    {
        _currentTheme = theme;
        var isDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsWindowsInDarkMode()
        };

        var dictUri = isDark
            ? new Uri("pack://application:,,,/Theme/DarkTheme.xaml", UriKind.Absolute)
            : new Uri("pack://application:,,,/Theme/LightTheme.xaml", UriKind.Absolute);

        var appDicts = Application.Current.Resources.MergedDictionaries;
        var existing = appDicts.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"));

        var newDict = new ResourceDictionary { Source = dictUri };
        if (existing != null)
        {
            var index = appDicts.IndexOf(existing);
            appDicts[index] = newDict;
        }
        else
        {
            appDicts.Add(newDict);
        }

        ThemeChanged?.Invoke(null, theme);
    }

    public static bool IsWindowsInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int lightThemeValue)
            {
                return lightThemeValue == 0;
            }
        }
        catch { /* ignore */ }

        return false;
    }
}
