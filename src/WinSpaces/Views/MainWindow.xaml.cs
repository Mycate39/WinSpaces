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

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Cache la fenêtre plutôt que de la fermer (minimiser dans le tray)
        e.Cancel = true;
        Hide();
    }
}
