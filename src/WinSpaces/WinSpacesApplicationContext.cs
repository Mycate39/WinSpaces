using System.Runtime.InteropServices;
using WinSpaces.Desktops;
using WinSpaces.Native;
using WinSpaces.Services;

namespace WinSpaces;

/// <summary>
/// ApplicationContext WinSpaces : instancie tous les services, branche les
/// événements et gère le cycle de vie. Lancé par Program.Main via
/// Application.Run(context).
/// </summary>
internal sealed class WinSpacesApplicationContext : ApplicationContext
{
    private readonly VirtualDesktopService _vds;
    private readonly MessageWindow _msgWindow;
    private readonly HotkeyService _hotkeys;
    private readonly GestureManager _gestures;
    private readonly FullscreenSpaceManager _fullscreen;
    private readonly TrayIconService _tray;
    private readonly AppOptions _options = new();
    private bool _disposed;

    public WinSpacesApplicationContext()
    {
        _vds = new VirtualDesktopService();
        _msgWindow = new MessageWindow();
        _hotkeys = new HotkeyService(_msgWindow, _vds);
        _gestures = new GestureManager(_msgWindow, _vds);
        _fullscreen = new FullscreenSpaceManager(_vds);
        _tray = new TrayIconService(_options);

        WireEvents();
        StartServices();
    }

    // @@CTX2@@

    private void WireEvents()
    {
        _hotkeys.NextSpaceRequested += (_, _) => _vds.SwitchByOffset(1);
        _hotkeys.PreviousSpaceRequested += (_, _) => _vds.SwitchByOffset(-1);
        _hotkeys.NewSpaceRequested += (_, _) =>
        {
            var id = _vds.CreateDesktop();
            if (id != Guid.Empty) _vds.SwitchToDesktop(id);
        };
        _hotkeys.MoveWindowRequested += (_, _) => MoveForegroundWindowToNextDesktop();

        _gestures.SpaceSwitchRequested += (_, dir) => _vds.SwitchByOffset(dir);

        _tray.NextSpaceRequested += () => _vds.SwitchByOffset(1);
        _tray.PreviousSpaceRequested += () => _vds.SwitchByOffset(-1);
        _tray.NewSpaceRequested += () =>
        {
            var id = _vds.CreateDesktop();
            if (id != Guid.Empty) _vds.SwitchToDesktop(id);
        };
        _tray.MoveWindowRequested += MoveForegroundWindowToNextDesktop;
        _tray.ExitRequested += ExitApplication;
    }

    private void StartServices()
    {
        bool ok = _vds.Initialize();
        if (!ok)
        {
            _tray.ShowInfo("WinSpaces",
                "API bureaux virtuels indisponible. Seule l'icône sera active.");
        }
        _hotkeys.Install();
        _gestures.Start();
        _fullscreen.Start();
        _tray.ShowInfo("WinSpaces v0.1",
            "Ctrl+Alt+←/→ pour changer d'espace.");
    }

    private void MoveForegroundWindowToNextDesktop()
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero) return;
        var ids = _vds.GetDesktopIds();
        int idx = _vds.CurrentDesktopIndex;
        if (idx < 0 || ids.Count < 2) return;
        int target = (idx + 1) % ids.Count;
        _vds.MoveWindowToDesktop(fg, ids[target]);
        _vds.SwitchToDesktop(ids[target]);
    }

    private void ExitApplication()
    {
        try
        {
            _tray.Dispose();
            _fullscreen.Dispose();
            _gestures.Dispose();
            _hotkeys.Dispose();
            _vds.Dispose();
            _msgWindow.DestroyHandle();
        }
        finally
        {
            ExitThread();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            ExitApplication();
        }
        base.Dispose(disposing);
    }
}