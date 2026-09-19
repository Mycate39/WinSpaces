namespace WinSpaces.Diagnostics;

/// <summary>
/// Agrégateur central de santé : collecte et expose l'état de tous les
/// composants implémentant IHealthCheckable pour le tableau de bord.
/// </summary>
public sealed class HealthMonitor
{
    private readonly List<IHealthCheckable> _components = new();

    public IReadOnlyList<IHealthCheckable> Components => _components.AsReadOnly();

    public void Register(IHealthCheckable component)
    {
        if (component != null && !_components.Contains(component))
            _components.Add(component);
    }

    public bool AllHealthy => _components.All(c => c.IsHealthy);

    public int HealthyCount => _components.Count(c => c.IsHealthy);

    public int TotalCount => _components.Count;

    public string GetGlobalStatus()
    {
        if (_components.Count == 0) return "Aucun composant enregistré";
        if (AllHealthy) return $"Tous les systèmes opérationnels ({HealthyCount}/{TotalCount})";
        return $"Attention : {TotalCount - HealthyCount} composant(s) en erreur";
    }
}
