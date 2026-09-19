using System.Collections.ObjectModel;
using System.Windows.Input;
using WinSpaces.Modules.Spotlight.Services;
using WinSpaces.ViewModels;

namespace WinSpaces.Modules.Spotlight.ViewModels;

/// <summary>
/// ViewModel pour la fenêtre Spotlight.
/// Gère la saisie, la recherche en temps réel, la navigation clavier et la sélection.
/// </summary>
public sealed class SpotlightViewModel : ViewModelBase
{
    private readonly SpotlightService _service;
    private string _searchQuery = string.Empty;
    private int _selectedIndex = 0;
    private SpotlightResults? _currentResults;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                PerformSearch();
            }
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    public ObservableCollection<SpotlightResultItem> Results { get; } = new();

    public ICommand ExecuteSelectedCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }

    public event EventHandler? CloseRequested;

    public SpotlightViewModel(SpotlightService service)
    {
        _service = service;

        ExecuteSelectedCommand = new RelayCommand(ExecuteSelected);
        MoveUpCommand = new RelayCommand(MoveUp);
        MoveDownCommand = new RelayCommand(MoveDown);
    }

    private void PerformSearch()
    {
        Results.Clear();
        _selectedIndex = 0;

        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _currentResults = null;
            return;
        }

        _currentResults = _service.Search(_searchQuery);

        // 1. Résultat de calcul (si présent)
        if (_currentResults.Calculation != null)
        {
            Results.Add(new SpotlightResultItem
            {
                Type = ResultType.Calculation,
                Title = _currentResults.Calculation.FormattedResult,
                Subtitle = $"= {_currentResults.Calculation.Expression}",
                Data = _currentResults.Calculation
            });
        }

        // 2. Applications
        foreach (var app in _currentResults.Applications)
        {
            Results.Add(new SpotlightResultItem
            {
                Type = ResultType.Application,
                Title = app.Name,
                Subtitle = app.Path,
                Icon = "📱",
                Data = app
            });
        }

        // 3. Fichiers
        foreach (var file in _currentResults.Files)
        {
            Results.Add(new SpotlightResultItem
            {
                Type = ResultType.File,
                Title = file.Name,
                Subtitle = file.Path,
                Icon = GetFileIcon(file.Name),
                Data = file
            });
        }

        OnPropertyChanged(nameof(Results));


    private void ExecuteSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= Results.Count) return;

        var selected = Results[_selectedIndex];

        switch (selected.Type)
        {
            case ResultType.Application:
                var app = (ApplicationEntry)selected.Data;
                _service.Executor.Execute(app.Path);
                break;

            case ResultType.File:
                var file = (FileEntry)selected.Data;
                _service.Executor.Execute(file.Path);
                break;

            case ResultType.Calculation:
                var calc = (CalculationResult)selected.Data;
                System.Windows.Clipboard.SetText(calc.FormattedResult);
                break;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MoveUp()
    {
        if (Results.Count == 0) return;
        SelectedIndex = _selectedIndex > 0 ? _selectedIndex - 1 : Results.Count - 1;
    }

    private void MoveDown()
    {
        if (Results.Count == 0) return;
        SelectedIndex = _selectedIndex < Results.Count - 1 ? _selectedIndex + 1 : 0;
    }

    public void Clear()
    {
        SearchQuery = string.Empty;
        Results.Clear();
        SelectedIndex = 0;
    }

    private static string GetFileIcon(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".log" => "📄",
            ".pdf" => "📕",
            ".docx" or ".doc" => "📘",
            ".xlsx" or ".xls" => "📊",
            ".pptx" or ".ppt" => "📙",
            ".zip" or ".rar" or ".7z" => "📦",
            ".jpg" or ".png" or ".gif" or ".bmp" => "🖼️",
            ".mp3" or ".wav" or ".flac" => "🎵",
            ".mp4" or ".avi" or ".mkv" => "🎬",
            ".exe" or ".msi" => "⚙️",
            _ => "📄"
        };
    }
}

public class SpotlightResultItem
{
    public required ResultType Type { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public string Icon { get; init; } = "📄";
    public required object Data { get; init; }
}

public enum ResultType
{
    Application,
    File,
    Calculation
}

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

    }
