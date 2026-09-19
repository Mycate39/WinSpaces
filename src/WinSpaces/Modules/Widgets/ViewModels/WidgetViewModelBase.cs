using WinSpaces.Modules.Widgets.Core;
using WinSpaces.ViewModels;

namespace WinSpaces.Modules.Widgets.ViewModels;

/// <summary>
/// Classe de base pour les ViewModels des widgets.
/// Gère la mise à jour des données et les interactions.
/// </summary>
public abstract class WidgetViewModelBase : ViewModelBase, IDisposable
{
    private readonly IWidget _widget;
    private bool _disposed;

    public IWidget Widget => _widget;

    public string Name => _widget.Name;
    public double X => _widget.X;
    public double Y => _widget.Y;
    public bool IsVisible => _widget.IsVisible;
    public double Opacity => _widget.Opacity;

    protected WidgetViewModelBase(IWidget widget)
    {
        _widget = widget;
    }

    /// <summary>
    /// Rafraîchit les données affichées par le widget.
    /// </summary>
    public virtual async Task RefreshDataAsync()
    {
        await _widget.RefreshAsync();
        NotifyAllPropertiesChanged();
    }

    /// <summary>
    /// Notifie tous les changements de propriétés.
    /// </summary>
    protected void NotifyAllPropertiesChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Opacity));
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
