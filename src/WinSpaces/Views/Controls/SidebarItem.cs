using System.Windows;
using System.Windows.Controls;

namespace WinSpaces.Views.Controls;

/// <summary>
/// Élément de barre latérale façon Finder / Réglages macOS : icône, titre et
/// pastille de compteur optionnelle. Hérite de RadioButton (sélection unique).
/// </summary>
public sealed class SidebarItem : System.Windows.Controls.RadioButton
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(SidebarItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BadgeProperty = DependencyProperty.Register(
        nameof(Badge), typeof(int), typeof(SidebarItem), new PropertyMetadata(0));

    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(SidebarItem), new PropertyMetadata(string.Empty));

    /// <summary>Glyphe Segoe Fluent Icons / MDL2.</summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>Compteur affiché en pastille (masqué à 0).</summary>
    public int Badge
    {
        get => (int)GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <summary>Texte secondaire aligné à droite (taille de fichier…).</summary>
    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }
}
