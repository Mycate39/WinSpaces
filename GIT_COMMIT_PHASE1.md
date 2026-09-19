# Phase 1 : Guide de Commit Git

## Fichiers à committer

### Nouveaux fichiers (7)
```bash
git add src/WinSpaces/Diagnostics/HealthStatus.cs
git add src/WinSpaces/Diagnostics/HealthCheckableBase.cs
git add src/WinSpaces/Diagnostics/HealthCheckableExtensions.cs
git add src/WinSpaces/Configuration/WinSpacesConfiguration.cs
git add src/WinSpaces/Configuration/ConfigurationService.cs
git add src/WinSpaces/Services/GlobalHotkeyManager.cs
git add ROADMAP.md PHASE1_COMPLETE.md
```

### Fichiers modifiés (1)
```bash
git add src/WinSpaces/Diagnostics/IHealthCheckable.cs
```

### Structure Modules/ (à ajouter)
```bash
git add src/WinSpaces/Modules/
```

## Commandes de commit suggérées

```bash
# Option A : Un seul commit pour toute la Phase 1
git commit -m "feat: Phase 1 v2.1.0 - Fondations Extensibles

- ✨ Nouvelle architecture modulaire (Spotlight, MenuBar, Widgets, Theming)
- ✨ IHealthCheckable étendu avec métriques (DetailedStatus, Metrics, LastCheck)
- ✨ ConfigurationService avec persistance JSON et hot-reload
- ✨ GlobalHotkeyManager pour enregistrement dynamique multi-modules
- 📝 Documentation complète (ROADMAP.md, PHASE1_COMPLETE.md)

Composants créés:
- Diagnostics: HealthStatus, HealthCheckableBase, HealthCheckableExtensions
- Configuration: WinSpacesConfiguration, ConfigurationService
- Services: GlobalHotkeyManager
- Structure: Modules/{Spotlight,MenuBar,Widgets,Theming}

Prochaine étape: Phase 2 - Module Spotlight v2.2.0"

# Option B : Commits séparés par domaine (recommandé)

# 1. Système de diagnostic étendu
git add src/WinSpaces/Diagnostics/
git commit -m "feat(diagnostics): Étendre IHealthCheckable avec métriques v2.1

- Ajout HealthStatus enum (Healthy, Degraded, Critical, Unknown)
- Ajout DetailedStatus, Metrics, LastCheck à IHealthCheckable
- Nouvelle classe HealthCheckableBase pour faciliter l'implémentation
- HealthCheckableExtensions pour rétrocompatibilité services legacy

Impacte: Prépare le terrain pour monitoring détaillé des modules"

# 2. Configuration centralisée
git add src/WinSpaces/Configuration/
git commit -m "feat(config): ConfigurationService avec hot-reload

- Nouvelle structure WinSpacesConfiguration modulaire
- ConfigurationService avec persistance JSON
- Hot-reload automatique via FileSystemWatcher
- Événement ConfigurationChanged pour notifications temps réel
- Support 5 modules: Desktops, Spotlight, MenuBar, Widgets, Theme

Fichier config: %AppData%/WinSpaces/config.json"

# 3. Gestionnaire de raccourcis global
git add src/WinSpaces/Services/GlobalHotkeyManager.cs
git commit -m "feat(hotkeys): GlobalHotkeyManager pour enregistrement dynamique

- Remplace HotkeyService avec support multi-modules
- API Register/Unregister pour gestion cycle de vie
- Métriques intégrées (registered_count, etc.)
- Hérite de HealthCheckableBase pour diagnostic

Permettra aux modules Spotlight, MenuBar, etc. d'enregistrer leurs raccourcis"

# 4. Structure modulaire
git add src/WinSpaces/Modules/
git commit -m "feat(modules): Structure dossiers pour 4 nouveaux modules

- Modules/Spotlight: Lanceur rapide style macOS
- Modules/MenuBar: Barre de menu système
- Modules/Widgets: Système de widgets desktop
- Modules/Theming: Thématisation dynamique

Dossiers vides prêts pour Phase 2+"

# 5. Documentation
git add ROADMAP.md PHASE1_COMPLETE.md
git commit -m "docs: ROADMAP et résumé Phase 1 complète

- ROADMAP.md: Plan 15 semaines, 5 phases de développement
- PHASE1_COMPLETE.md: Récapitulatif composants créés et métriques

Phase 1 terminée : Fondations Extensibles v2.1.0 ✅
Prochaine étape : Phase 2 Spotlight v2.2.0"
```

## Vérification avant commit

```bash
# Vérifier les fichiers modifiés/ajoutés
git status

# Vérifier le diff des changements
git diff src/WinSpaces/Diagnostics/IHealthCheckable.cs
git diff --cached

# Vérifier la structure Modules/
find src/WinSpaces/Modules -type d
```

## Après le commit

```bash
# Créer un tag pour la Phase 1
git tag -a v2.1.0 -m "Phase 1: Fondations Extensibles

Architecture modulaire complète pour transformation WinSpaces.
Prochaine étape: Implémentation Spotlight (Phase 2)."

# Pousser vers le dépôt distant (si configuré)
git push origin main
git push origin v2.1.0
```

## Note importante

⚠️ **Compilation Windows requise** : Les fichiers créés nécessitent Windows + .NET 8 SDK pour compilation complète. Sur macOS, la validation syntaxique est OK mais le build complet n'est pas possible.

Avant de committer sur Windows :
1. Restaurer les packages : `dotnet restore`
2. Vérifier la compilation : `dotnet build`
3. Corriger les éventuelles erreurs de dépendances manquantes

✅ Une fois validé sur Windows, procéder au commit avec l'une des options ci-dessus.
