using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace Sortexa.App.Services;

public interface IThemeService
{
    event Action<string>? ThemeChanged;
    string CurrentTheme { get; }
    void ApplyTheme(string themeName);
}

public sealed class ThemeService : IThemeService
{
    public event Action<string>? ThemeChanged;
    public string CurrentTheme { get; private set; } = "System";

    public void ApplyTheme(string themeName)
    {
        CurrentTheme = themeName;
        bool isDark = false;

        if (string.Equals(themeName, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            isDark = true;
        }
        else if (string.Equals(themeName, "Light", StringComparison.OrdinalIgnoreCase))
        {
            isDark = false;
        }
        else // "System"
        {
            isDark = DetectSystemIsDark();
        }

        string themeUri = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        UpdateMergedThemeDictionary(themeUri);
        ThemeChanged?.Invoke(themeName);
    }

    private static bool DetectSystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                var val = key.GetValue("AppsUseLightTheme");
                if (val is int intVal)
                {
                    return intVal == 0;
                }
            }
        }
        catch
        {
            // Default to dark theme for modern aesthetic
        }

        return true;
    }

    private static void UpdateMergedThemeDictionary(string themeRelativeUri)
    {
        if (Application.Current == null)
            return;

        var dicts = Application.Current.Resources.MergedDictionaries;
        var existingTheme = dicts.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"));

        var newDict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Sortexa;component/{themeRelativeUri}", UriKind.RelativeOrAbsolute)
        };

        if (existingTheme != null)
        {
            int index = dicts.IndexOf(existingTheme);
            dicts[index] = newDict;
        }
        else
        {
            dicts.Add(newDict);
        }
    }
}
