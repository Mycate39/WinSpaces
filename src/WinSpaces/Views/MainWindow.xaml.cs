using System.Windows;
using WinSpaces.ViewModels;

namespace WinSpaces.Views;

/// <summary>
/// MainWindow : fenêtre principale du dashboard WinSpaces.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Diagnostics.RefreshStatus();
        _viewModel.RefreshDesktopInfo();
    }

    private bool _allowClose;

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
}
