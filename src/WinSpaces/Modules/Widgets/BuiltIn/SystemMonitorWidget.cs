using System.Diagnostics;
using WinSpaces.Modules.Widgets.Core;

namespace WinSpaces.Modules.Widgets.BuiltIn;

/// <summary>
/// Widget de monitoring système affichant CPU et RAM en temps réel.
/// </summary>
public sealed class SystemMonitorWidget : WidgetBase
{
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _ramCounter;

    public override string Id => "system-monitor-" + Guid.NewGuid().ToString("N")[..8];
    public override string Name => "System Monitor";
    public override string Description => "Affiche l'utilisation CPU et RAM en temps réel";
    public override double Width => 200;
    public override double Height => 120;

    public float CpuUsage { get; private set; }
    public float RamUsagePercent { get; private set; }
    public long RamUsageMB { get; private set; }
    public long TotalRamMB { get; private set; }

    public SystemMonitorWidget() : base(TimeSpan.FromSeconds(2))
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur init PerformanceCounters", ex));
        }

        // Valeurs initiales
        TotalRamMB = GetTotalMemoryMB();
    }

    public override async Task RefreshAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                CpuUsage = _cpuCounter?.NextValue() ?? 0f;
                
                var availableRam = _ramCounter?.NextValue() ?? 0f;
                RamUsageMB = TotalRamMB - (long)availableRam;
                RamUsagePercent = TotalRamMB > 0 ? (RamUsageMB / (float)TotalRamMB) * 100f : 0f;

                Configuration["cpu_usage"] = CpuUsage;
                Configuration["ram_usage_percent"] = RamUsagePercent;
                Configuration["ram_usage_mb"] = RamUsageMB;
            }
            catch (Exception ex)
            {
                AppLog.Error(ex);
            }
        });
    }

    private long GetTotalMemoryMB()
    {
        try
        {
            var gcMemInfo = GC.GetGCMemoryInfo();
            return gcMemInfo.TotalAvailableMemoryBytes / (1024 * 1024);
        }
        catch
        {
            return 8192; // Fallback 8GB
        }
    }

    public override void Dispose()
    {
        _cpuCounter?.Dispose();
        _ramCounter?.Dispose();
        base.Dispose();
    }
}
