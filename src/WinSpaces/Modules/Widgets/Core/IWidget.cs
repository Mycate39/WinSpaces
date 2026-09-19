namespace WinSpaces.Modules.Widgets.Core;

/// <summary>
/// Interface définissant le contrat d'un widget.
/// Tous les widgets intégrés ou personnalisés doivent implémenter cette interface.
/// </summary>
public interface IWidget
{
    /// <summary>
    /// Identifiant unique du widget.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Nom d'affichage du widget.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description courte du widget.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Largeur du widget en pixels.
    /// </summary>
    double Width { get; }

    /// <summary>
    /// Hauteur du widget en pixels.
    /// </summary>
    double Height { get; }

    /// <summary>
    /// Position X sur le bureau.
    /// </summary>
    double X { get; set; }

    /// <summary>
    /// Position Y sur le bureau.
    /// </summary>
    double Y { get; set; }

    /// <summary>
    /// Indique si le widget est actuellement visible.
    /// </summary>
    bool IsVisible { get; set; }

    /// <summary>
    /// Indique si le widget est épinglé (toujours au premier plan).
    /// </summary>
    bool IsPinned { get; set; }

    /// <summary>
    /// Opacité du widget (0.0 à 1.0).
    /// </summary>
    double Opacity { get; set; }

    /// <summary>
    /// Données de configuration spécifiques au widget (sérialisable en JSON).
    /// </summary>
    Dictionary<string, object> Configuration { get; }

    /// <summary>
    /// Initialise le widget (chargement données, abonnements événements).
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Rafraîchit les données du widget.
    /// </summary>
    Task RefreshAsync();

    /// <summary>
    /// Libère les ressources du widget.
    /// </summary>
    void Dispose();
}
