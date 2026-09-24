using System;
using Microsoft.Win32;
using WinSpaces.Diagnostics;
using WinSpaces.Services;

namespace WinSpaces.Modules.Theming.Services;

/// <summary>
/// Service d'arrière-plan surveillant les modifications du fond d'écran Windows.
/// </summary>
public sealed class WallpaperMonitor : HealthCheckableBase, IDisposable
{
    private bool _disposed;
    public override string ComponentName => "Wallpaper Monitor";
    public override bool IsHealthy => true;
    public override string StatusMessage => "Surveillance active";

    public WallpaperMonitor()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SetMetric("status", "Active");
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.Desktop)
        {
            AppLog.Info("WallpaperMonitor : Fond d'écran modifié");
            SetMetric("last_change", DateTime.UtcNow);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _disposed = true;
        }
    }
}
