using WinSpaces.Diagnostics;
using WinSpaces.Desktops;
using WinSpaces.Services;

namespace WinSpaces.ViewModels;

/// <summary>
/// ViewModel principal : coordonne les différents panneaux et expose l'état global.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly VirtualDesktopService _desktops;
    private readonly HealthMonitor _healthMonitor;

    public MainViewModel(VirtualDesktopService desktops, HealthMonitor healthMonitor, AppOptions options)
    {
        _desktops = desktops;
        _healthMonitor = healthMonitor;
        Options = options;
        Diagnostics = new DiagnosticsViewModel(healthMonitor);
    }

    public DiagnosticsViewModel Diagnostics { get; }

    public AppOptions Options { get; }

    public int CurrentDesktopIndex => _desktops.CurrentDesktopIndex;

    public int DesktopCount => _desktops.GetDesktopIds().Count;

    public void RefreshDesktopInfo()
    {
        OnPropertyChanged(nameof(CurrentDesktopIndex));
        OnPropertyChanged(nameof(DesktopCount));
    }
}
