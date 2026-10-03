using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WinSpaces.Services;
using WinSpaces.ViewModels;

namespace WinSpaces.Modules.Logs;

/// <summary>Fichier affiché par la console de journaux.</summary>
public enum LogSource
{
    Current,
    Archive
}

/// <summary>Filtre de niveau (segmenté dans la barre d'outils).</summary>
public enum LogLevelFilter
{
    All,
    Error,
    Warning,
    Info,
    Debug
}

/// <summary>
/// Console des journaux façon « Console » de macOS : lecture du journal
/// WinSpaces (courant ou archive), filtres, recherche et suivi en direct.
/// Les entrées sont affichées de la plus récente à la plus ancienne.
/// </summary>
public sealed class LogViewerViewModel : ViewModelBase, IDisposable
{
    private const int MaxEntries = 10_000;
    private const int RecentErrorsCount = 5;

    private readonly DispatcherTimer _liveTimer;
    private ObservableCollection<LogEntry> _entries = new();
    private ICollectionView _entriesView;
    private LogFileTail _tail;
    private int _loadGeneration;
    private bool _hasLoaded;
    private bool _isActive;
    private bool _disposed;

    private LogLevelFilter _levelFilter = LogLevelFilter.All;
    private LogSource _source = LogSource.Current;
    private string _searchText = string.Empty;
    private LogEntry? _selectedEntry;
    private bool _isLive = true;
    private bool _isLoading;
    private int _errorCount;
    private int _warningCount;
    private int _visibleCount;
    private string _statusText = string.Empty;

    public LogViewerViewModel()
    {
        _tail = new LogFileTail(AppLog.FilePath);
        _entriesView = CreateView(_entries);

        _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveTimer.Tick += (_, _) => PollNewEntries();

        RefreshCommand = new DelegateCommand(Reload);
        CopySelectedCommand = new DelegateCommand(CopySelected);
        CopyVisibleCommand = new DelegateCommand(CopyVisible);
        OpenFolderCommand = new DelegateCommand(OpenFolder);
        OpenFileCommand = new DelegateCommand(OpenFile);
        ClearCommand = new DelegateCommand(ClearCurrentLog);
    }

    // --- Données ---------------------------------------------------------

    /// <summary>Vue filtrée des entrées (plus récentes en premier).</summary>
    public ICollectionView Entries => _entriesView;

    /// <summary>Dernières erreurs, pour l'aperçu.</summary>
    public ObservableCollection<LogEntry> RecentErrors { get; } = new();

    public int ErrorCount { get => _errorCount; private set => SetProperty(ref _errorCount, value); }
    public int WarningCount { get => _warningCount; private set => SetProperty(ref _warningCount, value); }
    public int TotalCount => _entries.Count;
    public int VisibleCount { get => _visibleCount; private set => SetProperty(ref _visibleCount, value); }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    // --- Filtres & sélection ---------------------------------------------

    public LogLevelFilter LevelFilter
    {
        get => _levelFilter;
        set
        {
            if (!SetProperty(ref _levelFilter, value)) return;
            OnPropertyChanged(nameof(Title));
            RefreshFilter();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value ?? string.Empty)) return;
            RefreshFilter();
        }
    }

    public LogSource Source
    {
        get => _source;
        set
        {
            if (!SetProperty(ref _source, value)) return;
            OnPropertyChanged(nameof(IsArchive));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(FileName));
            Reload();
        }
    }

    public bool IsArchive => _source == LogSource.Archive;

    public LogEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (!SetProperty(ref _selectedEntry, value)) return;
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => _selectedEntry is not null;

    /// <summary>Suivi en direct des nouvelles lignes (journal courant uniquement).</summary>
    public bool IsLive
    {
        get => _isLive;
        set
        {
            if (!SetProperty(ref _isLive, value)) return;
            if (value) PollNewEntries();
            UpdateStatus();
        }
    }

    public string Title => _levelFilter switch
    {
        LogLevelFilter.Error => "Erreurs",
        LogLevelFilter.Warning => "Avertissements",
        LogLevelFilter.Info => "Informations",
        LogLevelFilter.Debug => "Débogage",
        _ => IsArchive ? "Archive du journal" : "Tous les messages"
    };

    // --- Répertoire des journaux -----------------------------------------

    public string DirectoryPath => AppLog.DirectoryPath;
    public string CurrentPath => IsArchive ? AppLog.ArchivePath : AppLog.FilePath;
    public string FileName => Path.GetFileName(CurrentPath);
    public string CurrentFileSize => DescribeFile(AppLog.FilePath);
    public string ArchiveFileSize => DescribeFile(AppLog.ArchivePath);
    public bool ArchiveExists => File.Exists(AppLog.ArchivePath);

    // --- Commandes ---------------------------------------------------------

    public ICommand RefreshCommand { get; }
    public ICommand CopySelectedCommand { get; }
    public ICommand CopyVisibleCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand ClearCommand { get; }

    /// <summary>
    /// Appelé quand la fenêtre devient visible / masquée : on ne lit le
    /// fichier que lorsque quelqu'un regarde.
    /// </summary>
    public void SetActive(bool active)
    {
        if (_disposed) return;
        _isActive = active;
        if (!active)
        {
            _liveTimer.Stop();
            return;
        }

        if (!_hasLoaded) Reload();
        else PollNewEntries();
        _liveTimer.Start();
    }

    /// <summary>Recharge entièrement le fichier sélectionné (en arrière-plan).</summary>
    public async void Reload()
    {
        if (_disposed) return;
        var generation = ++_loadGeneration;
        var tail = new LogFileTail(CurrentPath);
        IsLoading = true;

        List<LogEntry> parsed;
        try
        {
            parsed = await Task.Run(() =>
            {
                var list = new List<LogEntry>();
                foreach (var line in tail.ReadNewLines(out _))
                {
                    var entry = tail.Parser.Feed(line);
                    if (entry is not null) list.Add(entry);
                }
                return list;
            });
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration) return;
            IsLoading = false;
            StatusText = $"Lecture impossible : {ex.Message}";
            return;
        }

        // Un rechargement plus récent (changement de fichier…) a pris le relais.
        if (generation != _loadGeneration || _disposed) return;

        if (parsed.Count > MaxEntries)
            parsed.RemoveRange(0, parsed.Count - MaxEntries);
        parsed.Reverse();

        _tail = tail;
        _hasLoaded = true;
        _entries = new ObservableCollection<LogEntry>(parsed);
        _entriesView = CreateView(_entries);
        OnPropertyChanged(nameof(Entries));

        SelectedEntry = null;
        RecountAll();
        IsLoading = false;
        RefreshFileInfo();
        UpdateStatus();
    }

    // --- Lecture en direct -------------------------------------------------

    private void PollNewEntries()
    {
        if (_disposed || !_isActive || !_hasLoaded || _isLoading || IsArchive || !_isLive) return;

        try
        {
            var lines = _tail.ReadNewLines(out bool wasReset);
            if (wasReset)
            {
                // Rotation ou effacement : on repart de zéro.
                Reload();
                return;
            }
            if (lines.Count == 0) return;

            foreach (var line in lines)
            {
                var entry = _tail.Parser.Feed(line);
                if (entry is null) continue;
                _entries.Insert(0, entry);
                CountEntry(entry, +1);
                if (entry.Kind == LogLevelKind.Error) PushRecentError(entry);
            }

            while (_entries.Count > MaxEntries)
            {
                var oldest = _entries[^1];
                _entries.RemoveAt(_entries.Count - 1);
                CountEntry(oldest, -1);
            }

            RefreshFileInfo();
            UpdateStatus();
        }
        catch (IOException)
        {
            // Fichier momentanément verrouillé : nouvel essai au prochain tick.
        }
    }

    // --- Filtrage ------------------------------------------------------------

    private ICollectionView CreateView(ObservableCollection<LogEntry> source)
    {
        var view = new ListCollectionView(source) { Filter = Matches };
        return view;
    }

    private bool Matches(object item)
    {
        if (item is not LogEntry entry) return false;

        bool levelOk = _levelFilter switch
        {
            LogLevelFilter.Error => entry.Kind == LogLevelKind.Error,
            LogLevelFilter.Warning => entry.Kind == LogLevelKind.Warning,
            LogLevelFilter.Info => entry.Kind == LogLevelKind.Info,
            LogLevelFilter.Debug => entry.Kind == LogLevelKind.Debug,
            _ => true
        };
        if (!levelOk) return false;

        var search = _searchText.Trim();
        return search.Length == 0 || entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshFilter()
    {
        _entriesView.Refresh();
        UpdateStatus();
    }

    // --- Compteurs -------------------------------------------------------

    private void RecountAll()
    {
        int errors = 0, warnings = 0;
        foreach (var e in _entries)
        {
            if (e.Kind == LogLevelKind.Error) errors++;
            else if (e.Kind == LogLevelKind.Warning) warnings++;
        }
        ErrorCount = errors;
        WarningCount = warnings;

        RecentErrors.Clear();
        foreach (var e in _entries.Where(e => e.Kind == LogLevelKind.Error).Take(RecentErrorsCount))
            RecentErrors.Add(e);
    }

    private void CountEntry(LogEntry entry, int delta)
    {
        if (entry.Kind == LogLevelKind.Error) ErrorCount += delta;
        else if (entry.Kind == LogLevelKind.Warning) WarningCount += delta;
    }

    private void PushRecentError(LogEntry entry)
    {
        RecentErrors.Insert(0, entry);
        while (RecentErrors.Count > RecentErrorsCount)
            RecentErrors.RemoveAt(RecentErrors.Count - 1);
    }

    private void UpdateStatus()
    {
        VisibleCount = _entriesView.Cast<object>().Count();
        OnPropertyChanged(nameof(TotalCount));

        var status = VisibleCount == TotalCount
            ? $"{TotalCount:N0} entrées"
            : $"{VisibleCount:N0} sur {TotalCount:N0} entrées";
        if (!IsArchive && _isLive) status += " · en direct";
        StatusText = status;
    }

    private void RefreshFileInfo()
    {
        OnPropertyChanged(nameof(CurrentFileSize));
        OnPropertyChanged(nameof(ArchiveFileSize));
        OnPropertyChanged(nameof(ArchiveExists));
    }

    private static string DescribeFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return "absent";
            double kb = info.Length / 1024.0;
            return kb < 1024 ? $"{kb:N0} Ko" : $"{kb / 1024.0:N1} Mo";
        }
        catch
        {
            return "—";
        }
    }

    // --- Actions -------------------------------------------------------------

    private void CopySelected()
    {
        if (_selectedEntry is null) return;
        TrySetClipboard(FormatEntry(_selectedEntry));
    }

    private void CopyVisible()
    {
        var text = string.Join(Environment.NewLine,
            _entriesView.Cast<LogEntry>().Reverse().Select(FormatEntry));
        TrySetClipboard(text);
    }

    private static string FormatEntry(LogEntry e)
        => $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss}] [{e.Kind.ToString().ToUpperInvariant()}] {e.Message}";

    private static void TrySetClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            // Presse-papiers verrouillé par une autre application.
            AppLog.Warning($"Copie dans le presse-papiers impossible : {ex.Message}");
        }
    }

    private void OpenFolder()
    {
        try
        {
            if (File.Exists(CurrentPath))
                Process.Start("explorer.exe", $"/select,\"{CurrentPath}\"");
            else
            {
                Directory.CreateDirectory(DirectoryPath);
                Process.Start(new ProcessStartInfo(DirectoryPath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }

    private void OpenFile()
    {
        try
        {
            if (File.Exists(CurrentPath))
                Process.Start(new ProcessStartInfo(CurrentPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }

    private void ClearCurrentLog()
    {
        var answer = System.Windows.MessageBox.Show(
            "Effacer le contenu du journal courant ?\n\nL'archive (winspaces.log.old) est conservée.",
            "WinSpaces — Journaux",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (answer != System.Windows.MessageBoxResult.Yes) return;

        AppLog.Clear();
        AppLog.Info("Journal effacé depuis le tableau de bord.");
        if (IsArchive) Source = LogSource.Current;
        else Reload();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _liveTimer.Stop();
    }
}
