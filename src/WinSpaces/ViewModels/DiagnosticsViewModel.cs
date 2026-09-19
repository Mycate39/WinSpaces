using System.Collections.ObjectModel;
using WinSpaces.Diagnostics;

namespace WinSpaces.ViewModels;

/// <summary>
/// ViewModel pour le panneau de diagnostic : expose l'état de tous les composants.
/// </summary>
public sealed class DiagnosticsViewModel : ViewModelBase
{
    private readonly HealthMonitor _healthMonitor;
    private string _globalStatus = string.Empty;

    public DiagnosticsViewModel(HealthMonitor healthMonitor)
    {
        _healthMonitor = healthMonitor;
        RefreshStatus();
    }

    public ObservableCollection<IHealthCheckable> Components { get; } = new();

    public string GlobalStatus
    {
        get => _globalStatus;
        private set => SetProperty(ref _globalStatus, value);
    }

    public bool AllHealthy => _healthMonitor.AllHealthy;

    public int HealthyCount => _healthMonitor.HealthyCount;

    public int TotalCount => _healthMonitor.TotalCount;

    public void RefreshStatus()
    {
        Components.Clear();
        foreach (var component in _healthMonitor.Components)
            Components.Add(component);

        GlobalStatus = _healthMonitor.GetGlobalStatus();
        OnPropertyChanged(nameof(AllHealthy));
        OnPropertyChanged(nameof(HealthyCount));
        OnPropertyChanged(nameof(TotalCount));
    }
}
