using WinSpaces.Desktops;
using WinSpaces.Native;
using WinSpaces.Diagnostics;


namespace WinSpaces.Services;

/// <summary>
/// Orchestrateur de gestes : combine MomentumGestureDetector (molette horizontale,
/// universel) et PrecisionTouchpadWatcher (3/4 doigts HID, spécificité Précision
/// Touchpad). Le mode est déterminé au démarrage selon le matériel détecté.
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

    public GestureManager(MessageWindow window, VirtualDesktopService desktops)
    {
        _momentum = new MomentumGestureDetector();
        _precision = new PrecisionTouchpadWatcher(window);
    }

    /// <summary>Vrai si le Précision Touchpad a été trouvé à l'initialisation.</summary>
    public bool PrecisionTouchpadDetected => _precision.PrecisionTouchpadPresent;

    /// <summary>Bascule d'espace demandée : -1 (précédent), +1 (suivant).</summary>
    public event EventHandler<int>? SpaceSwitchRequested;

    public void Start()
    {
        _precision.Start();

        if (_precision.PrecisionTouchpadPresent)
        {
            // Présence détectée : écouter le swipe 3/4 doigts.
            _precision.SwipeDetected += (_, dir) => SpaceSwitchRequested?.Invoke(this, dir);
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
        _momentum.Dispose();
        _precision.Dispose();
    }
}