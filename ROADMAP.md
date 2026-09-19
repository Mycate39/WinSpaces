# WinSpaces — Feuille de Route Architecturale
## Suite d'Intégration macOS pour Windows 11

**Version actuelle :** 2.0.0 (Bureaux virtuels + Dashboard WPF)  
**Vision :** Suite complète d'expérience macOS sur Windows 11  
**Architecture :** WPF + MVVM + .NET 8 + Diagnostic intégré

---

## 📋 Architecture Cible

### Structure des Modules

```
WinSpaces/
├── Core/                          # Noyau commun
│   ├── Services/                  # Services existants (VirtualDesktop, Gestures, etc.)
│   ├── Diagnostics/               # IHealthCheckable + HealthMonitor
│   ├── Native/                    # P/Invoke, hooks Win32
│   └── Configuration/             # AppOptions étendu
│
├── Modules/                       # Nouveaux modules fonctionnels
│   ├── Spotlight/                 # Lanceur rapide (Alt+Espace)
│   │   ├── Services/
│   │   │   ├── SpotlightService.cs          # Orchestrateur principal
│   │   │   ├── ApplicationIndexer.cs        # Indexation apps installées
│   │   │   ├── FileSearchEngine.cs          # Recherche fichiers (MFT/Everything API)
│   │   │   ├── CalculatorEngine.cs          # Évaluateur expressions
│   │   │   └── ActionExecutor.cs            # Exécution commandes système
│   │   ├── Views/
│   │   │   └── SpotlightWindow.xaml         # Fenêtre de recherche
│   │   └── ViewModels/
│   │       └── SpotlightViewModel.cs
│   │
│   ├── MenuBar/                   # Barre de menu supérieure
│   │   ├── Services/
│   │   │   ├── MenuBarService.cs            # Orchestrateur barre menu
│   │   │   ├── SystemMonitor.cs             # Batterie, Wi-Fi, Volume
│   │   │   ├── AppMenuIntegration.cs        # Menus app active (via UI Automation)
│   │   │   └── TopMostWindowManager.cs      # Gestion z-order barre
│   │   ├── Views/
│   │   │   ├── MenuBarWindow.xaml           # Barre horizontale top
│   │   │   └── MenuBarItems/                # Composants individuels (Clock, Battery, etc.)
│   │   └── ViewModels/
│   │       └── MenuBarViewModel.cs
│   │
│   ├── Widgets/                   # Système de widgets
│   │   ├── Core/
│   │   │   ├── WidgetEngine.cs              # Gestionnaire widgets
│   │   │   ├── WidgetBase.cs                # Classe de base pour widgets
│   │   │   └── WidgetRegistry.cs            # Catalogue widgets disponibles
│   │   ├── Built-in/
│   │   │   ├── WeatherWidget/
│   │   │   ├── NotesWidget/
│   │   │   ├── CalendarWidget/
│   │   │   └── PerformanceWidget/
│   │   ├── Views/
│   │   │   └── WidgetContainer.xaml         # Conteneur ancrable
│   │   └── ViewModels/
│   │
│   └── Theming/                   # Moteur de thématisation
│       ├── Services/
│       │   ├── ThemeEngine.cs               # Orchestrateur thèmes
│       │   ├── WallpaperAnalyzer.cs         # Extraction couleurs dominantes
│       │   ├── AccentColorManager.cs        # Gestion couleurs système
│       │   └── EffectsRenderer.cs           # Mica, Acrylic, Blur
│       ├── Themes/
│       │   ├── DarkTheme.xaml
│       │   ├── LightTheme.xaml
│       │   └── AdaptiveTheme.xaml
│       └── ViewModels/
│           └── ThemeViewModel.cs
```

---


## 🎯 Phases de Développement

### **Phase 1 : Fondations Extensibles (v2.1.0)** — 2 semaines
*Préparer l'architecture pour les nouveaux modules*

#### Objectifs :
- ✅ Créer une structure de modules découplés
- ✅ Étendre `IHealthCheckable` avec métriques de performance
- ✅ Système de configuration modulaire
- ✅ Service de gestion des raccourcis globaux étendu

#### Tâches :
1. **Refactoring Configuration**
   - Créer `ModuleConfiguration` pour chaque module
   - Implémenter `ConfigurationService` avec persistance JSON
   - Ajouter hot-reload des configurations

2. **Extension Diagnostics**
   ```csharp
   public interface IHealthCheckable
   {
       string ComponentName { get; }
       bool IsHealthy { get; }
       string StatusMessage { get; }
       
       // Nouveaux membres v2.1
       HealthStatus DetailedStatus { get; }
       Dictionary<string, object> Metrics { get; }
       DateTime LastCheck { get; }
   }
   
   public enum HealthStatus { Healthy, Degraded, Critical, Unknown }
   ```

3. **Service de Raccourcis Global**
   - Créer `GlobalHotkeyManager` (remplacer `HotkeyService`)
   - Support multi-modules avec priorités
   - Configuration dynamique des raccourcis

4. **Service de Rendu WPF Optimisé**
   - `WindowCompositionService` pour effets Mica/Acrylic
   - Gestion hardware acceleration
   - Cache de ressources visuelles

**Livrables :**
- Architecture modulaire prête
- Documentation API interne
- Tests unitaires infrastructure

---

### **Phase 2 : Spotlight Avancé (v2.2.0)** — 3 semaines
*Lanceur rapide type macOS Spotlight*

#### Spécifications :
- **Raccourci :** `Alt+Espace` (configurable)
- **UI :** Fenêtre centrale 600x80px (extensible à 600x400px avec résultats)
- **Indexation :**
  - Applications (Start Menu, Program Files)
  - Fichiers récents (MFT sur Windows)
  - Historique navigateurs (optionnel, via SQLite)
- **Fonctionnalités :**
  - Recherche floue (Levenshtein distance)
  - Calculs mathématiques (ex: `2+2`, `sqrt(16)`)
  - Conversions (ex: `10 km to miles`)
  - Commandes système (`shutdown`, `restart`, `lock`)

**Livrables :**
- Spotlight fonctionnel avec indexation basique
- UI fluide avec animations (fade-in/out)
- Tests de performance (< 50ms pour recherche)

---

### **Phase 3-5 : Menu Bar, Widgets, Thèmes** — 10 semaines
*Voir détails complets dans la documentation étendue*

---

## 🚀 Roadmap Temporelle

| Phase | Version | Durée | Jalons |
|-------|---------|-------|--------|
| **Phase 1** | v2.1.0 | 2 semaines | Architecture modulaire prête |
| **Phase 2** | v2.2.0 | 3 semaines | Spotlight fonctionnel |
| **Phase 3** | v2.3.0 | 4 semaines | Menu Bar opérationnel |
| **Phase 4** | v2.4.0 | 3 semaines | 4 widgets disponibles |
| **Phase 5** | v2.5.0 | 3 semaines | Thèmes dynamiques actifs |
| **Total** | | **15 semaines** (~4 mois) | Suite complète |

---

## 📦 Dépendances Externes

### NuGet Packages à Ajouter :
```xml
<!-- Spotlight -->
<PackageReference Include="Lucene.Net" Version="4.8.0" />
<PackageReference Include="NCalc" Version="3.7.2" />

<!-- Widgets -->
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="Microsoft.Graph" Version="5.x" />

<!-- Thèmes -->
<PackageReference Include="System.Drawing.Common" Version="8.0.0" />
```

---

## 🎨 Design Principles

1. **Consistance macOS :** Reproduire fidèlement l'UX macOS (animations, espacements, polices)
2. **Performance First :** Aucun module ne doit impacter négativement les bureaux virtuels
3. **Modularité :** Chaque module peut être désactivé indépendamment
4. **Diagnostic Omniprésent :** Tout est monitoré via `IHealthCheckable`
5. **Configuration Simple :** UI de configuration dans Dashboard, pas de fichiers XML

---

## 📊 Prochaine Étape Immédiate

**Créer la structure de Phase 1 (v2.1.0) :**
1. Dossier `Modules/` avec sous-dossiers vides
2. Refactoring `AppOptions` → `WinSpacesConfiguration`
3. Extension `IHealthCheckable` avec métriques
4. `ConfigurationService` avec persistance JSON
5. `GlobalHotkeyManager` étendu
