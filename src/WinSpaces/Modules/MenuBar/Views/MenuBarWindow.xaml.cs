using System.Windows;
using System.Windows.Interop;
using WinSpaces.Modules.MenuBar.ViewModels;

namespace WinSpaces.Modules.MenuBar.Views;

/// <summary>
/// Fenêtre de la barre de menu système (MenuBar).
/// Ancrée en haut de l'écran principal, toujours visible.
/// </summary>
public partial class MenuBarWindow : Window
{
    private readonly MenuBarViewModel _viewModel;

    public MenuBarWindow(MenuBarViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Positionner la fenêtre en haut de l'écran principal
        PositionMenuBar();

        // Rendre la fenêtre non-clickable pour les événements de souris (pass-through)
        MakeWindowTransparentToMouseExceptContent();
    }

    /// <summary>
    /// Positionne la MenuBar sur toute la largeur de l'écran principal.
    /// </summary>
    private void PositionMenuBar()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen?.WorkingArea == null) return;

        var workingArea = screen.WorkingArea;

        Left = workingArea.Left;
        Top = workingArea.Top;
        Width = workingArea.Width;
        Height = 32; // Hauteur fixe

        AppLog.Info($"MenuBar : Positionnée sur {Width}x{Height} à ({Left}, {Top})");
    }

    /// <summary>
    /// Rend la fenêtre transparente aux clics sauf sur le contenu.
    /// Permet d'interagir avec les applications en dessous.
    /// </summary>
    private void MakeWindowTransparentToMouseExceptContent()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            const int GWL_EXSTYLE = -20;
            const int WS_EX_TRANSPARENT = 0x00000020;
            const int WS_EX_LAYERED = 0x00080000;

            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            
            // Ne pas appliquer WS_EX_TRANSPARENT pour permettre l'interaction avec les boutons
            // SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur configuration fenêtre MenuBar", ex));
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
