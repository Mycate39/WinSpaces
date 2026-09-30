namespace WinSpaces.Configuration;

/// <summary>
/// Configuration globale de WinSpaces avec support modulaire.
/// Remplace l'ancien AppOptions avec une structure extensible.
/// </summary>
public class WinSpacesConfiguration
{
    /// <summary>Configuration du module Bureaux Virtuels (existant).</summary>
    public DesktopsConfig Desktops { get; set; } = new();

    /// <summary>Configuration du module Spotlight (lanceur rapide).</summary>
    public SpotlightConfig Spotlight { get; set; } = new();

    /// <summary>Configuration du module Menu Bar.</summary>
    public MenuBarConfig MenuBar { get; set; } = new();

    /// <summary>Configuration du module Widgets.</summary>
    public WidgetsConfig Widgets { get; set; } = new();

    /// <summary>Configuration du module Thématisation.</summary>
    public ThemeConfig Theme { get; set; } = new();

    /// <summary>Configuration des gestes (trackpad / souris).</summary>
    public GestureConfig Gestures { get; set; } = new();

    /// <summary>Configuration globale de l'application.</summary>
    public GlobalConfig Global { get; set; } = new();
}

/// <summary>
/// Configuration du module Bureaux Virtuels.
/// </summary>
public class DesktopsConfig
{
    public bool FullscreenSpacesEnabled { get; set; } = true;
    public bool GesturesEnabled { get; set; } = true;
    public bool HotkeysEnabled { get; set; } = true;
}

/// <summary>
/// Configuration du module Spotlight.
/// </summary>
public class SpotlightConfig
{
    public bool Enabled { get; set; } = true;
    public string HotkeyModifiers { get; set; } = "Alt";
    public string HotkeyKey { get; set; } = "Space";
    public int MaxResults { get; set; } = 10;
    public bool IndexApplications { get; set; } = true;
    public bool IndexFiles { get; set; } = true;
    public bool EnableCalculator { get; set; } = true;
    public bool EnableWebSearch { get; set; } = true;
    public string WebSearchEngine { get; set; } = "https://www.google.com/search?q={0}";
}

/// <summary>
/// Configuration du module Menu Bar.
/// </summary>
public class MenuBarConfig
{
    public bool Enabled { get; set; } = false; // Désactivé par défaut (nouvelle fonctionnalité)
    public bool AutoHide { get; set; } = false;
    public bool ShowClock { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    public bool ShowWifi { get; set; } = true;
    public bool ShowVolume { get; set; } = true;
    public int Height { get; set; } = 28;
}

/// <summary>
/// Configuration du module Widgets.
/// </summary>
public class WidgetsConfig
{
    public bool Enabled { get; set; } = false; // Désactivé par défaut
    public List<WidgetInstanceConfig> ActiveWidgets { get; set; } = new();
}

public class WidgetInstanceConfig
{
    public string WidgetId { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
}

/// <summary>
/// Configuration du module Thématisation.
/// </summary>
public class ThemeConfig
{
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = "Auto"; // "Light", "Dark", "Auto"
    public bool AdaptToWallpaper { get; set; } = true;
    public bool EnableMicaEffect { get; set; } = true;
    public bool EnableAcrylicEffect { get; set; } = true;
}

/// <summary>
/// Configuration des gestes (trackpad / souris).
/// </summary>
public class GestureConfig
{
    /// <summary>Active la détection des gestes trackpad.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Nombre minimal de doigts pour déclencher un swipe (Précision Touchpad).</summary>
    public int MinContactsForSwipe { get; set; } = 3;

    /// <summary>Seuil de delta cumulé pour déclencher le swipe (Précision Touchpad).</summary>
    public int SwipeDeltaThreshold { get; set; } = 800;

    /// <summary>Seuil de delta pour déclencher le fling (Momentum / molette).</summary>
    public int MomentumTriggerThreshold { get; set; } = 150;

    /// <summary>Fenêtre de décroissance du momentum (ms).</summary>
    public int MomentumDecayWindowMs { get; set; } = 500;

    /// <summary>Anti-rebond entre deux bascules (ms).</summary>
    public int MomentumCooldownMs { get; set; } = 350;

    /// <summary>Timeout avant qu'un contact soit considéré comme obsolète (ms).</summary>
    public int ContactStaleTimeoutMs { get; set; } = 120;

    /// <summary>Timeout avant nouvelle session de geste (ms).</summary>
    public int NewSessionTimeoutMs { get; set; } = 200;
}

/// <summary>
/// Configuration globale de l'application.
/// </summary>
public class GlobalConfig
{
    public bool AutostartEnabled { get; set; } = false;
    public string Language { get; set; } = "fr-FR";
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool DebugLogging { get; set; } = false;
}
