namespace WinSpaces.Diagnostics;

/// <summary>
/// États de santé détaillés pour les composants système.
/// </summary>
public enum HealthStatus
{
    /// <summary>Composant opérationnel sans problème.</summary>
    Healthy,
    
    /// <summary>Composant opérationnel mais avec performances dégradées.</summary>
    Degraded,
    
    /// <summary>Composant en erreur critique, fonctionnalité indisponible.</summary>
    Critical,
    
    /// <summary>État inconnu ou non initialisé.</summary>
    Unknown
}
