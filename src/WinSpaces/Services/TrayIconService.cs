using System.Drawing;
using System.Drawing.Drawing2D;
using WinSpaces.Native;

namespace WinSpaces.Services;

/// <summary>
/// Icône de la barre des tâches + menu contextuel. L'icône est dessinée
/// à la volée (trois barres arrondies rappelant les "Espaces" de macOS)
/// pour éviter toute ressource externe.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private bool _disposed;

    public event Action? NextSpaceRequested;
    public event Action? PreviousSpaceRequested;
    public event Action? NewSpaceRequested;
    public event Action? MoveWindowRequested;
    public event Action? ExitRequested;

    public TrayIconService(AppOptions options)
    {
        _icon = new NotifyIcon { Visible = true, Text = "WinSpaces — Espaces pour Windows" };
        _icon.Icon = CreateIcon();
        _icon.DoubleClick += (_, _) => NextSpaceRequested?.Invoke();

        _menu = new ContextMenuStrip();
        _menu.Items.Add(new ToolStripMenuItem("Nouvel espace", null, (_, _) => NewSpaceRequested?.Invoke()) { ShortcutKeyDisplayString = "Ctrl+Alt+N" });
        _menu.Items.Add(new ToolStripMenuItem("Espace suivant", null, (_, _) => NextSpaceRequested?.Invoke()) { ShortcutKeyDisplayString = "Ctrl+Alt+→" });
        _menu.Items.Add(new ToolStripMenuItem("Espace précédent", null, (_, _) => PreviousSpaceRequested?.Invoke()) { ShortcutKeyDisplayString = "Ctrl+Alt+←" });
        _menu.Items.Add(new ToolStripMenuItem("Déplacer fenêtre → espace suivant", null, (_, _) => MoveWindowRequested?.Invoke()) { ShortcutKeyDisplayString = "Ctrl+Alt+W" });
        _menu.Items.Add(new ToolStripSeparator());

        var fsItem = new ToolStripMenuItem("Espace plein écran auto") { CheckOnClick = true, Checked = options.FullscreenSpacesEnabled };
        fsItem.Click += (_, _) => options.FullscreenSpacesEnabled = fsItem.Checked;
        _menu.Items.Add(fsItem);

        var gpItem = new ToolStripMenuItem("Gestes trackpad") { CheckOnClick = true, Checked = options.GesturesEnabled };
        gpItem.Click += (_, _) => options.GesturesEnabled = gpItem.Checked;
        _menu.Items.Add(gpItem);

        _menu.Items.Add(new ToolStripSeparator());

        var asItem = new ToolStripMenuItem("Démarrer avec Windows") { CheckOnClick = true, Checked = options.AutostartEnabled };
        asItem.Click += (_, _) => { options.AutostartEnabled = asItem.Checked; AutostartManager.SetEnabled(asItem.Checked); };
        _menu.Items.Add(asItem);

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Quitter", null, (_, _) => ExitRequested?.Invoke()));

        _icon.ContextMenuStrip = _menu;
    }

    public void ShowInfo(string title, string text)
        => _icon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);

    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Fond arrondi sombre.
            using var bg = new SolidBrush(Color.FromArgb(40, 44, 52));
            using var path = RoundedRect(new Rectangle(1, 1, 30, 30), 8);
            g.FillPath(bg, path);

            // Trois barres = métaphore "espaces".
            using var bar = new SolidBrush(Color.FromArgb(88, 166, 255));
            g.FillRectangle(bar, new Rectangle(7,  9, 5, 14));
            g.FillRectangle(bar, new Rectangle(14, 7, 5, 16));
            g.FillRectangle(bar, new Rectangle(21, 9, 5, 14));
        }

        IntPtr hIcon = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
        finally { NativeMethods.DestroyIcon(hIcon); }
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}