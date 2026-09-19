using System.Windows;
using System.Windows.Input;
using WinSpaces.Modules.Spotlight.ViewModels;

namespace WinSpaces.Modules.Spotlight.Views;

/// <summary>
/// Fenêtre Spotlight : interface de recherche rapide style macOS.
/// </summary>
public partial class SpotlightWindow : Window
{
    private readonly SpotlightViewModel _viewModel;

    public SpotlightWindow(SpotlightViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.CloseRequested += (_, _) => HideWindow();

        // Positionner en haut centre de l'écran
        Loaded += (_, _) => PositionWindow();
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Left = (workArea.Width - Width) / 2;
        Top = workArea.Top + 100; // 100px depuis le haut
    }

    /// <summary>
    /// Affiche la fenêtre Spotlight et donne le focus.
    /// </summary>
    public void ShowSpotlight()
    {
        Show();
        Activate();
        SearchBox.Focus();
        SearchBox.SelectAll();

        // Animation d'apparition (optionnelle)
        Opacity = 0;
        var animation = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
        BeginAnimation(OpacityProperty, animation);
    }

    /// <summary>
    /// Masque la fenêtre Spotlight et efface la recherche.
    /// </summary>
    public void HideWindow()
    {
        _viewModel.Clear();
        Hide();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Fermer automatiquement quand la fenêtre perd le focus
        HideWindow();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideWindow();
                e.Handled = true;
                break;

            case Key.Up:
                _viewModel.MoveUpCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down:
                _viewModel.MoveDownCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Enter:
                _viewModel.ExecuteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
