using WinSpaces.Diagnostics;
using WinSpaces.Services;

namespace WinSpaces.Modules.MenuBar.Services;

/// <summary>
/// Service orchestrateur du module Menu Bar.
/// Gère le cycle de vie et coordonne les composants.
/// </summary>
public sealed class MenuBarService : HealthCheckableBase, IDisposable
{
    private readonly SystemMonitorService _systemMonitor;
    private bool _isEnabled;
    private bool _disposed;

    public override string ComponentName => "Menu Bar Service";
    public override bool IsHealthy => _systemMonitor.IsHealthy;
    public override string StatusMessage => _isEnabled 
        ? "Menu Bar actif" 
        : "Menu Bar désactivé";

    public SystemMonitorService SystemMonitor => _systemMonitor;
    public bool IsEnabled => _isEnabled;

    public MenuBarService()
    {
        _systemMonitor = new SystemMonitorService();
        _isEnabled = false;

        SetMetric("indicators_count", 4); // Battery, Volume, WiFi, Clock
        SetMetric("is_enabled", false);
    }

    /// <summary>
    /// Démarre le service Menu Bar.
    /// </summary>
    public void Start()
    {
        if (_isEnabled) return;

        _systemMonitor.Start();
        _isEnabled = true;

        SetMetric("is_enabled", true);
        SetMetric("start_time", DateTime.UtcNow);

        AppLog.Info("MenuBar : Service démarré");
    }

    /// <summary>
    /// Arrête le service Menu Bar.
    /// </summary>
    public void Stop()
    {
        if (!_isEnabled) return;

        _systemMonitor.Stop();
        _isEnabled = false;

        SetMetric("is_enabled", false);

        AppLog.Info("MenuBar : Service arrêté");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _systemMonitor.Dispose();
    }
}
