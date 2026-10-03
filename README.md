# WinSpaces

Reproduit les **Espaces** (Spaces) de macOS sur Windows — une application **open-source** en C# / .NET 8 qui vit dans la barre des tâches, donne un bureau virtuel dédié à chaque application en plein écran et permet de basculer d'espace via gestes du trackpad et raccourcis clavier.

> **Statut :** v3.3.1 — version de fiabilisation (audit complet : décodage HID du Précision Touchpad, bascule d'espace COM, raccourcis, deadlocks, fuites COM/handles). Support complet de Windows 11 24H2, 25H2 et 26H2 (build 26100+), bureaux virtuels, hotkeys, momentum, gestes Précision Touchpad **style macOS (3/4 doigts horizontal + vertical)**, publish single-file automatisé, et **Dashboard WPF** avec diagnostic système intégré. Thèmes Light/Dark stables avec préservation des styles de base.

## Fonctionnalités

| Fonctionnalité | Description |
|---|---|
| **Dashboard WPF** 🆕 | Interface graphique moderne affichant l'état de santé de tous les composants système en temps réel. Accès depuis le menu de la barre des tâches. |
| **Diagnostic système** 🆕 | Surveillance automatique des services : API COM bureaux virtuels, hooks système, Precision Touchpad, raccourcis clavier. |
| **Espace plein écran automatique** | Quand une app passe en plein écran, WinSpaces crée un bureau virtuel, y déplace la fenêtre et y bascule. À la sortie, l'espace est supprimé et on revient au bureau d'origine. |
| **Gestes trackpad (momentum)** | Balayage horizontal 2 doigts / molette horizontale soutenue → changement d'espace. Fonctionne sur tous les périphériques. |
| **Précision Touchpad (3/4 doigts)** | Sur les portables Windows 10/11 compatibles : détection automatique, balayage 3+ doigts via Raw Input HID (HidP_* indépendant du constructeur). |
| **Raccourcis clavier** | Ctrl+Alt+Flèche droite/gauche, Ctrl+Alt+N, Ctrl+Alt+W — voir ci-dessous. |
| **Spotlight** | `Alt+Espace` : lanceur d'applications, recherche de fichiers (Bureau, Documents, Téléchargements…) et calculatrice. |
| **Icône de barre des tâches** | Menu contextuel : dashboard, nouvel espace, suivant/précédent, déplacer fenêtre, options, quitter. |
| **Single-file** | Publiez-vous en un seul .exe auto-contenu grâce au workflow GitHub Actions. |

## Raccourcis

| Action | Combinaison |
|---|---|
| Espace suivant | `Ctrl+Alt+→` |
| Espace précédent | `Ctrl+Alt+←` |
| Nouvel espace | `Ctrl+Alt+N` |
| Déplacer fenêtre vers l'espace suivant | `Ctrl+Alt+W` |
| Spotlight | `Alt+Espace` |

> Les options « Espace plein écran auto » et « Gestes trackpad » du menu de la barre des tâches sont prises en compte immédiatement (non persistées entre deux lancements).

## Prérequis

- **Windows 10 1809+** ou **Windows 11** (24H2, 25H2, 26H2 et futures versions — support par détection de build au runtime).
- SDK .NET 8 pour compiler.
- Pour les gestes 3/4 doigts : portables avec pilote Précision Touchpad (la plupart des portables modernes).
- Pour éviter les conflits avec les gestes système 3/4 doigts : paramètres → Bluetooth et périphériques → Pavé tactile → *Balayer à trois/four doigts* → **Rien**.

## Build

```bash
dotnet restore src/WinSpaces/WinSpaces.csproj
dotnet build src/WinSpaces/WinSpaces.csproj -c Release
```

> Le build est possible depuis **macOS/Linux** grâce à `<EnableWindowsTargeting>true</EnableWindowsTargeting>` et `<Platforms>x64</Platforms>` dans le csproj. L'exécution reste Windows-only.

### Publish (executable standalone)

```bash
dotnet publish src/WinSpaces/WinSpaces.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-dir
```

> Un workflow GitHub Actions automatise cette étape à chaque tag `v*`. Voir les [Releases](https://github.com/Mycate39/WinSpaces/releases) pour l'exécutable pré-compilé.

## Arborescence

```
WinSpaces.sln
src/WinSpaces/
  WinSpaces.csproj
  app.manifest
  Program.cs                           ← point d'entrée (mutex single-instance)
  WinSpacesApplicationContext.cs       ← bootstrap & wiring des services
  Native/
    NativeTypes.cs                     ← constantes Win32
    NativeStructs.cs                   ← structures & délégués P/Invoke
    NativeMethods.cs                   ← DllImport user32/kernel32/hid/ntdll
    HookBase.cs                        ← base abstraite des hooks LL
    MouseHook.cs                       ← WH_MOUSE_LL (molette)
    KeyboardHook.cs                    ← WH_KEYBOARD_LL
    WindowEventHook.cs                 ← SetWinEventHook (foreground)
    MessageWindow.cs                   ← fenêtre message-only (WM_HOTKEY, WM_INPUT)
  Desktops/
    VirtualDesktopService.cs           ← orchestrateur COM bureaux virtuels
    Interop/
      VirtualDesktopInterop.cs         ← interfaces COM non documentées (vtable exacte)
  Services/
    HotkeyService.cs                   ← raccourcis globaux (RegisterHotKey, ids 0xB01–0xB04)
    GlobalHotkeyManager.cs             ← raccourcis des modules (Spotlight, ids 0xC001+)
    MomentumGestureDetector.cs         ← molette horizontale (fallback universel)
    PrecisionTouchpadWatcher.cs        ← Raw Input HID (3/4 doigts)
    GestureManager.cs                  ← orchestrateur momentum ↔ précision
    FullscreenSpaceManager.cs          ← détection plein écran → espace dédié
    TrayIconService.cs                 ← icône + menu contextuel
    AppLog.cs                          ← journal %LOCALAPPDATA%\WinSpaces\ (rotation à 5 Mo)
    AppOptions.cs                      ← options en mémoire (v0.1)
    AutostartManager.cs                ← registre HKCU\...\Run
```

## Architecture technique

- **WinForms + WPF** : la boucle de messages WinForms (`Application.Run`) distribue aussi les messages du Dispatcher WPF (fenêtre cachée) — aucun pompage manuel. Les ressources partagées (`Styles.xaml`, `WindowStyles.xaml`) sont chargées explicitement, et les exceptions non gérées (Dispatcher, WinForms, tâches) sont journalisées au lieu de fermer l'application.
- **COM interne** (`IVirtualDesktopManagerInternal`) via `IServiceProvider10` + `CLSID_ImmersiveShell` — exactement comme le fait le Shell Windows lui-même. **Quatre schémas COM** sont sélectionnés au runtime selon le build Windows (`RtlGetVersion`) : Win10 (`F31574D6`), Win11 ≤ 23H2 (`53F5CA0B`, vtable historique), Win11 24H2 (même IID, vtable allongée), Win11 **25H2/26H2+** (build 26100+, même `IVirtualDesktopManagerInternal24H2` avec `SwitchDesktopAndMoveForegroundView`). API officielle `IVirtualDesktopManager` utilisée en fallback et pour le déplacement de fenêtres par GUID.
- **Hooks Win32** : deux `SetWinEventHook` ciblés (`EVENT_SYSTEM_FOREGROUND`, `EVENT_OBJECT_LOCATIONCHANGE`) + `SetWindowsHookEx` (molette). Aucun hook n'injecte de code ; les callbacks natifs sont protégés contre les exceptions et les actions longues (bascule COM) sont différées hors du hook bas niveau.
- **Raw Input + HidP_*** : pour le Précision Touchpad, les données « preparsed » sont obtenues via `GetRawInputDeviceInfo(RIDI_PREPARSEDDATA)` et les rapports décodés par `HidP_GetUsageValue` (statut `HIDP_STATUS_SUCCESS = 0x00110000`). Le mode hybride (Contact Count = 0 sur les rapports suivants d'une trame) est géré, et un seul geste (horizontal **ou** vertical) est consommé jusqu'au lever des doigts.
- **Configuration** : `%APPDATA%\WinSpaces\config.json` avec hot-reload (anti-rebond, rechargement sur le thread UI, configuration courante conservée si le fichier est en cours d'écriture).

## Limitations connues

- **Windows 11 24H2+** : la bascule d'espace utilise `SwitchDesktopAndMoveForegroundView`, qui peut emmener la fenêtre active vers l'espace cible (à confirmer).
- **Windows 11 21H2 / 22H2 (avant les mises à jour 2023)** utilise d'autres IID COM : WinSpaces se rabat alors sur l'API officielle (fonctions réduites).
- Un Précision Touchpad branché **après** le lancement n'est pas relié aux gestes (redémarrer WinSpaces).
- Les widgets n'actualisent pas encore leur affichage en temps réel.
- Le parsing HID Précision Touchpad s'appuie sur HidP_* (indépendant du constructeur) ; un seuil de delta et un anti-spam « un swipe jusqu'au lever des doigts » évitent les déclenchements fantômes. Le mode momentum (2 doigts / molette) reste le fallback universel.
- Les gestes système 3/4 doigts par défaut consomment l'entrée avant WinSpaces ; désactiver ces gestes dans les paramètres Windows est recommandé (et informé par bulle au lancement).

## Crédits & licences

- **WinSpaces** : licence MIT, 2026.
- **Définitions COM internes** : portées du projet [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) (licence MIT © 2017 Markus Scholtes). Seul l'ordre des vtables et les GUIDs sont repris ; le code applicatif est original.
