## 🛡️ WinSpaces v3.3.1 — « Fiabilisation : gestes, espaces & stabilité »

Version corrective issue d'un audit complet du code (interop natif, COM, concurrence, cycle de vie WinForms/WPF). Aucune nouvelle fonctionnalité : les fonctionnalités existantes fonctionnent enfin comme prévu.

### 🔴 Correctifs critiques
- **Gestes Précision Touchpad (3/4 doigts) réparés** : `HIDP_STATUS_SUCCESS` valait `0` au lieu de `0x00110000`, donc tous les appels `HidP_*` étaient considérés en échec. Les données « preparsed » étaient demandées à `HidD_GetPreparsedData` avec un handle Raw Input (invalide) : elles proviennent désormais de `GetRawInputDeviceInfo(RIDI_PREPARSEDDATA)`.
- **Changement d'espace réparé** : `GetAdjacentDesktop` recevait `-1/+1` au lieu des directions `3` (gauche) / `4` (droite) attendues par l'Explorer (`E_INVALIDARG`). Concerne `Ctrl+Alt+←/→`, les gestes et le menu de la barre des tâches.
- **Raccourcis en collision** : Spotlight et les raccourcis d'espaces partageaient l'id `0xB01` sur la même fenêtre — `Alt+Espace` changeait aussi d'espace et `Ctrl+Alt+→` ouvrait Spotlight. Les modules utilisent désormais la plage `0xC001+`.
- **Blocages au démarrage et à la fermeture** : `WidgetManager` attendait du code async de façon bloquante sur le thread UI (deadlock via le `WindowsFormsSynchronizationContext`). Chargement non bloquant et sauvegarde synchrone à la fermeture.

### 🟠 Stabilité & concurrence
- **Pont WinForms/WPF** : suppression du `WpfMessageFilter`, qui faisait un `Dispatcher.Invoke` synchrone à chaque message Win32 (boucle imbriquée, ré-entrance, coût CPU à chaque `WM_INPUT`).
- **Ressources WPF** : `Styles.xaml` et `WindowStyles.xaml` sont maintenant chargés (ils étaient introuvables pour la barre de menu et Spotlight).
- **Exceptions** : gestionnaires globaux (Dispatcher WPF, WinForms, AppDomain, tâches non observées) — journalisation au lieu d'un arrêt brutal.
- **Configuration (hot-reload)** : l'événement était levé sur un thread du pool *en tenant* le `SemaphoreSlim` (deadlock si un abonné sauvegardait). Rechargement sur le thread UI avec anti-rebond ; un fichier en cours d'écriture ne remet plus la configuration aux valeurs par défaut.
- **Plein écran automatique** : hook WinEvent limité aux deux événements utiles (au lieu d'une plage de ~32 000 types), protection contre la ré-entrance pendant les appels COM (double création de bureau), nettoyage des espaces dédiés des fenêtres fermées, filtrage sur la fenêtre au premier plan.
- **Hooks bas niveau** : callbacks protégés contre les exceptions ; la bascule d'espace est différée hors du hook souris (évite son retrait silencieux par `LowLevelHooksTimeout`).
- **Fermeture** : `Dispose` libère désormais réellement hooks, raccourcis, COM et icône de la barre des tâches (y compris à la fermeture de session) ; chaque étape est isolée.

### 🟡 Gestes, COM & divers
- Touchpad : suppression du delta vertical fantôme (Mission Control intempestif), gestion du mode hybride (Contact Count = 0), filtrage par périphérique, un seul geste par contact (horizontal **ou** vertical), plus d'allocation par rapport HID.
- COM : libération du `IUnknown` de `CLSID_ImmersiveShell`, des RCW de bureaux et protection contre un double release dans `RemoveDesktop`.
- Les options « Espace plein écran auto » et « Gestes trackpad » du menu de la barre des tâches sont enfin appliquées.
- Métriques de diagnostic thread-safe (`ConcurrentDictionary`).
- Widgets : identifiant stable (il changeait à chaque lecture), désabonnement du timer corrigé.
- Spotlight : motifs de recherche invalides filtrés, énumération paresseuse des fichiers.
- Journal : rotation automatique au-delà de 5 Mo.

### ⚠️ Note
Ces correctifs ont été validés par compilation ; merci de signaler tout comportement inattendu (gestes, bascule d'espace, plein écran) via les Issues.

### 📦 Installation
Téléchargez `WinSpaces.exe` ci-dessous (auto-contenu, Windows 10 1809+ / Windows 11 x64) et lancez-le.
