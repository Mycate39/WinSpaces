using System.IO;
using System.Text.Json;
using WinSpaces.Diagnostics;
using WinSpaces.Services;

namespace WinSpaces.Modules.Widgets.Core;

/// <summary>
/// Gestionnaire central des widgets.
/// Gère le chargement, l'enregistrement, la persistance des positions et le cycle de vie.
/// </summary>
public sealed class WidgetManager : HealthCheckableBase, IDisposable
{
    private readonly Dictionary<string, IWidget> _activeWidgets = new();
    private readonly Dictionary<string, Func<IWidget>> _registeredWidgetFactories = new();
    private readonly string _configPath;
    private bool _disposed;

    public override string ComponentName => "Widget Manager";
    public override bool IsHealthy => true;
    public override string StatusMessage => $"{_activeWidgets.Count} widgets actifs";

    public IReadOnlyDictionary<string, IWidget> ActiveWidgets => _activeWidgets;

    public event EventHandler<IWidget>? WidgetAdded;
    public event EventHandler<IWidget>? WidgetRemoved;

    public WidgetManager()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var winSpacesPath = Path.Combine(appDataPath, "WinSpaces");
        Directory.CreateDirectory(winSpacesPath);
        _configPath = Path.Combine(winSpacesPath, "widgets.json");

        SetMetric("active_widgets_count", 0);
        SetMetric("registered_types_count", 0);
        SetMetric("config_path", _configPath);
    }

    /// <summary>
    /// Enregistre un type de widget disponible.
    /// </summary>
    public void RegisterWidgetType(string typeId, Func<IWidget> factory)
    {
        _registeredWidgetFactories[typeId] = factory;
        SetMetric("registered_types_count", _registeredWidgetFactories.Count);
        AppLog.Info($"WidgetManager : Type '{typeId}' enregistré");
    }

    /// <summary>
    /// Initialise le gestionnaire et charge les widgets sauvegardés.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await LoadWidgetsFromConfigAsync();
            AppLog.Info($"WidgetManager : Initialisé avec {_activeWidgets.Count} widgets");
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur initialisation WidgetManager", ex));
        }
    }
    public void InitializeAndLoadWidgets()
    {
        InitializeAsync().GetAwaiter().GetResult();
    }


    /// <summary>
    /// Crée et active un nouveau widget.
    /// </summary>
    public async Task<IWidget?> CreateWidgetAsync(string typeId)
    {
        if (!_registeredWidgetFactories.TryGetValue(typeId, out var factory))
        {
            AppLog.Warning($"WidgetManager : Type '{typeId}' non enregistré");
            return null;
        }

        try
        {
            var widget = factory();
            await widget.InitializeAsync();

            _activeWidgets[widget.Id] = widget;
            SetMetric("active_widgets_count", _activeWidgets.Count);

            WidgetAdded?.Invoke(this, widget);
            AppLog.Info($"WidgetManager : Widget '{widget.Name}' créé");

            await SaveWidgetsToConfigAsync();
            return widget;
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException($"Erreur création widget '{typeId}'", ex));
            return null;
        }
    }

    /// <summary>
    /// Supprime un widget actif.
    /// </summary>
    public async Task RemoveWidgetAsync(string widgetId)
    {
        if (!_activeWidgets.TryGetValue(widgetId, out var widget))
            return;

        try
        {
            widget.Dispose();
            _activeWidgets.Remove(widgetId);
            SetMetric("active_widgets_count", _activeWidgets.Count);

            WidgetRemoved?.Invoke(this, widget);
            AppLog.Info($"WidgetManager : Widget '{widget.Name}' supprimé");

            await SaveWidgetsToConfigAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException($"Erreur suppression widget '{widgetId}'", ex));
        }
    }

    /// <summary>
    /// Sauvegarde la configuration de tous les widgets actifs.
    /// </summary>
    public async Task SaveWidgetsToConfigAsync()
    {
        try
        {
            var config = _activeWidgets.Values.Select(w => new
            {
                TypeId = w.GetType().Name,
                w.Id,
                w.X,
                w.Y,
                w.IsVisible,
                w.IsPinned,
                w.Opacity,
                w.Configuration
            }).ToList();

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);

            SetMetric("last_save", DateTime.UtcNow);
            AppLog.Info("WidgetManager : Configuration sauvegardée");
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur sauvegarde configuration widgets", ex));
        }
    }

    /// <summary>
    /// Charge les widgets depuis la configuration sauvegardée.
    /// </summary>
    private async Task LoadWidgetsFromConfigAsync()
    {
        if (!File.Exists(_configPath))
        {
            AppLog.Info("WidgetManager : Aucune configuration trouvée, démarrage à vide");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configPath);
            var configs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);

            if (configs == null) return;

            foreach (var config in configs)
            {
                if (!config.TryGetValue("TypeId", out var typeIdElement))
                    continue;

                var typeId = typeIdElement.GetString();
                if (typeId == null || !_registeredWidgetFactories.ContainsKey(typeId))
                    continue;

                var widget = await CreateWidgetAsync(typeId);
                if (widget == null) continue;

                // Restaurer position et propriétés
                if (config.TryGetValue("X", out var x)) widget.X = x.GetDouble();
                if (config.TryGetValue("Y", out var y)) widget.Y = y.GetDouble();
                if (config.TryGetValue("IsVisible", out var vis)) widget.IsVisible = vis.GetBoolean();
                if (config.TryGetValue("IsPinned", out var pin)) widget.IsPinned = pin.GetBoolean();
                if (config.TryGetValue("Opacity", out var opa)) widget.Opacity = opa.GetDouble();
            }

            SetMetric("last_load", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException("Erreur chargement configuration widgets", ex));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var widget in _activeWidgets.Values)
        {
            widget.Dispose();
        }

        _activeWidgets.Clear();
        AppLog.Info("WidgetManager : Dispose completed");
    }
    /// <summary>
    /// Sauvegarde l'état et ferme tous les widgets proprement.
    /// </summary>
    public void SaveStateAndCloseAll()
    {
        SaveWidgetsToConfigAsync().GetAwaiter().GetResult();
        Dispose();
    }

}

