namespace WinSpaces.Services;

/// <summary>
/// Options applicatives exposées au menu de la barre des tâches et lues par
/// les services au fil de l'eau (démarrage en mémoire pour cette v0.1).
/// </summary>
public sealed class AppOptions
{
    /// <summary>Espace dédié automatique quand une app passe en plein écran.</summary>
    public bool FullscreenSpacesEnabled { get; set; } = true;

    /// <summary>Gestes du trackpad (momentum + Précision Touchpad).</summary>
    public bool GesturesEnabled { get; set; } = true;

    /// <summary>Lancement automatique au démarrage de Windows.</summary>
    public bool AutostartEnabled { get; set; }
}