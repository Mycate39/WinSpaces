using System.Windows;
using System.Windows.Input;
using WinSpaces.ViewModels;

namespace WinSpaces.Views;

/// <summary>
/// Tableau de bord WinSpaces : fenêtre sans bordure système façon macOS
/// (feux tricolores, barre latérale), avec aperçu, diagnostic et journaux.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Thickness _shadowMargin;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _shadowMargin = Frame.Margin;

        // Les journaux ne sont lus que lorsque la fenêtre est visible.
        IsVisibleChanged += (_, e) =>
        {
            bool visible = (bool)e.NewValue;
            _viewModel.Logs.SetActive(visible);
            if (visible) _viewModel.Refresh();
        };
        StateChanged += (_, _) => UpdateFrameForState();
        Closed += (_, _) => _viewModel.Dispose();
    }

    /// <summary>Affiche une section de la barre latérale (overview, logs, errors…).</summary>
    public void ShowSection(string section) => _viewModel.SelectedSection = section;

    /// <summary>Ferme réellement la fenêtre (sortie de l'application).</summary>
    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose) return;

        // Cache la fenêtre plutôt que de la fermer (minimiser dans le tray)
        e.Cancel = true;
        Hide();
    }

    // --- Barre de titre ------------------------------------------------------

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2)
        {
            ToggleZoom();
            return;
        }
        try { DragMove(); }
        catch (InvalidOperationException) { /* bouton relâché entre-temps */ }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnZoomClick(object sender, RoutedEventArgs e) => ToggleZoom();

    private void ToggleZoom()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>
    /// Fenêtre sans bordure : en plein écran on retire la marge d'ombre et on
    /// limite la taille à la zone de travail pour ne pas recouvrir la barre des tâches.
    /// </summary>
    private void UpdateFrameForState()
    {
        if (WindowState == WindowState.Maximized)
        {
            var area = SystemParameters.WorkArea;
            MaxWidth = area.Width;
            MaxHeight = area.Height;
            Frame.Margin = new Thickness(0);
        }
        else
        {
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
            Frame.Margin = _shadowMargin;
        }
    }
}
