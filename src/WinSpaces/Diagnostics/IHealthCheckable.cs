namespace WinSpaces.Diagnostics;

/// <summary>
/// Définit un composant pouvant être interrogé sur son état de santé.
/// Version étendue v2.1+ avec métriques de performance détaillées.
/// </summary>
public interface IHealthCheckable
{
    /// <summary>Nom du composant (ex: "Virtual Desktop Service").</summary>
    string ComponentName { get; }
    
    /// <summary>Indicateur simple : true si opérationnel, false sinon.</summary>
    bool IsHealthy { get; }
    
    /// <summary>Message d'état lisible (ex: "Actif : 4 bureaux détectés").</summary>
    string StatusMessage { get; }
    
    // --- Nouveaux membres v2.1 ---
    
    /// <summary>État de santé détaillé (Healthy, Degraded, Critical, Unknown).</summary>
    HealthStatus DetailedStatus { get; }
    
    /// <summary>
    /// Métriques spécifiques au composant (ex: CPU%, latence ms, compteurs).
    /// Clés communes : "cpu_percent", "memory_mb", "latency_ms", "items_count", "error_count"
    /// </summary>
    IReadOnlyDictionary<string, object> Metrics { get; }
    
    /// <summary>Horodatage du dernier contrôle de santé.</summary>
    DateTime LastCheck { get; }
}
