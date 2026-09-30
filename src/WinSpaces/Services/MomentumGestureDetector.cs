using WinSpaces.Native;
using WinSpaces.Configuration;

namespace WinSpaces.Services;

/// <summary>
/// Détecteur de balayage "momentum" pour les trackpads/souris classiques
/// (2 doigts horizontal ou molette horizontale soutenue). C'est le fallback
/// universel lorsque aucun Précision Touchpad n'est détecté : 
///   - la molette horizontale (WM_MOUSEHWHEEL) émet des crans de ±120 ;
///   - un "fling" horizontal émet plusieurs crans rapprochés dans le temps.
/// On accumule le delta sur une courte fenêtre temporelle ; au-delà du seuil,
/// on déclenche un changement d'espace, avec un anti-rebond.
/// </summary>
internal sealed class MomentumGestureDetector : IDisposable
{
    // Configuration (valeurs par défaut, remplacées par Configure())
    private int _triggerThreshold = 150;
    private TimeSpan _decayWindow = TimeSpan.FromMilliseconds(500);
    private TimeSpan _cooldown = TimeSpan.FromMilliseconds(350);

    private readonly MouseHook _mouseHook = new();
    private int _accumulatedDelta;
    private DateTime _lastWheelUtc = DateTime.MinValue;
    private DateTime _lastTriggerUtc = DateTime.MinValue;
    private bool _disposed;

    /// <summary>
    /// Configure le détecteur de momentum avec les paramètres de configuration.
    /// </summary>
    /// <param name="config">Configuration des gestes.</param>
    public void Configure(GestureConfig config)
    {
        if (config == null) return;
        
        _triggerThreshold = Math.Max(50, config.MomentumTriggerThreshold);
        _decayWindow = TimeSpan.FromMilliseconds(Math.Max(100, config.MomentumDecayWindowMs));
        _cooldown = TimeSpan.FromMilliseconds(Math.Max(100, config.MomentumCooldownMs));
        
        AppLog.Info($"MomentumGestureDetector configuré : TriggerThreshold={_triggerThreshold}, DecayWindow={_decayWindow.TotalMilliseconds}ms, Cooldown={_cooldown.TotalMilliseconds}ms");
    }

    public MouseHook MouseHook => _mouseHook;

    /// <summary>Balayage détecté : -1 (gauche) ou +1 (droite, crans positifs).</summary>
    public event EventHandler<int>? FlingDetected;

    public void Start()
    {
        _mouseHook.MouseWheel += OnMouseWheel;
        _mouseHook.Install();
    }

    public void Stop() => _mouseHook.MouseWheel -= OnMouseWheel;

    private void OnMouseWheel(object? sender, MouseWheelEventArgs e)
    {
        if (!e.IsHorizontal || e.Delta == 0) return;

        var now = DateTime.UtcNow;

        // Fenêtre de décroissance : si trop longtemps sans événement, on repart de zéro.
        if (now - _lastWheelUtc > _decayWindow)
            _accumulatedDelta = 0;

        _lastWheelUtc = now;
        _accumulatedDelta += e.Delta;

        if (Math.Abs(_accumulatedDelta) < _triggerThreshold) return;
        if (now - _lastTriggerUtc < _cooldown) return;

        _lastTriggerUtc = now;
        int direction = _accumulatedDelta > 0 ? 1 : -1;
        _accumulatedDelta = 0;

        FlingDetected?.Invoke(this, direction);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _mouseHook.Dispose();
    }
}