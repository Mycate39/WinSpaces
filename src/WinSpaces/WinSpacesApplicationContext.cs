using System.Runtime.InteropServices;
using WinSpaces.Desktops;
using WinSpaces.Native;
using WinSpaces.Services;
using WinSpaces.Diagnostics;
using WinSpaces.Views;
using WinSpaces.ViewModels;
using WinSpaces.Modules.Spotlight.Services;
using WinSpaces.Modules.Spotlight.Views;
using WinSpaces.Modules.Spotlight.ViewModels;
using WinSpaces.Modules.MenuBar.Services;
using WinSpaces.Modules.MenuBar.Views;
using WinSpaces.Modules.MenuBar.ViewModels;
using WinSpaces.Configuration;
using WinSpaces.Modules.Widgets.Core;
using WinSpaces.Modules.Theming.Services;



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
    private readonly GlobalHotkeyManager _globalHotkeys;
    private readonly GestureManager _gestures;
    private readonly FullscreenSpaceManager _fullscreen;
    private readonly ThemeEngine _themeEngine;
    private readonly WallpaperMonitor _wallpaperMonitor;

    private readonly ConfigurationService _configurationService;
    private readonly WidgetManager _widgetManager;

    private readonly TrayIconService _tray;
    private readonly AppOptions _options = new();
    private readonly HealthMonitor _healthMonitor = new();
    
    // Spotlight Module
    private readonly SpotlightService _spotlightService;
    private SpotlightWindow? _spotlightWindow;
    
    // MenuBar Module
    private readonly MenuBarService _menuBarService;
    private MenuBarWindow? _menuBarWindow;
    
    private MainWindow? _mainWindow;
    private bool _disposed;

    public HealthMonitor HealthMonitor => _healthMonitor;

    public WinSpacesApplicationContext()
    {
        _vds = new VirtualDesktopService();
        _msgWindow = new MessageWindow();
        _configurationService = new ConfigurationService();
        _widgetManager = new WidgetManager();
        _themeEngine = new ThemeEngine();
        _wallpaperMonitor = new WallpaperMonitor();

        _hotkeys = new HotkeyService(_msgWindow, _vds);
        _globalHotkeys = new GlobalHotkeyManager(_msgWindow);
        _gestures = new GestureManager(_msgWindow, _vds);
        _fullscreen = new FullscreenSpaceManager(_vds);
        _tray = new TrayIconService(_options);
        
        // Spotlight Module
        _spotlightService = new SpotlightService();
        
        // MenuBar Module
        _menuBarService = new MenuBarService();

        // Enregistrement des composants pour le diagnostic
        _healthMonitor.Register(_vds);
        _healthMonitor.Register(_themeEngine);
        _healthMonitor.Register(_wallpaperMonitor);
        _themeEngine.ApplyTheme("DarkTheme"); // Thème par défaut

        _healthMonitor.Register(_widgetManager);
        _widgetManager.InitializeAndLoadWidgets();

        _healthMonitor.Register(_hotkeys);
        _healthMonitor.Register(_gestures);
        _healthMonitor.Register(_fullscreen);
        _healthMonitor.Register(_globalHotkeys);
        _healthMonitor.Register(_spotlightService);
        _healthMonitor.Register(_menuBarService);

        WireEvents();
        StartServices();
    }


    

    


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

        _tray.DashboardRequested += ShowDashboard;
        _tray.NextSpaceRequested += () => _vds.SwitchByOffset(1);
        _tray.PreviousSpaceRequested += () => _vds.SwitchByOffset(-1);
        _tray.NewSpaceRequested += () =>
        {
            var id = _vds.CreateDesktop();
            if (id != Guid.Empty) _vds.SwitchToDesktop(id);
        };
        _tray.MoveWindowRequested += MoveForegroundWindowToNextDesktop;
        _tray.ExitRequested += ExitApplication;
        
        // Spotlight hotkey: Alt+Space
        RegisterSpotlightHotkey();
    }

    private void RegisterSpotlightHotkey()
    {
        try
        {
            // Alt+Space (VK_SPACE = 0x20)
            _globalHotkeys.Register(
                (uint)KeyboardKeys.MOD_ALT, 
                KeyboardKeys.VK_SPACE, 
                ToggleSpotlight
            );
            
            AppLog.Info("Spotlight : Raccourci Alt+Espace enregistré");
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur enregistrement hotkey Spotlight", ex));
        }
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
        
        // Initialiser Spotlight (indexation en arrière-plan)
        _ = _spotlightService.InitializeAsync();
        
        // Initialiser et afficher MenuBar
        _menuBarService.Start();
        ShowMenuBar();
        
        _tray.ShowInfo("WinSpaces v2.5.3",
            "Ctrl+Alt+←/→ pour changer d'espace | Alt+Espace pour Spotlight");
    }

    private void ShowDashboard()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            var vm = new MainViewModel(_vds, _healthMonitor);
            _mainWindow = new MainWindow(vm);
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void ShowMenuBar()
    {
        if (_menuBarWindow == null || !_menuBarWindow.IsLoaded)
        {
            var vm = new MenuBarViewModel(_menuBarService);
            _menuBarWindow = new MenuBarWindow(vm);
            _menuBarWindow.Closed += (_, _) =>
            {
                vm.Dispose();
                _menuBarWindow = null;
            };
        }

        _menuBarWindow.Show();
    }

    private void ToggleSpotlight()
    {
        if (_spotlightWindow == null || !_spotlightWindow.IsLoaded)
        {
            var vm = new SpotlightViewModel(_spotlightService);
            _spotlightWindow = new SpotlightWindow(vm);
            _spotlightWindow.Closed += (_, _) => _spotlightWindow = null;
        }

        if (_spotlightWindow.IsVisible)
        {
            _spotlightWindow.HideWindow();
        }
        else
        {
            _spotlightWindow.ShowSpotlight();
        }
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
        if (_disposed) return;
        _disposed = true;

        try
        {
            _widgetManager.SaveStateAndCloseAll();

            if (_menuBarWindow != null)
            {
                _menuBarWindow.Close();
                _menuBarWindow = null;
            }

            if (_spotlightWindow != null)
            {
                _spotlightWindow.Close();
                _spotlightWindow = null;
            }

            if (_mainWindow != null)
            {
                _mainWindow.Close();
                _mainWindow = null;
            }

            _spotlightService.Dispose();
            _menuBarService.Dispose();
            _wallpaperMonitor.Dispose();
            _globalHotkeys.Dispose();
            _tray.Dispose();
            _fullscreen.Dispose();
            _gestures.Dispose();
            _hotkeys.Dispose();
            _vds.Dispose();
            _msgWindow.DestroyHandle();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
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