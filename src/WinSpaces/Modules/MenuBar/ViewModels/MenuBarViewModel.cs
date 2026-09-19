using System.Windows.Input;
using WinSpaces.Modules.MenuBar.Services;
using WinSpaces.ViewModels;

namespace WinSpaces.Modules.MenuBar.ViewModels;

/// <summary>
/// ViewModel pour la barre de menu système.
/// Gère l'affichage des indicateurs et les interactions utilisateur.
/// </summary>
public sealed class MenuBarViewModel : ViewModelBase
{
    private readonly MenuBarService _service;
    private readonly SystemMonitorService _monitor;

    // Propriétés observables pour le binding
    public int BatteryLevel => _monitor.BatteryLevel;
    public bool IsCharging => _monitor.IsCharging;
    public int VolumeLevel => _monitor.VolumeLevel;
    public bool IsMuted => _monitor.IsMuted;
    public bool IsWiFiConnected => _monitor.IsWiFiConnected;
    public string NetworkName => _monitor.NetworkName;
    public string CurrentTime => _monitor.CurrentTime.ToString("HH:mm");
    public string CurrentDate => _monitor.CurrentTime.ToString("ddd d MMM");

    // Icônes dynamiques
    public string BatteryIcon => GetBatteryIcon();
    public string VolumeIcon => IsMuted ? "🔇" : "🔊";
    public string WiFiIcon => IsWiFiConnected ? "📶" : "📵";

    // Commandes
    public ICommand OpenBatterySettingsCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand OpenNetworkSettingsCommand { get; }
    public ICommand OpenDateTimeSettingsCommand { get; }

    public MenuBarViewModel(MenuBarService service)
    {
        _service = service;
        _monitor = service.SystemMonitor;

        // S'abonner aux changements d'état système
        _monitor.SystemStateChanged += OnSystemStateChanged;

        // Initialiser les commandes
        OpenBatterySettingsCommand = new RelayCommand(OpenBatterySettings);
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        OpenNetworkSettingsCommand = new RelayCommand(OpenNetworkSettings);
        OpenDateTimeSettingsCommand = new RelayCommand(OpenDateTimeSettings);
    }

    private void OnSystemStateChanged(object? sender, EventArgs e)
    {
        // Notifier tous les changements de propriétés
        OnPropertyChanged(nameof(BatteryLevel));
        OnPropertyChanged(nameof(IsCharging));
        OnPropertyChanged(nameof(BatteryIcon));
        OnPropertyChanged(nameof(VolumeLevel));
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(VolumeIcon));
        OnPropertyChanged(nameof(IsWiFiConnected));
        OnPropertyChanged(nameof(NetworkName));
        OnPropertyChanged(nameof(WiFiIcon));
        OnPropertyChanged(nameof(CurrentTime));
        OnPropertyChanged(nameof(CurrentDate));
    }

    private string GetBatteryIcon()
    {
        if (IsCharging) return "🔌";

        return BatteryLevel switch
        {
            >= 90 => "🔋",
            >= 60 => "🔋",
            >= 30 => "🪫",
            >= 10 => "🪫",
            _ => "🪫"
        };
    }

    private void OpenBatterySettings()
    {
        try
        {
            System.Diagnostics.Process.Start("ms-settings:batterysaver");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }

    private void ToggleMute()
    {
        // TODO: Implémenter toggle mute via CoreAudio
        AppLog.Info("MenuBar : Toggle mute demandé");
    }

    private void OpenNetworkSettings()
    {
        try
        {
            System.Diagnostics.Process.Start("ms-settings:network");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }

    private void OpenDateTimeSettings()
    {
        try
        {
            System.Diagnostics.Process.Start("ms-settings:dateandtime");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }
}

// RelayCommand (réutilisable depuis Spotlight)
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}
