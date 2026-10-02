## 🎯 WinSpaces v3.2.1 - "Theme Stability & Code Organization"

### 🛠️ Correctifs & Résolution de bugs
- **ThemeEngine** : Correction du changement de thème pour préserver les styles de base (Styles.xaml) lors du basculement entre Light/Dark. Le `Clear()` a été remplacé par une suppression sélective des dictionnaires de thème uniquement.
- **WinSpacesApplicationContext** : Réorganisation des champs privés pour un ordre plus logique (services de configuration d'abord, puis services fonctionnels, puis UI).

### ⚙️ Architecture & Technique
- **ThemeEngine** : Utilisation de `System.Linq` pour filtrer et supprimer uniquement les thèmes LightTheme.xaml et DarkTheme.xaml, conservant les ressources communes.
- **WinSpacesApplicationContext** : Déplacement de `ConfigurationService` avant les services qui en dépendent pour clarifier l'ordre d'initialisation.

### 📦 Build
- **Version** : Passage de v3.2.0 à v3.2.1 (SemVer : PATCH pour corrections de bugs et améliorations internes).