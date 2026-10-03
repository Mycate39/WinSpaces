## 🍏 WinSpaces v3.4.0 — « Tableau de bord macOS & démarrage stable »

Cette version **remplace la v3.3.1, qui plantait au démarrage**. Elle a été testée sur un PC Windows 11 25H2 (build 26200) : démarrage, barre de menu, tableau de bord, journaux, Spotlight, raccourcis et gestes 3 doigts fonctionnent.

### 🔴 Correctifs critiques
- **Plantage au lancement (v3.3.1)** : les rayons d'arrondi de `Styles.xaml` étaient déclarés en `Double` alors qu'ils sont utilisés comme `CornerRadius` → `XamlParseException` à l'affichage de la barre de menu. Types corrigés.
- **Boucle infinie du touchpad** : chaque `WM_INPUT_DEVICE_CHANGE` réenregistrait le pavé, ce qui renvoyait aussitôt un nouveau `WM_INPUT_DEVICE_CHANGE`. La file de messages était saturée : l'application tournait mais **aucune fenêtre ne se dessinait** (et le journal se remplissait). Le pavé n'est plus réenregistré ; un seul ré-inventaire est fait 500 ms après une rafale de changements.
- **Bascule d'espace** (`Ctrl+Alt+←/→`, gestes) : le bureau voisin est désormais calculé à partir de la liste ordonnée des bureaux au lieu de `GetAdjacentDesktop` (non documentée). Sous Windows 11 24H2+, la bascule n'emmène plus la fenêtre active (`SwitchDesktop` au lieu de `SwitchDesktopAndMoveForegroundView`, réservé à `Ctrl+Alt+W`).

### ✨ Nouveautés
- **Tableau de bord style macOS** : fenêtre sans bordure aux coins arrondis, feux tricolores, barre latérale, thème clair/sombre qui suit Windows (`Theme.Mode = Auto`).
  - **Aperçu** : espace actuel, composants opérationnels, nombre d'erreurs et d'avertissements, réglages à interrupteurs, dernières erreurs.
  - **Diagnostic** : état détaillé de chaque composant.
  - **Journaux** (inspiré de Console.app) : lecture en direct, filtres par niveau, recherche, détail des traces d'exception, copie, ouverture du fichier ou du dossier, effacement.
  - **Répertoire** : `winspaces.log` et son archive `winspaces.log.old`, avec leur taille.
- Le tableau de bord s'ouvre au **lancement manuel** ; nouvel élément « Journaux et erreurs… » dans le menu de l'icône.
- Notification « un seul espace » qui propose `Ctrl+Alt+N` quand il n'y a pas de bureau vers lequel basculer.
- « Ouvrir au démarrage de Windows » applique réellement la clé de registre depuis le tableau de bord ; les coches du menu restent synchronisées.

### 🧰 Diagnostic & robustesse
- Erreur fatale → fenêtre explicite avec le chemin du journal (plus de fermeture silencieuse), y compris pour les exceptions hors du thread principal.
- Jalons de démarrage dans le journal (`Init 1/5` … `Démarrage terminé`) et modules non critiques isolés : un module en échec est journalisé sans empêcher le reste de démarrer.
- **Mode sans échec** : `WinSpaces.exe --safe` (sans gestes, plein écran automatique ni barre de menu).
- Journalisation des raccourcis reçus, des bascules et des gestes du touchpad (amplitude mesurée vs seuil quand un geste ne déclenche rien).
- Les messages identiques consécutifs sont regroupés dans le journal (« répété N fois »).
- Nouvel outil `scripts/Diagnostic WinSpaces.cmd` : lance l'application, propose un test interactif et produit un rapport (journal + erreurs Windows) sur le Bureau.
- Tests unitaires du parseur de journaux et de la lecture incrémentale.

### 📦 Installation
Téléchargez `WinSpaces.exe` ci-dessous (auto-contenu, Windows 10 1809+ / Windows 11 x64) et lancez-le. Si vous aviez la v3.3.1, remplacez simplement l'exécutable.
