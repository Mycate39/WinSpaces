using System.Windows.Threading;

namespace WinSpaces.Modules.Widgets.Core;

/// <summary>
/// Classe de base abstraite pour tous les widgets.
/// Facilite l'implémentation de IWidget avec gestion automatique des propriétés communes.
/// </summary>
public abstract class WidgetBase : IWidget
{
    protected readonly DispatcherTimer RefreshTimer;
    private bool _disposed;

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract double Width { get; }
    public abstract double Height { get; }

    public double X { get; set; } = 100;
    public double Y { get; set; } = 100;
    public bool IsVisible { get; set; } = true;
    public bool IsPinned { get; set; } = false;
    public double Opacity { get; set; } = 0.95;

    public Dictionary<string, object> Configuration { get; } = new();

    protected WidgetBase(TimeSpan refreshInterval)
    {
        RefreshTimer = new DispatcherTimer
        {
            Interval = refreshInterval
        };
        RefreshTimer.Tick += async (s, e) => await RefreshAsync();
    }

    public virtual async Task InitializeAsync()
    {
        await RefreshAsync();
        RefreshTimer.Start();
        AppLog.Info($"Widget '{Name}' : Initialisé");
    }

    public abstract Task RefreshAsync();

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        RefreshTimer.Stop();
        RefreshTimer.Tick -= async (s, e) => await RefreshAsync();
        AppLog.Info($"Widget '{Name}' : Dispose completed");
    }
}
