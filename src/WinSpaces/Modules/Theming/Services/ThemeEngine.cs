using System;
using System.Windows;
using WinSpaces.Diagnostics;
using WinSpaces.Services;

namespace WinSpaces.Modules.Theming.Services;

/// <summary>
/// Service central gérant le chargement dynamique des thèmes WPF.
/// </summary>
public sealed class ThemeEngine : HealthCheckableBase
{
    private ResourceDictionary? _currentTheme;
    public override string ComponentName => "Theme Engine";
    public override bool IsHealthy => true;
    public override string StatusMessage => "Thème actif";

    public ThemeEngine()
    {
        SetMetric("theme_mode", "Unknown");
    }

    public void ApplyTheme(string themeName)
    {
        try
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Modules/Theming/Themes/{themeName}.xaml")
            };

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
            
            _currentTheme = dict;
            SetMetric("theme_mode", themeName);
            AppLog.Info($"ThemeEngine : Thème '{themeName}' appliqué");
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException($"Échec chargement thème {themeName}", ex));
        }
    }
}
