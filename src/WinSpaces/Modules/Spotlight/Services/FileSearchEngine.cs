using System.Diagnostics;
using System.IO;
using WinSpaces.Diagnostics;
using WinSpaces.Services;

namespace WinSpaces.Modules.Spotlight.Services;

/// <summary>
/// Moteur de recherche de fichiers rapide.
/// Utilise les chemins courants (Desktop, Documents, Downloads) pour la recherche initiale.
/// </summary>
public sealed class FileSearchEngine : HealthCheckableBase
{
    private readonly string[] _searchPaths;

    public override string ComponentName => "File Search Engine";
    public override bool IsHealthy => true;
    public override string StatusMessage => $"Recherche active sur {_searchPaths.Length} dossiers";

    public FileSearchEngine()
    {
        _searchPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        SetMetric("search_paths_count", _searchPaths.Length);
    }

    /// <summary>
    /// Recherche des fichiers par nom (recherche rapide limitée).
    /// </summary>
    public IEnumerable<FileEntry> Search(string query, int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return Enumerable.Empty<FileEntry>();

        var results = new List<FileEntry>();
        var queryLower = query.ToLowerInvariant();
        var sw = Stopwatch.StartNew();

        try
        {
            foreach (var basePath in _searchPaths)
            {
                if (!Directory.Exists(basePath)) continue;

                try
                {
                    var files = Directory.GetFiles(basePath, $"*{query}*", SearchOption.TopDirectoryOnly)
                        .Take(maxResults - results.Count);

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        results.Add(new FileEntry
                        {
                            Name = fileInfo.Name,
                            Path = fileInfo.FullName,
                            SizeBytes = fileInfo.Length,
                            ModifiedDate = fileInfo.LastWriteTime
                        });

                        if (results.Count >= maxResults) break;
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception ex) { AppLog.Error(ex); }

                if (results.Count >= maxResults) break;
            }

            sw.Stop();
            SetMetric("last_search_ms", (int)sw.ElapsedMilliseconds);
            SetMetric("last_results_count", results.Count);
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur recherche fichiers", ex));
        }

        return results;
    }
}

public record FileEntry
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime ModifiedDate { get; init; }
}
