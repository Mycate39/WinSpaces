using WinSpaces.Diagnostics;
using WinSpaces.Services;

namespace WinSpaces.Modules.Spotlight.Services;

/// <summary>
/// Service principal orchestrant tous les moteurs de recherche Spotlight.
/// Agrège les résultats de toutes les sources (apps, fichiers, calculs).
/// </summary>
public sealed class SpotlightService : HealthCheckableBase, IDisposable
{
    private readonly ApplicationIndexer _appIndexer;
    private readonly FileSearchEngine _fileSearch;
    private readonly CalculatorEngine _calculator;
    private readonly ActionExecutor _executor;
    private bool _disposed;

    public override string ComponentName => "Spotlight Service";
    public override bool IsHealthy => _appIndexer.IsHealthy;
    public override string StatusMessage => _appIndexer.IsHealthy 
        ? "Prêt pour recherche" 
        : "Indexation en cours...";

    public ApplicationIndexer AppIndexer => _appIndexer;
    public FileSearchEngine FileSearch => _fileSearch;
    public CalculatorEngine Calculator => _calculator;
    public ActionExecutor Executor => _executor;

    public SpotlightService()
    {
        _appIndexer = new ApplicationIndexer();
        _fileSearch = new FileSearchEngine();
        _calculator = new CalculatorEngine();
        _executor = new ActionExecutor();

        SetMetric("searches_count", 0);
    }

    /// <summary>
    /// Initialise le service (lance l'indexation des applications).
    /// </summary>
    public async Task InitializeAsync()
    {
        AppLog.Info("Spotlight : Démarrage indexation applications...");
        await _appIndexer.IndexAsync();
        SetMetric("initialization_complete", true);
    }

    /// <summary>
    /// Recherche unifiée sur toutes les sources.
    /// </summary>
    public SpotlightResults Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SpotlightResults
            {
                Query = query,
                Applications = Array.Empty<ApplicationEntry>(),
                Files = Array.Empty<FileEntry>(),
                Calculation = null
            };
        }

        var results = new SpotlightResults { Query = query };

        // 1. Calcul mathématique (priorité haute si détecté)
        results.Calculation = _calculator.TryEvaluate(query);

        // 2. Applications (max 5)
        results.Applications = _appIndexer.Search(query, maxResults: 5).ToArray();

        // 3. Fichiers (max 3)
        results.Files = _fileSearch.Search(query, maxResults: 3).ToArray();

        // Incrémenter compteur
        var count = (int)(Metrics.GetValueOrDefault("searches_count", 0));
        SetMetric("searches_count", count + 1);
        SetMetric("last_query", query);
        SetMetric("last_search_time", DateTime.UtcNow);

        return results;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _appIndexer.Dispose();
    }
}

/// <summary>
/// Résultats agrégés d'une recherche Spotlight.
/// </summary>
public class SpotlightResults
{
    public required string Query { get; init; }
    public ApplicationEntry[] Applications { get; set; } = Array.Empty<ApplicationEntry>();
    public FileEntry[] Files { get; set; } = Array.Empty<FileEntry>();
    public CalculationResult? Calculation { get; set; }

    public bool HasResults => Applications.Length > 0 || Files.Length > 0 || Calculation != null;
}
