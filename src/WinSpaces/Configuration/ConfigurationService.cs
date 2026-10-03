using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WinSpaces.Diagnostics;
using WinSpaces.Services;

namespace WinSpaces.Configuration;

/// <summary>
/// Service de gestion centralisée de la configuration WinSpaces.
/// Gère la persistance JSON, le hot-reload et les notifications de changement.
/// </summary>
public sealed class ConfigurationService : HealthCheckableBase, IDisposable, IAsyncDisposable
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinSpaces"
    );
    
    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");
    
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly FileSystemWatcher? _watcher;

    // Le FileSystemWatcher notifie sur un thread du pool, souvent plusieurs fois
    // par sauvegarde et pendant que l'éditeur écrit encore. On rebascule sur le
    // thread UI (les abonnés — gestes, UI — n'y sont pas thread-safe) avec un
    // anti-rebond avant de relire le fichier.
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _reloadDebounce;
    private WinSpacesConfiguration _configuration;
    private bool _disposed;

    public override string ComponentName => "Configuration Service";
    
    public override bool IsHealthy => File.Exists(ConfigFilePath);
    
    public override string StatusMessage => IsHealthy 
        ? $"Configuration chargée depuis {Path.GetFileName(ConfigFilePath)}" 
        : "Configuration par défaut (fichier non trouvé)";

    public override HealthStatus DetailedStatus => IsHealthy ? HealthStatus.Healthy : HealthStatus.Degraded;

    /// <summary>Configuration actuelle (lecture seule publique).</summary>
    public WinSpacesConfiguration Current => _configuration;

    /// <summary>Événement déclenché lorsque la configuration change (hot-reload).</summary>
    public event EventHandler<WinSpacesConfiguration>? ConfigurationChanged;

    public ConfigurationService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _reloadDebounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _reloadDebounce.Tick += OnReloadDebounceTick;

        _configuration = LoadConfiguration();
        
        SetMetric("config_file_path", ConfigFilePath);
        SetMetric("modules_count", 5);
        
        if (IsHealthy)
        {
            try
            {
                _watcher = new FileSystemWatcher(ConfigDirectory, "config.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                _watcher.Changed += OnConfigFileChanged;
                _watcher.EnableRaisingEvents = true;
                
                SetMetric("hot_reload_enabled", true);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex);
                SetMetric("hot_reload_enabled", false);
            }
        }
    }



    private WinSpacesConfiguration LoadConfiguration()
    {
        _semaphore.Wait();
        try
        {
            return LoadConfigurationInternal();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<WinSpacesConfiguration> LoadConfigurationAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await LoadConfigurationInternalAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private WinSpacesConfiguration LoadConfigurationInternal()
    {
        try
        {
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);

            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<WinSpacesConfiguration>(json, GetJsonOptions());
                
                if (config != null)
                {
                    AppLog.Info($"Configuration chargée depuis {ConfigFilePath}");
                    SetMetric("load_source", "file");
                    return config;
                }
            }

            var defaultConfig = new WinSpacesConfiguration();
            SaveConfigurationInternal(defaultConfig);
            SetMetric("load_source", "default");
            return defaultConfig;
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur chargement configuration", ex));
            SetMetric("load_error", ex.Message);
            return new WinSpacesConfiguration();
        }
    }

    private async Task<WinSpacesConfiguration> LoadConfigurationInternalAsync()
    {
        try
        {
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);

            if (File.Exists(ConfigFilePath))
            {
                var json = await File.ReadAllTextAsync(ConfigFilePath);
                var config = JsonSerializer.Deserialize<WinSpacesConfiguration>(json, GetJsonOptions());
                
                if (config != null)
                {
                    AppLog.Info($"Configuration chargée depuis {ConfigFilePath}");
                    SetMetric("load_source", "file");
                    return config;
                }
            }

            var defaultConfig = new WinSpacesConfiguration();
            SaveConfigurationInternal(defaultConfig);
            SetMetric("load_source", "default");
            return defaultConfig;
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur chargement configuration", ex));
            SetMetric("load_error", ex.Message);
            return new WinSpacesConfiguration();
        }
    }

    public void SaveConfiguration()
    {
        _semaphore.Wait();
        try
        {
            SaveConfigurationInternal(_configuration);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void SaveConfigurationInternal(WinSpacesConfiguration config)
    {
        try
        {
            if (_watcher != null)
                _watcher.EnableRaisingEvents = false;

            var json = JsonSerializer.Serialize(config, GetJsonOptions());
            File.WriteAllText(ConfigFilePath, json);
            
            SetMetric("last_save", DateTime.UtcNow);
            AppLog.Info("Configuration sauvegardée");
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur sauvegarde configuration", ex));
            SetMetric("save_error", ex.Message);
        }
        finally
        {
            if (_watcher != null)
                _watcher.EnableRaisingEvents = true;
        }
    }

    public void UpdateConfiguration(WinSpacesConfiguration newConfig)
    {
        _configuration = newConfig;
        SaveConfiguration();
        ConfigurationChanged?.Invoke(this, _configuration);
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;
        // Thread du pool : on se contente de (re)lancer l'anti-rebond sur le thread UI.
        _dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            _reloadDebounce.Stop();
            _reloadDebounce.Start();
        });
    }

    private void OnReloadDebounceTick(object? sender, EventArgs e)
    {
        _reloadDebounce.Stop();
        if (_disposed) return;

        // Sauvegarde en cours : on réessaie plus tard au lieu d'ignorer le changement.
        if (!_semaphore.Wait(0))
        {
            _reloadDebounce.Start();
            return;
        }

        WinSpacesConfiguration? newConfig;
        try
        {
            newConfig = TryReadConfigurationFile();
            if (newConfig is null) return;
            _configuration = newConfig;

            SetMetric("reload_count", (int)Metrics.GetValueOrDefault("reload_count", 0) + 1);
            SetMetric("last_reload", DateTime.UtcNow);
        }
        finally
        {
            _semaphore.Release();
        }

        // Levé HORS du sémaphore : SemaphoreSlim n'est pas réentrant, un abonné
        // appelant SaveConfiguration()/UpdateConfiguration() se bloquait à vie.
        try
        {
            ConfigurationChanged?.Invoke(this, newConfig);
            AppLog.Info("Configuration rechargée (hot-reload)");
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur hot-reload configuration", ex));
        }
    }

    /// <summary>
    /// Relit le fichier pour le hot-reload. Contrairement au chargement initial,
    /// un fichier illisible (verrouillé, JSON partiel pendant l'écriture) ne
    /// remplace PAS la configuration courante par les valeurs par défaut.
    /// </summary>
    private WinSpacesConfiguration? TryReadConfigurationFile()
    {
        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<WinSpacesConfiguration>(json, GetJsonOptions());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            AppLog.Warning($"Hot-reload ignoré, configuration courante conservée : {ex.Message}");
            SetMetric("load_error", ex.Message);
            return null;
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _reloadDebounce.Stop();
        _reloadDebounce.Tick -= OnReloadDebounceTick;

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnConfigFileChanged;
            _watcher.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
