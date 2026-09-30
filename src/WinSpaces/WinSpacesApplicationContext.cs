using System.Runtime.InteropServices;
using System.Windows; // Pour System.Windows.Application et ShutdownMode
using System.Windows.Interop; // Pour ComponentDispatcher
using System.Windows.Forms; // Pour IMessageFilter
using System.Windows.Threading; // Pour DispatcherPriority
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

    // Instance de l'application WPF - CRITICAL pour les fenêtres WPF, ressources, bindings, etc.
    private System.Windows.Application? _wpfApp;
    
    // Message filter pour pomper le dispatcher WPF depuis la boucle WinForms
    private WpfMessageFilter? _wpfMessageFilter;

    public HealthMonitor HealthMonitor => _healthMonitor;

    public WinSpacesApplicationContext()
    {
        // IMPORTANT: Initialiser l'application WPF AVANT tout service/fenêtre WPF
        // Cela crée Application.Current, initialise le dispatcher, charge les ressources App.xaml, etc.
        if (System.Windows.Application.Current == null)
        {
            _wpfApp = new System.Windows.Application();
            _wpfApp.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            // Ne pas appeler Run() car on utilise WinForms Application.Run(context)
            // Le dispatcher WPF sera pompé par la boucle de messages WinForms via IMessageFilter
        }
        else
        {
            _wpfApp = System.Windows.Application.Current;
        }
        
        // Installer le message filter pour pomper le dispatcher WPF
        _wpfMessageFilter = new WpfMessageFilter();
        System.Windows.Forms.Application.AddMessageFilter(_wpfMessageFilter);

        _vds = new VirtualDesktopService();
        _msgWindow = new MessageWindow();
        _configurationService = new ConfigurationService();
        _widgetManager = new WidgetManager();
        _themeEngine = new ThemeEngine();
        _wallpaperMonitor = new WallpaperMonitor();

        _hotkeys = new HotkeyService(_msgWindow, _vds);
        _globalHotkeys = new GlobalHotkeyManager(_msgWindow);
        _gestures = new GestureManager(_msgWindow, _vds, _configurationService);
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

        // Enable debug logging if configured
        AppLog.SetDebugEnabled(_configurationService.Current.Global.DebugLogging);

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
        AppLog.Info("WinSpacesApplicationContext: MenuBar service started.");
        ShowMenuBar();
        
        _tray.ShowInfo("WinSpaces v2.5.3",
            "Ctrl+Alt+←/→ pour changer d'espace | Alt+Espace pour Spotlight");
    }

    private void ShowDashboard()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            var vm = new MainViewModel(_vds, _healthMonitor, _options);
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
            AppLog.Info("WinSpacesApplicationContext: MenuBar window created.");
        }

        _menuBarWindow.Show();
        AppLog.Info("WinSpacesApplicationContext: MenuBar window shown.");
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
            // Arrêter les services de premier plan avant de fermer les fenêtres
            _wallpaperMonitor.Dispose();
            _menuBarService.Stop();
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
            _globalHotkeys.Dispose();
            _tray.Dispose();
            _fullscreen.Dispose();
            _gestures.Dispose();
            _hotkeys.Dispose();
            _vds.Dispose();
            _msgWindow.DestroyHandle();

            // Retirer le message filter WPF
            if (_wpfMessageFilter != null)
            {
                System.Windows.Forms.Application.RemoveMessageFilter(_wpfMessageFilter);
                _wpfMessageFilter = null;
            }

            // Arrêter proprement l'application WPF
            if (_wpfApp != null)
            {
                try
                {
                    _wpfApp.Shutdown();
                }
                catch (Exception ex)
                {
                    AppLog.Error(new InvalidOperationException("Erreur arrêt WPF Application", ex));
                }
                _wpfApp = null;
            }
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

/// <summary>
/// Message filter pour pomper le dispatcher WPF depuis la boucle de messages WinForms.
/// Cela permet aux DispatcherTimer, bindings, et événements WPF de fonctionner correctement
/// quand on utilise Application.Run() de WinForms au lieu de WPF Application.Run().
/// </summary>
internal sealed class WpfMessageFilter : IMessageFilter
{
    public bool PreFilterMessage(ref Message m)
    {
        // Pomper le dispatcher WPF pour traiter les messages en attente
        if (System.Windows.Application.Current != null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => { }));
        }
        return false; // Ne pas consommer le message
    }
}