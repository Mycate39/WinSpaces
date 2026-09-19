namespace WinSpaces.Diagnostics;

/// <summary>
/// Adaptateur pour les implémentations legacy de IHealthCheckable qui n'ont pas
/// encore les nouveaux membres v2.1 (DetailedStatus, Metrics, LastCheck).
/// Permet la rétrocompatibilité pendant la migration progressive.
/// </summary>
public static class HealthCheckableExtensions
{
    private static readonly Dictionary<IHealthCheckable, DateTime> _lastCheckCache = new();
    private static readonly Dictionary<IHealthCheckable, Dictionary<string, object>> _metricsCache = new();

    /// <summary>
    /// Obtient le DetailedStatus pour les implémentations legacy.
    /// </summary>
    public static HealthStatus GetDetailedStatus(this IHealthCheckable component)
    {
        if (component is HealthCheckableBase hcb)
            return hcb.DetailedStatus;

        // Fallback pour legacy : conversion simple IsHealthy -> DetailedStatus
        return component.IsHealthy ? HealthStatus.Healthy : HealthStatus.Critical;
    }

    /// <summary>
    /// Obtient les métriques pour les implémentations legacy (retourne un dictionnaire vide).
    /// </summary>
    public static IReadOnlyDictionary<string, object> GetMetrics(this IHealthCheckable component)
    {
        if (component is HealthCheckableBase hcb)
            return hcb.Metrics;

        // Fallback pour legacy : créer un dictionnaire vide en cache
        if (!_metricsCache.TryGetValue(component, out var metrics))
        {
            metrics = new Dictionary<string, object>();
            _metricsCache[component] = metrics;
        }

        return metrics;
    }

    /// <summary>
    /// Obtient le LastCheck pour les implémentations legacy.
    /// </summary>
    public static DateTime GetLastCheck(this IHealthCheckable component)
    {
        if (component is HealthCheckableBase hcb)
            return hcb.LastCheck;

        // Fallback pour legacy : retourner l'heure actuelle et la mettre en cache
        if (!_lastCheckCache.TryGetValue(component, out var lastCheck))
        {
            lastCheck = DateTime.UtcNow;
            _lastCheckCache[component] = lastCheck;
        }

        return lastCheck;
    }

    /// <summary>
    /// Met à jour le LastCheck pour les implémentations legacy.
    /// </summary>
    public static void UpdateLastCheck(this IHealthCheckable component)
    {
        if (component is HealthCheckableBase)
            return; // HealthCheckableBase gère déjà cela

        _lastCheckCache[component] = DateTime.UtcNow;
    }
}
