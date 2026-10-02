## 🎯 WinSpaces v3.2.0 - "Core Stability & Precision Touchpad Enhancement"

### 🚀 Nouveautés
- **Configuration DebugLogging** : Ajout de la propriété `DebugLogging` dans `GlobalConfig` pour activer/désactiver les logs de débogage à la volée sans redémarrer l'application.

### 🛠️ Correctifs & Résolution de bugs
- **PrecisionTouchpadWatcher** : Correction majeure de la méthode `ProcessHidReport` (signature corrigée de 2 à 3 paramètres : `buffer`, `start`, `reportLen`) résolvant l'erreur de compilation CS1501.
- **Gestion mémoire** : Correction de la libération des données *preparsed* HID via `HidD_FreePreparsedData` au lieu de `Marshal.FreeHGlobal`.
- **Pool de buffers** : Refactorisation du pool de buffers pour éviter les allocations répétées sur chaque `WM_INPUT` (amélioration performance).
- **Variables inutilisées** : Nettoyage des variables `WS_EX_TRANSPARENT`, `WS_EX_LAYERED` dans `MenuBarWindow` et `_checkCounter` dans `FullscreenSpaceManager`.
- **Gestion Win32Exception** : Amélioration de la gestion d'erreur pour `RegisterRawInputDevices`.

### ⚙️ Architecture & Technique
- **AppLog** : Ajout de la méthode `SetDebugEnabled(bool)` et propriété `IsDebugEnabled` pour contrôler le niveau de log dynamiquement.
- **VirtualDesktopService** : Amélioration de la détection des APIs COM Windows (support 24H2+, 21H2-23H2, Windows 10) avec meilleur logging.
- **Configuration** : Structure modulaire `WinSpacesConfiguration` avec sections dédiées (Desktops, Spotlight, MenuBar, Widgets, Theme, Gestures, Global).
- **Workflow CI/CD** : Correction du chemin de l'exécutable dans le workflow GitHub Actions pour pointer vers le bon dossier de publication.

### 📦 Build
- **Exécutable auto-contenu** : Publication en `win-x64` self-contained (`.NET 8.0`), prêt à l'emploi sans installation du runtime.
- **Version** : Passage de v3.1.1 à v3.2.0 (SemVer : MINOR pour nouvelles fonctionnalités et corrections significatives).