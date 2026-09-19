# WinSpaces

Reproduit les **Espaces** (Spaces) de macOS sur Windows — une application **open-source** en C# / .NET 8 qui vit dans la barre des tâches, donne un bureau virtuel dédié à chaque application en plein écran et permet de basculer d'espace via gestes du trackpad et raccourcis clavier.

> **Statut :** v0.1.5 — support complet de Windows 11 24H2, 25H2 et 26H2 (build 26100+), bureaux virtuels, hotkeys, momentum, gestes Précision Touchpad, et publish single-file automatisé.

## Fonctionnalités

| Fonctionnalité | Description |
|---|---|
| **Espace plein écran automatique** | Quand une app passe en plein écran, WinSpaces crée un bureau virtuel, y déplace la fenêtre et y bascule. À la sortie, l'espace est supprimé et on revient au bureau d'origine. |
| **Gestes trackpad (momentum)** | Balayage horizontal 2 doigts / molette horizontale soutenue → changement d'espace. Fonctionne sur tous les périphériques. |
| **Précision Touchpad (3/4 doigts)** | Sur les portables Windows 10/11 compatibles : détection automatique, balayage 3+ doigts via Raw Input HID (HidP_* indépendant du constructeur). |
| **Raccourcis clavier** | Ctrl+Alt+Flèche droite/gauche, Ctrl+Alt+N, Ctrl+Alt+W — voir ci-dessous. |
| **Icône de barre des tâches** | Menu contextuel : nouvel espace, suivant/précédent, déplacer fenêtre, options, quitter. |
| **Single-file** | Publiez-vous en un seul .exe auto-contenu grâce au workflow GitHub Actions (v0.1.3+). |

## Raccourcis

| Action | Combinaison |
|---|---|
| Espace suivant | `Ctrl+Alt+→` |
| Espace précédent | `Ctrl+Alt+←` |
| Nouvel espace | `Ctrl+Alt+N` |
| Déplacer fenêtre vers l'espace suivant | `Ctrl+Alt+W` |

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
    HotkeyService.cs                   ← raccourcis globaux (RegisterHotKey)
    MomentumGestureDetector.cs         ← molette horizontale (fallback universel)
    PrecisionTouchpadWatcher.cs        ← Raw Input HID (3/4 doigts)
    GestureManager.cs                  ← orchestrateur momentum ↔ précision
    FullscreenSpaceManager.cs          ← détection plein écran → espace dédié
    TrayIconService.cs                 ← icône + menu contextuel
    AppLog.cs                          ← journal %LOCALAPPDATA%\WinSpaces\
    AppOptions.cs                      ← options en mémoire (v0.1)
    AutostartManager.cs                ← registre HKCU\...\Run
```

## Architecture technique

- **WinForms** : boucle de messages native, NotifyIcon sans dépendance lourde.
- **COM interne** (`IVirtualDesktopManagerInternal`) via `IServiceProvider10` + `CLSID_ImmersiveShell` — exactement comme le fait le Shell Windows lui-même. **Quatre schémas COM** sont sélectionnés au runtime selon le build Windows (`RtlGetVersion`) : Win10 (`F31574D6`), Win11 ≤ 23H2 (`53F5CA0B`, vtable historique), Win11 24H2 (même IID, vtable allongée), Win11 **25H2/26H2+** (build 26100+, même `IVirtualDesktopManagerInternal24H2` avec `SwitchDesktopAndMoveForegroundView`). API officielle `IVirtualDesktopManager` utilisée en fallback et pour le déplacement de fenêtres par GUID.
- **Hooks Win32** : `SetWinEventHook` (foreground) + `SetWindowsHookEx` (molette) — aucun hook n'injecte du code, ils ne font que surveiller.
- **Raw Input + HidP_*** : pour le Précision Touchpad, les rapports HID sont décodés via `hid.dll` (données « preparsed », `HidP_GetUsageValue`) plutôt que par des offsets figés. Un seul swipe est consommé jusqu'au lever des doigts.

## Limitations connues

- **Windows 11 25H2/26H2+** est désormais pris en charge (même `IVirtualDesktopManagerInternal24H2` avec `SwitchDesktopAndMoveForegroundView`, détection automatique par `RtlGetVersion`).
- Le parsing HID Précision Touchpad s'appuie sur HidP_* (indépendant du constructeur) ; un seuil de delta et un anti-spam « un swipe jusqu'au lever des doigts » évitent les déclenchements fantômes. Le mode momentum (2 doigts / molette) reste le fallback universel.
- Les gestes système 3/4 doigts par défaut consomment l'entrée avant WinSpaces ; désactiver ces gestes dans les paramètres Windows est recommandé (et informé par bulle au lancement).

## Crédits & licences

- **WinSpaces** : licence MIT, 2026.
- **Définitions COM internes** : portées du projet [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) (licence MIT © 2017 Markus Scholtes). Seul l'ordre des vtables et les GUIDs sont repris ; le code applicatif est original.