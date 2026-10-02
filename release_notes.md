## 🎯 WinSpaces v3.3.0 - "macOS-Style Gestures & Desktop Switch"

### 🚀 Nouveautés - Gestes style macOS
- **3 doigts horizontal** : Switch entre espaces (équivalent macOS Spaces)
- **4 doigts horizontal** : Switch entre bureaux (différencié du 3 doigts, optionnel via config `DifferentiateThreeFourFingers`)
- **3 doigts vers le haut** : Mission Control → Affiche le Dashboard WinSpaces (configurable via `ThreeFingerUpAction`)
- **3 doigts vers le bas** : App Exposé → Affiche le Dashboard (configurable via `ThreeFingerDownAction`)
- **Gestes verticaux activables/désactivables** via `VerticalGesturesEnabled`
- **Seuils configurables** : `VerticalSwipeDeltaThreshold` pour la sensibilité verticale

### 🛠️ Correctifs & Résolution de bugs
- **ThemeEngine** : Correction du changement de thème pour préserver les styles de base (Styles.xaml) lors du basculement entre Light/Dark. Le `Clear()` a été remplacé par une suppression sélective des dictionnaires de thème uniquement.
- **WinSpacesApplicationContext** : Réorganisation des champs privés pour un ordre plus logique (services de configuration d'abord, puis services fonctionnels, puis UI).

### ⚙️ Architecture & Technique
- **ThemeEngine** : Utilisation de `System.Linq` pour filtrer et supprimer uniquement les thèmes LightTheme.xaml et DarkTheme.xaml, conservant les ressources communes.
- **WinSpacesApplicationContext** : Déplacement de `ConfigurationService` avant les services qui en dépendent pour clarifier l'ordre d'initialisation.
- **GestureManager** : Nouveaux événements `DesktopSwitchRequested`, `MissionControlRequested`, `AppExposeRequested`
- **PrecisionTouchpadWatcher** : Suivi du mouvement vertical (Y), détection 3 vs 4 doigts, événements verticaux et actions 3 doigts

### 📦 Build
- **Version** : Passage de v3.2.1 à v3.3.0 (SemVer : MINOR pour nouvelles fonctionnalités majeures - gestes macOS).