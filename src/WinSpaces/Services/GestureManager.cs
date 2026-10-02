using WinSpaces.Desktops;
using WinSpaces.Native;
using WinSpaces.Diagnostics;
using WinSpaces.Configuration;


namespace WinSpaces.Services;

/// <summary>
/// Orchestrateur de gestes : combine MomentumGestureDetector (molette horizontale,
/// universel) et PrecisionTouchpadWatcher (3/4 doigts HID, spécificité Précision
/// Touchpad). Le mode est déterminé au démarrage selon le matériel détecté.
/// Supporte les gestes style macOS :
/// - 3 doigts horizontal : switch espace
/// - 4 doigts horizontal : switch bureau (différencié)
/// - 3 doigts haut : Mission Control (dashboard)
/// - 3 doigts bas : App Exposé
/// </summary>
internal sealed class GestureManager : HealthCheckableBase, IDisposable
{
    // IHealthCheckable Implementation
    public override string ComponentName => "Gesture Manager";
    public override bool IsHealthy => PrecisionTouchpadDetected || _momentum.MouseHook.IsInstalled;
    public override string StatusMessage => GetStatusMessage();

    private string GetStatusMessage()
    {
        if (PrecisionTouchpadDetected) return "Mode Precision Touchpad (3/4 doigts)";
        if (_momentum.MouseHook.IsInstalled) return "Mode Momentum (molette horizontale)";
        return "Aucun système de geste actif";
    }

    private readonly MomentumGestureDetector _momentum;
    private readonly PrecisionTouchpadWatcher _precision;
    private bool _disposed;
    private readonly ConfigurationService _configService;

    public GestureManager(MessageWindow window, VirtualDesktopService desktops, ConfigurationService configService)
    {
        _momentum = new MomentumGestureDetector();
        _precision = new PrecisionTouchpadWatcher(window);
        _configService = configService;
        
        // S'abonner aux changements de configuration pour hot-reload
        _configService.ConfigurationChanged += OnConfigurationChanged;
        
        // Appliquer la configuration initiale
        ApplyGestureConfiguration(_configService.Current.Gestures);
    }

    /// <summary>Vrai si le Précision Touchpad a été trouvé à l'initialisation.</summary>
    public bool PrecisionTouchpadDetected => _precision.PrecisionTouchpadPresent;

    /// <summary>Bascule d'espace demandée (3 doigts horizontal) : -1 (précédent), +1 (suivant).</summary>
    public event EventHandler<int>? SpaceSwitchRequested;

    /// <summary>Bascule de bureau demandée (4 doigts horizontal) : -1 (précédent), +1 (suivant).</summary>
    public event EventHandler<int>? DesktopSwitchRequested;

    /// <summary>Mission Control demandé (3 doigts vers le haut).</summary>
    public event EventHandler? MissionControlRequested;

    /// <summary>App Exposé demandé (3 doigts vers le bas).</summary>
    public event EventHandler? AppExposeRequested;

    public void Start()
    {
        _precision.Start();

        if (_precision.PrecisionTouchpadPresent)
        {
            // Présence détectée : écouter les gestes Précision Touchpad.
            _precision.SwipeDetected += (_, dir) => SpaceSwitchRequested?.Invoke(this, dir); // 3 doigts horizontal
            _precision.FourFingerSwipeDetected += (_, dir) => DesktopSwitchRequested?.Invoke(this, dir); // 4 doigts horizontal
            _precision.VerticalSwipeDetected += (_, dir) => 
            {
                // Log pour debug, mais les actions spécifiques sont gérées via ThreeFingerUp/Down
                AppLog.Info($"VerticalSwipeDetected: direction={dir}");
            };
            _precision.ThreeFingerUpActionRequested += (_, _) => MissionControlRequested?.Invoke(this, EventArgs.Empty);
            _precision.ThreeFingerDownActionRequested += (_, _) => AppExposeRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // Fallback universel : molette / trackpad classique.
            _momentum.FlingDetected += (_, dir) => SpaceSwitchRequested?.Invoke(this, dir);
        }

        _momentum.Start();
        AppLog.Info($"Gestes : mode={(_precision.PrecisionTouchpadPresent ? "Précision Touchpad" : "momentum")}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        // Se désabonner des événements de configuration
        if (_configService != null)
            _configService.ConfigurationChanged -= OnConfigurationChanged;
        
        _momentum.Dispose();
        _precision.Dispose();
    }

    /// <summary>Applique la configuration des gestes aux détecteurs.</summary>
    private void ApplyGestureConfiguration(GestureConfig config)
    {
        if (config == null) return;
        
        // Configurer le Precision Touchpad Watcher
        _precision.Configure(config);
        
        // Configurer le Momentum Gesture Detector (fallback)
        _momentum.Configure(config);
        
        AppLog.Info($"Configuration des gestes appliquée : Enabled={config.Enabled}");
    }

    /// <summary>Gestionnaire d'événement pour les changements de configuration (hot-reload).</summary>
    private void OnConfigurationChanged(object? sender, WinSpacesConfiguration e)
    {
        // Appliquer uniquement la section gestes de la nouvelle configuration
        ApplyGestureConfiguration(e.Gestures);
    }
}