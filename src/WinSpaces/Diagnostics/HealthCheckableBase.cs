namespace WinSpaces.Diagnostics;

/// <summary>
/// Classe de base abstraite implémentant IHealthCheckable avec gestion automatique
/// des métriques et de l'horodatage. Facilite l'implémentation pour les services.
/// </summary>
public abstract class HealthCheckableBase : IHealthCheckable
{
    private readonly Dictionary<string, object> _metrics = new();
    
    protected HealthCheckableBase()
    {
        LastCheck = DateTime.UtcNow;
    }

    public abstract string ComponentName { get; }
    public abstract bool IsHealthy { get; }
    public abstract string StatusMessage { get; }

    public virtual HealthStatus DetailedStatus
    {
        get
        {
            if (IsHealthy) return HealthStatus.Healthy;
            return HealthStatus.Critical;
        }
    }

    public IReadOnlyDictionary<string, object> Metrics => _metrics;
    
    public DateTime LastCheck { get; private set; }

    /// <summary>
    /// Met à jour ou ajoute une métrique de performance.
    /// </summary>
    protected void SetMetric(string key, object value)
    {
        _metrics[key] = value;
        LastCheck = DateTime.UtcNow;
    }

    /// <summary>
    /// Supprime une métrique de performance.
    /// </summary>
    protected void RemoveMetric(string key)
    {
        _metrics.Remove(key);
        LastCheck = DateTime.UtcNow;
    }

    /// <summary>
    /// Efface toutes les métriques.
    /// </summary>
    protected void ClearMetrics()
    {
        _metrics.Clear();
        LastCheck = DateTime.UtcNow;
    }

    /// <summary>
    /// Méthode à appeler pour rafraîchir l'horodatage sans modifier les métriques.
    /// </summary>
    protected void UpdateLastCheck()
    {
        LastCheck = DateTime.UtcNow;
    }
}
