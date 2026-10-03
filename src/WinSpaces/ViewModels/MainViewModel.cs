using System.ComponentModel;
using System.Windows.Input;
using WinSpaces.Diagnostics;
using WinSpaces.Desktops;
using WinSpaces.Modules.Logs;
using WinSpaces.Services;

namespace WinSpaces.ViewModels;

/// <summary>
/// ViewModel du tableau de bord : navigation par barre latérale (style macOS),
/// aperçu, diagnostic et console des journaux.
/// </summary>
public sealed class MainViewModel : ViewModelBase, IDisposable
{
    // Clés de section de la barre latérale.
    public const string SectionOverview = "overview";
    public const string SectionDiagnostics = "diagnostics";
    public const string SectionLogs = "logs";
    public const string SectionErrors = "errors";
    public const string SectionWarnings = "warnings";
    public const string SectionCurrentFile = "file-current";
    public const string SectionArchiveFile = "file-archive";

    private readonly VirtualDesktopService _desktops;
    private string _selectedSection = SectionOverview;
    private bool _applyingSection;

    public MainViewModel(VirtualDesktopService desktops, HealthMonitor healthMonitor, AppOptions options)
    {
        _desktops = desktops;
        Options = options;
        Diagnostics = new DiagnosticsViewModel(healthMonitor);
        Logs = new LogViewerViewModel();
        Logs.PropertyChanged += OnLogsPropertyChanged;

        RefreshCommand = new DelegateCommand(Refresh);
        NavigateCommand = new DelegateCommand(p => { if (p is string key) SelectedSection = key; });
        OpenEntryCommand = new DelegateCommand(p =>
        {
            if (p is not LogEntry entry) return;
            SelectedSection = entry.Kind == LogLevelKind.Error ? SectionErrors : SectionLogs;
            Logs.SelectedEntry = entry;
        });
    }

    public DiagnosticsViewModel Diagnostics { get; }
    public LogViewerViewModel Logs { get; }
    public AppOptions Options { get; }

    public ICommand RefreshCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand OpenEntryCommand { get; }

    public string Version => $"Version {System.Windows.Forms.Application.ProductVersion.Split('+')[0]}";

    // --- Navigation ----------------------------------------------------------

    /// <summary>Élément sélectionné dans la barre latérale.</summary>
    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (!SetProperty(ref _selectedSection, value)) return;
            ApplySection(value);
        }
    }

    /// <summary>Page affichée : overview, diagnostics ou logs.</summary>
    public string CurrentPage => _selectedSection switch
    {
        SectionOverview => SectionOverview,
        SectionDiagnostics => SectionDiagnostics,
        _ => SectionLogs
    };

    private void ApplySection(string key)
    {
        _applyingSection = true;
        try
        {
            switch (key)
            {
                case SectionLogs:
                case SectionCurrentFile:
                    Logs.Source = LogSource.Current;
                    Logs.LevelFilter = LogLevelFilter.All;
                    break;
                case SectionErrors:
                    Logs.Source = LogSource.Current;
                    Logs.LevelFilter = LogLevelFilter.Error;
                    break;
                case SectionWarnings:
                    Logs.Source = LogSource.Current;
                    Logs.LevelFilter = LogLevelFilter.Warning;
                    break;
                case SectionArchiveFile:
                    Logs.Source = LogSource.Archive;
                    Logs.LevelFilter = LogLevelFilter.All;
                    break;
                case SectionDiagnostics:
                    Diagnostics.RefreshStatus();
                    break;
            }
        }
        finally
        {
            _applyingSection = false;
        }
        OnPropertyChanged(nameof(CurrentPage));
    }

    /// <summary>
    /// Garde la barre latérale cohérente quand on change de filtre ou de
    /// fichier depuis la barre d'outils du journal.
    /// </summary>
    private void OnLogsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_applyingSection || CurrentPage != SectionLogs) return;
        if (e.PropertyName is not (nameof(LogViewerViewModel.LevelFilter) or nameof(LogViewerViewModel.Source))) return;

        string key = Logs.IsArchive
            ? SectionArchiveFile
            : Logs.LevelFilter switch
            {
                LogLevelFilter.Error => SectionErrors,
                LogLevelFilter.Warning => SectionWarnings,
                LogLevelFilter.All => _selectedSection == SectionCurrentFile ? SectionCurrentFile : SectionLogs,
                _ => "logs-other" // aucun élément de la barre latérale ne correspond
            };

        if (key == _selectedSection) return;
        _selectedSection = key;
        OnPropertyChanged(nameof(SelectedSection));
    }

    // --- Aperçu ----------------------------------------------------------------

    public int CurrentDesktopNumber
    {
        get
        {
            int index = _desktops.CurrentDesktopIndex;
            return index < 0 ? 0 : index + 1;
        }
    }

    public int DesktopCount => _desktops.GetDesktopIds().Count;

    // Options : relais avec notification (AppOptions n'est pas observable).
    public bool FullscreenSpacesEnabled
    {
        get => Options.FullscreenSpacesEnabled;
        set { Options.FullscreenSpacesEnabled = value; OnPropertyChanged(); }
    }

    public bool GesturesEnabled
    {
        get => Options.GesturesEnabled;
        set { Options.GesturesEnabled = value; OnPropertyChanged(); }
    }

    /// <summary>Applique réellement le démarrage automatique (clé Run du registre).</summary>
    public bool AutostartEnabled
    {
        get => Options.AutostartEnabled;
        set
        {
            Options.AutostartEnabled = value;
            AutostartManager.SetEnabled(value);
            OnPropertyChanged();
        }
    }

    public void Refresh()
    {
        Diagnostics.RefreshStatus();
        RefreshDesktopInfo();
        OnPropertyChanged(nameof(FullscreenSpacesEnabled));
        OnPropertyChanged(nameof(GesturesEnabled));
        OnPropertyChanged(nameof(AutostartEnabled));
    }

    public void RefreshDesktopInfo()
    {
        OnPropertyChanged(nameof(CurrentDesktopNumber));
        OnPropertyChanged(nameof(DesktopCount));
    }

    public void Dispose()
    {
        Logs.PropertyChanged -= OnLogsPropertyChanged;
        Logs.Dispose();
    }
}
