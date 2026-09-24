using System.IO;
using WinSpaces.Diagnostics;
using WinSpaces.Services;

namespace WinSpaces.Modules.Spotlight.Services;

/// <summary>
/// Indexe toutes les applications installées (Start Menu + Program Files).
/// Cache les résultats pour recherche ultra-rapide.
/// </summary>
public sealed class ApplicationIndexer : HealthCheckableBase, IDisposable
{
    private readonly List<ApplicationEntry> _applications = new();
    private readonly object _lock = new();
    private bool _isIndexing;
    private bool _disposed;

    public override string ComponentName => "Application Indexer";
    public override bool IsHealthy => _applications.Count > 0;
    public override string StatusMessage => _isIndexing 
        ? "Indexation en cours..." 
        : $"{_applications.Count} applications indexées";

    public IReadOnlyList<ApplicationEntry> Applications
    {
        get
        {
            lock (_lock)
            {
                return _applications.ToList();
            }
        }
    }

    public ApplicationIndexer()
    {
        SetMetric("apps_count", 0);
        SetMetric("last_index_time", DateTime.MinValue);
    }

    /// <summary>
    /// Lance l'indexation complète en arrière-plan.
    /// </summary>
    public Task IndexAsync()
    {
        return Task.Run(IndexInternal);
    }

    private void IndexInternal()
    {
        if (_isIndexing) return;

        _isIndexing = true;
        var startTime = DateTime.UtcNow;

        try
        {
            var apps = new List<ApplicationEntry>();

            // 1. Start Menu (utilisateur courant)
            var startMenuUser = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            IndexDirectory(startMenuUser, apps);

            // 2. Start Menu (tous les utilisateurs)
            var startMenuCommon = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            IndexDirectory(startMenuCommon, apps);

            // 3. Program Files
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            IndexDirectory(programFiles, apps, maxDepth: 2);

            // 4. Program Files (x86)
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (Directory.Exists(programFilesX86))
                IndexDirectory(programFilesX86, apps, maxDepth: 2);

            // Mise à jour thread-safe
            lock (_lock)
            {
                _applications.Clear();
                _applications.AddRange(apps.OrderBy(a => a.Name));
            }

            var elapsed = DateTime.UtcNow - startTime;
            SetMetric("apps_count", _applications.Count);
            SetMetric("last_index_time", DateTime.UtcNow);
            SetMetric("index_duration_ms", (int)elapsed.TotalMilliseconds);

            AppLog.Info($"Indexation terminée : {_applications.Count} apps en {elapsed.TotalMilliseconds:F0}ms");
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur indexation applications", ex));
            SetMetric("last_error", ex.Message);
        }
        finally
        {
            _isIndexing = false;
        }
    }


    private void IndexDirectory(string path, List<ApplicationEntry> apps, int maxDepth = 5, int currentDepth = 0)
    {
        if (!Directory.Exists(path) || currentDepth >= maxDepth) return;

        try
        {
            // Rechercher les .lnk et .exe
            foreach (var file in Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".lnk" || ext == ".exe")
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    
                    // Filtrer les noms techniques
                    if (ShouldInclude(name))
                    {
                        apps.Add(new ApplicationEntry
                        {
                            Name = name,
                            Path = file,
                            Type = ext == ".lnk" ? AppType.Shortcut : AppType.Executable
                        });
                    }
                }
            }

            // Récursion
            foreach (var dir in Directory.GetDirectories(path))
            {
                IndexDirectory(dir, apps, maxDepth, currentDepth + 1);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Ignorer les dossiers protégés
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }

    private static bool ShouldInclude(string name)
    {
        var lower = name.ToLowerInvariant();
        
        // Exclure les noms techniques courants
        string[] exclusions = 
        {
            "uninstall", "readme", "license", "help", "documentation",
            "setup", "installer", "update", "crash", "error", "log"
        };

        return !exclusions.Any(ex => lower.Contains(ex));
    }

    /// <summary>
    /// Recherche floue dans les applications indexées.
    /// </summary>
    public IEnumerable<ApplicationEntry> Search(string query, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<ApplicationEntry>();

        var queryLower = query.ToLowerInvariant();

        lock (_lock)
        {
            return _applications
                .Select(app => new
                {
                    App = app,
                    Score = CalculateScore(app.Name, queryLower)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(maxResults)
                .Select(x => x.App)
                .ToList();
        }
    }

    private static int CalculateScore(string appName, string query)
    {
        var nameLower = appName.ToLowerInvariant();

        // Correspondance exacte
        if (nameLower == query) return 1000;

        // Commence par
        if (nameLower.StartsWith(query)) return 500;

        // Contient
        if (nameLower.Contains(query)) return 100;

        // Recherche floue simple (tous les caractères présents dans l'ordre)
        int queryIndex = 0;
        foreach (char c in nameLower)
        {
            if (queryIndex < query.Length && c == query[queryIndex])
                queryIndex++;
        }

        return queryIndex == query.Length ? 50 : 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _applications.Clear();
        }
    }
}

public enum AppType
{
    Executable,
    Shortcut
}

public record ApplicationEntry
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required AppType Type { get; init; }
}

