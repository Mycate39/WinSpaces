using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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

    private WinSpacesConfiguration LoadConfigurationInternalSync()
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
        // Pas de besoin de Wait() ici car c'est un rechargement background, 
        // mais on doit s'assurer de ne pas bloquer si un autre thread écrit.
        if (!_semaphore.Wait(0)) return; 
        
        try
        {
            var newConfig = LoadConfigurationInternal();
            _configuration = newConfig;
            
            SetMetric("reload_count", (int)(Metrics.GetValueOrDefault("reload_count", 0)) + 1);
            SetMetric("last_reload", DateTime.UtcNow);
            
            ConfigurationChanged?.Invoke(this, _configuration);
            AppLog.Info("Configuration rechargée (hot-reload)");
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur hot-reload configuration", ex));
        }
        finally
        {
            _semaphore.Release();
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
        
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
