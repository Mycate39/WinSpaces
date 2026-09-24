using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using WinSpaces.Diagnostics;
using WinSpaces.Services;

namespace WinSpaces.Modules.MenuBar.Services;

/// <summary>
/// Service de monitoring système en temps réel.
/// Surveille : Batterie, Volume, WiFi/Réseau, Heure.
/// </summary>
public sealed class SystemMonitorService : HealthCheckableBase, IDisposable
{
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public override string ComponentName => "System Monitor";
    public override bool IsHealthy => true;
    public override string StatusMessage => "Monitoring actif";

    // État système
    public int BatteryLevel { get; private set; }
    public bool IsCharging { get; private set; }
    public int VolumeLevel { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsWiFiConnected { get; private set; }
    public string NetworkName { get; private set; } = "Non connecté";
    public DateTime CurrentTime { get; private set; } = DateTime.Now;

    public event EventHandler? SystemStateChanged;

    public SystemMonitorService()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += OnTimerTick;

        SetMetric("update_interval_seconds", 2);
        SetMetric("updates_count", 0);
    }

    public void Start()
    {
        RefreshSystemState();
        _timer.Start();
        AppLog.Info("SystemMonitor : Démarrage monitoring");
    }

    public void Stop()
    {
        _timer.Stop();
        AppLog.Info("SystemMonitor : Arrêt monitoring");
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        RefreshSystemState();
        
        var count = (int)(Metrics.GetValueOrDefault("updates_count", 0));
        SetMetric("updates_count", count + 1);
    }

    private void RefreshSystemState()
    {
        try
        {
            // 1. Batterie
            RefreshBattery();

            // 2. Volume
            RefreshVolume();

            // 3. Réseau
            RefreshNetwork();

            // 4. Heure
            CurrentTime = DateTime.Now;

            SystemStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur rafraîchissement système", ex));
        }
    }

    private void RefreshBattery()
    {
        try
        {
            var status = System.Windows.Forms.SystemInformation.PowerStatus;
            BatteryLevel = (int)(status.BatteryLifePercent * 100);
            IsCharging = status.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;

            SetMetric("battery_level", BatteryLevel);
            SetMetric("is_charging", IsCharging);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            BatteryLevel = -1;
        }
    }

    private void RefreshVolume()
    {
        try
        {
            // Utilisation de l'API Windows CoreAudio (simplifié ici)
            // En production, utiliser NAudio ou CoreAudioAPI
            VolumeLevel = 50; // Placeholder
            IsMuted = false;

            SetMetric("volume_level", VolumeLevel);
            SetMetric("is_muted", IsMuted);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }

    private void RefreshNetwork()
    {
        try
        {
            var isConnected = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
            IsWiFiConnected = isConnected;

            if (isConnected)
            {
                // Récupérer le nom du réseau WiFi (simplifié)
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                var activeInterface = interfaces.FirstOrDefault(i => 
                    i.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                    i.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211);

                NetworkName = activeInterface?.Name ?? "Ethernet";
            }
            else
            {
                NetworkName = "Non connecté";
            }

            SetMetric("wifi_connected", IsWiFiConnected);
            SetMetric("network_name", NetworkName);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            IsWiFiConnected = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }
}
