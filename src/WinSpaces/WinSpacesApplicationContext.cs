using System.Runtime.InteropServices;
using System.Windows; // Pour System.Windows.Application et ShutdownMode
using System.Windows.Forms.Integration; // Pour ElementHost.EnableModelessKeyboardInterop
using System.Windows.Threading; // Pour Dispatcher
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
    private readonly ConfigurationService _configurationService;
    private readonly GestureManager _gestures;
    private readonly FullscreenSpaceManager _fullscreen;
    private readonly ThemeEngine _themeEngine;
    private readonly WallpaperMonitor _wallpaperMonitor;
    private readonly WidgetManager _widgetManager;
    private readonly AppOptions _options = new();
    private readonly TrayIconService _tray;
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

    public HealthMonitor HealthMonitor => _healthMonitor;

    private readonly bool _safeMode;

    public WinSpacesApplicationContext(bool safeMode = false, bool launchedAtStartup = false)
    {
        _safeMode = safeMode;
        // Jalons de démarrage : la dernière ligne « Init » du journal indique
        // l'étape où un éventuel plantage s'est produit.
        AppLog.Info("Init 1/5 : application WPF et ressources");
        // IMPORTANT: Initialiser l'application WPF AVANT tout service/fenêtre WPF
        // Cela crée Application.Current, initialise le dispatcher, charge les ressources App.xaml, etc.
        if (System.Windows.Application.Current == null)
        {
            _wpfApp = new System.Windows.Application();
            _wpfApp.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            // Ne pas appeler Run() car on utilise WinForms Application.Run(context).
            // Aucun pompage manuel n'est nécessaire : le Dispatcher WPF poste ses
            // opérations (BeginInvoke, DispatcherTimer, rendu) à sa fenêtre cachée,
            // que la boucle WinForms distribue comme n'importe quel message.
            // L'ancien IMessageFilter faisait un Dispatcher.Invoke SYNCHRONE à chaque
            // message Win32 : boucle imbriquée (ré-entrance) et vidage complet de la
            // file WPF pour chaque WM_INPUT / mouvement de souris.
            LoadSharedResources(_wpfApp);
        }
        else
        {
            _wpfApp = System.Windows.Application.Current;
        }

        // Sans Run(), une exception levée dans une opération du Dispatcher (binding,
        // commande, Clipboard…) remonterait jusqu'à la boucle WinForms et tuerait
        // l'application : on la journalise et on continue.
        Dispatcher.CurrentDispatcher.UnhandledException += OnDispatcherUnhandledException;

        AppLog.Info("Init 2/5 : création des services");
        _vds = new VirtualDesktopService();
        _msgWindow = new MessageWindow();
        _configurationService = new ConfigurationService();
        _widgetManager = new WidgetManager();
        _themeEngine = new ThemeEngine();
        _wallpaperMonitor = new WallpaperMonitor();

        _hotkeys = new HotkeyService(_msgWindow, _vds);
        _globalHotkeys = new GlobalHotkeyManager(_msgWindow);
        _gestures = new GestureManager(_msgWindow, _vds, _configurationService);
        _fullscreen = new FullscreenSpaceManager(_vds, _options);
        _options.AutostartEnabled = AutostartManager.IsEnabled();
        _tray = new TrayIconService(_options);
        
        // Spotlight Module
        _spotlightService = new SpotlightService();
        
        // MenuBar Module
        _menuBarService = new MenuBarService();

        AppLog.Info("Init 3/5 : thème et widgets");
        // Enregistrement des composants pour le diagnostic
        _healthMonitor.Register(_vds);
        _healthMonitor.Register(_themeEngine);
        _healthMonitor.Register(_wallpaperMonitor);
        // Clair / sombre selon la config (« Auto » = réglage Windows). L'ancien
        // DarkTheme forcé ne redéfinissait que le fond : texte illisible.
        _themeEngine.ApplyMode(_configurationService.Current.Theme.Mode);

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

        AppLog.Info("Init 4/5 : événements et raccourcis");
        WireEvents();
        AppLog.Info("Init 5/5 : démarrage des services");
        StartServices();

        // Lancement manuel : on montre la fenêtre, comme une app macOS. Sans elle,
        // seule une icône (souvent masquée dans « ^ » sous Windows 11) prouvait
        // que l'application tournait.
        if (!launchedAtStartup)
            Step("ouverture du tableau de bord", () => ShowDashboard());

        System.Windows.Forms.Application.Idle += OnFirstIdle;
        AppLog.Info("Démarrage terminé.");
    }

    private static void OnFirstIdle(object? sender, EventArgs e)
    {
        System.Windows.Forms.Application.Idle -= OnFirstIdle;
        AppLog.Info("Boucle de messages active : WinSpaces est opérationnel.");
    }

    /// <summary>
    /// Étape de démarrage NON critique : journalisée, et un échec n'empêche pas
    /// le reste de l'application de démarrer.
    /// </summary>
    private static void Step(string name, Action action)
    {
        AppLog.Info($"  → {name}");
        try
        {
            action();
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException($"Échec de l'étape « {name} » (ignorée)", ex));
        }
    }


    

    


    /// <summary>
    /// App.xaml n'est jamais chargé (on instancie une Application nue, pas la
    /// classe générée) : sans ceci, les StaticResource partagées (CornerRadius*,
    /// Padding*, CardStyle…) utilisées par MenuBarWindow et SpotlightWindow sont
    /// introuvables et lèvent une XamlParseException.
    /// </summary>
    private static void LoadSharedResources(System.Windows.Application app)
    {
        foreach (var path in new[] { "Styles.xaml", "Modules/Theming/Themes/WindowStyles.xaml" })
        {
            try
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/WinSpaces;component/{path}")
                });
            }
            catch (Exception ex)
            {
                AppLog.Error(new InvalidOperationException($"Chargement ressources {path} impossible", ex));
            }
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error(new InvalidOperationException("Exception non gérée sur le Dispatcher WPF", e.Exception));
        e.Handled = true;
    }

    private void WireEvents()
    {
        _hotkeys.NextSpaceRequested += (_, _) => SwitchSpace(1, "raccourci");
        _hotkeys.PreviousSpaceRequested += (_, _) => SwitchSpace(-1, "raccourci");
        _hotkeys.NewSpaceRequested += (_, _) =>
        {
            var id = _vds.CreateDesktop();
            if (id != Guid.Empty) _vds.SwitchToDesktop(id);
            AppLog.Info($"Nouvel espace créé : {(id != Guid.Empty ? "OK" : "ÉCHEC")}.");
        };
        _hotkeys.MoveWindowRequested += (_, _) => MoveForegroundWindowToNextDesktop();

        // L'option « Gestes trackpad » du tray n'était lue nulle part.
        _gestures.SpaceSwitchRequested += (_, dir) => SwitchSpace(dir, "geste 3 doigts");
        _gestures.DesktopSwitchRequested += (_, dir) => SwitchSpace(dir, "geste 4 doigts");

        _gestures.MissionControlRequested += (_, _) => { if (_options.GesturesEnabled) ShowDashboard(); };
        _gestures.AppExposeRequested += (_, _) => { if (_options.GesturesEnabled) ShowDashboard(); }; // Pour l'instant même action que Mission Control

        _tray.DashboardRequested += () => ShowDashboard();
        _tray.LogsRequested += () => ShowDashboard(MainViewModel.SectionLogs);
        _tray.NextSpaceRequested += () => SwitchSpace(1, "menu");
        _tray.PreviousSpaceRequested += () => SwitchSpace(-1, "menu");
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

    private DateTime _lastSingleDesktopHintUtc = DateTime.MinValue;

    private void SwitchSpace(int offset, string source)
    {
        if (source.StartsWith("geste") && !_options.GesturesEnabled)
        {
            AppLog.Info($"Bascule ({source}) ignorée : gestes désactivés dans les réglages.");
            return;
        }

        AppLog.Info($"Bascule d'espace {(offset > 0 ? "suivant" : "précédent")} demandée ({source}).");
        if (_vds.SwitchByOffset(offset)) return;

        // Un seul bureau : rien vers quoi basculer. On l'explique (au plus une
        // fois par minute) au lieu de donner l'impression que rien ne marche.
        if (_vds.IsInternalApiAvailable && _vds.GetDesktopIds().Count < 2
            && DateTime.UtcNow - _lastSingleDesktopHintUtc > TimeSpan.FromMinutes(1))
        {
            _lastSingleDesktopHintUtc = DateTime.UtcNow;
            _tray.ShowInfo("WinSpaces — un seul espace",
                "Créez un deuxième espace avec Ctrl+Alt+N (ou le menu de l'icône WinSpaces) pour pouvoir basculer.");
        }
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
        Step("bureaux virtuels (COM)", () =>
        {
            if (!_vds.Initialize())
                _tray.ShowInfo("WinSpaces", "API bureaux virtuels indisponible. Seule l'icône sera active.");
        });
        Step("raccourcis clavier", () => _hotkeys.Install());

        if (_safeMode)
        {
            AppLog.Warning("Mode sans échec : gestes, plein écran automatique et barre de menu désactivés.");
        }
        else
        {
            Step("gestes (touchpad / molette)", () => _gestures.Start());
            Step("plein écran automatique", () => _fullscreen.Start());
        }

        // Initialiser Spotlight (indexation en arrière-plan)
        Step("Spotlight", () => _ = _spotlightService.InitializeAsync());

        if (!_safeMode)
        {
            Step("service de barre de menu", () => _menuBarService.Start());
            Step("fenêtre de barre de menu", ShowMenuBar);
        }

        _tray.ShowInfo($"WinSpaces v{System.Windows.Forms.Application.ProductVersion.Split('+')[0]}{(_safeMode ? " (sans échec)" : "")}",
            "Ctrl+Alt+←/→ pour changer d'espace | Alt+Espace pour Spotlight");
    }

    private void ShowDashboard(string? section = null)
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            var vm = new MainViewModel(_vds, _healthMonitor, _options);
            _mainWindow = new MainWindow(vm);
            ElementHost.EnableModelessKeyboardInterop(_mainWindow);
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }

        if (section is not null) _mainWindow.ShowSection(section);
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
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
            // Fenêtre WPF hors de Application.Run : sans cette interop, la
            // navigation clavier (Tab, raccourcis) n'est pas acheminée à WPF.
            ElementHost.EnableModelessKeyboardInterop(_spotlightWindow);
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
        _vds.SwitchToDesktop(ids[target], moveForegroundWindow: true);
    }

    private void ExitApplication()
    {
        if (_disposed) return;
        _disposed = true;

        // Chaque étape est isolée : auparavant, une seule exception (ex. fenêtre
        // déjà détruite) sautait tout le reste — hooks globaux, raccourcis et
        // icône du tray restaient alors actifs.
        Safe(_wallpaperMonitor.Dispose);
        Safe(_menuBarService.Stop);
        Safe(_widgetManager.SaveStateAndCloseAll);

        Safe(() => { _menuBarWindow?.Close(); _menuBarWindow = null; });
        Safe(() => { _spotlightWindow?.Close(); _spotlightWindow = null; });
        // MainWindow annule Close() (masquage dans le tray) : fermeture forcée.
        Safe(() => { _mainWindow?.ForceClose(); _mainWindow = null; });

        Safe(_spotlightService.Dispose);
        Safe(_menuBarService.Dispose);
        Safe(_globalHotkeys.Dispose);
        Safe(_tray.Dispose);
        Safe(_fullscreen.Dispose);
        Safe(_gestures.Dispose);
        Safe(_hotkeys.Dispose);
        Safe(_configurationService.Dispose);
        Safe(_vds.Dispose);
        Safe(_msgWindow.DestroyHandle);

        Safe(() => Dispatcher.CurrentDispatcher.UnhandledException -= OnDispatcherUnhandledException);

        // Arrêter proprement l'application WPF
        Safe(() => { _wpfApp?.Shutdown(); _wpfApp = null; });

        ExitThread();
    }

    private static void Safe(Action step)
    {
        try { step(); }
        catch (Exception ex) { AppLog.Error(ex); }
    }

    protected override void Dispose(bool disposing)
    {
        // Ne PAS positionner _disposed ici : ExitApplication() sortait aussitôt et
        // rien n'était libéré quand Application.Run se terminait autrement que par
        // « Quitter » (fermeture de session…), laissant une icône fantôme.
        if (disposing)
            ExitApplication();
        base.Dispose(disposing);
    }
}
