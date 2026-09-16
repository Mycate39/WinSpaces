# WinSpaces

Reproduit les **Espaces** (Spaces) de macOS sur Windows — une application **open-source** en C# / .NET 8 qui vit dans la barre des tâches, donne un bureau virtuel dédié à chaque application en plein écran et permet de basculer d'espace via gestes du trackpad et raccourcis clavier.

> **Statut :** v0.1.1 — les bases (COM interne Win10/Win11 24H2, hotkeys, momentum, icône) sont fonctionnelles ; le parsing HID Précision Touchpad s'appuie sur HidP_* (indépendant du constructeur).

## Fonctionnalités

| Fonctionnalité | Description |
|---|---|
| **Espace plein écran automatique** | Quand une app passe en plein écran, WinSpaces crée un bureau virtuel, y déplace la fenêtre et y bascule. À la sortie, l'espace est supprimé et on revient au bureau d'origine. |
| **Gestes trackpad (momentum)** | Balayage horizontal 2 doigts / molette horizontale soutenue → changement d'espace. Fonctionne sur tous les périphériques. |
| **Précision Touchpad (3/4 doigts)** | Sur les portables Windows 10/11 compatibles : détection automatique, balayage 3+ doigts via Raw Input HID. |
| **Raccourcis clavier** | Ctrl+Alt+Flèche droite/gauche, Ctrl+Alt+N, Ctrl+Alt+W — voir ci-dessous. |
| **Icône de barre des tâches** | Menu contextuel : nouvel espace, suivant/précédent, déplacer fenêtre, options, quitter. |

## Raccourcis

| Action | Combinaison |
|---|---|
| Espace suivant | `Ctrl+Alt+→` |
| Espace précédent | `Ctrl+Alt+←` |
| Nouvel espace | `Ctrl+Alt+N` |
| Déplacer fenêtre vers l'espace suivant | `Ctrl+Alt+W` |

## Prérequis

- **Windows 10 1809+** ou **Windows 11** (API interne testée jusqu'à Windows 11 24H2).
- SDK .NET 8 pour compiler.
- Pour les gestes 3/4 doigts : portables avec pilote Précision Touchpad (la plupart des portables modernes).
- Pour éviter les conflits avec les gestes système 3/4 doigts : paramètres → Bluetooth et périphériques → Pavé tactile → *Balayer à trois/four doigts* → **Rien**.

## Build

```bash
dotnet restore src/WinSpaces/WinSpaces.csproj
dotnet build src/WinSpaces/WinSpaces.csproj -c Release
```

> Le build est possible depuis **macOS/Linux** grâce à `<EnableWindowsTargeting>true</EnableWindowsTargeting>` dans le csproj. L'exécution reste Windows-only.

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
- **COM interne** (`IVirtualDesktopManagerInternal`) via `IServiceProvider10` + `CLSID_ImmersiveShell` — exactement comme le fait le Shell Windows lui-même. Trois schémas COM sont sélectionnés au runtime selon le build Windows (`RtlGetVersion`) : Win10 (`F31574D6`), Win11 ≤ 23H2 (`53F5CA0B`, vtable historique), Win11 24H2+ (même IID, vtable allongée). API officielle `IVirtualDesktopManager` utilisée en fallback et pour le déplacement de fenêtres par GUID.
- **Hooks Win32** : `SetWinEventHook` (foreground) + `SetWindowsHookEx` (molette) — aucun hook n'injecte du code, ils ne font que surveiller.
- **Raw Input + HidP_*** : pour le Précision Touchpad, les rapports HID sont décodés via `hid.dll` (données « preparsed », `HidP_GetUsageValue`) plutôt que par des offsets figés. Un seul swipe est consommé jusqu'au lever des doigts.

## Limitations connues

- **Windows 11 24H2+** est désormais pris en charge (vtable `IVirtualDesktopManagerInternal24H2` avec `SwitchDesktopAndMoveForegroundView`).
- Le parsing HID Précision Touchpad s'appuie sur HidP_* (indépendant du constructeur) ; un seuil de delta et un anti-spam « un swipe jusqu'au lever des doigts » évitent les déclenchements fantômes. Le mode momentum (2 doigts / molette) reste le fallback universel.
- Les gestes système 3/4 doigts par défaut consomment l'entrée avant WinSpaces ; désactiver ces gestes dans les paramètres Windows est recommandé (et informé par bulle au lancement).

## Crédits & licences

- **WinSpaces** : licence MIT, 2026.
- **Définitions COM internes** : portées du projet [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) (licence MIT © 2017 Markus Scholtes). Seul l'ordre des vtables et les GUIDs sont repris ; le code applicatif est original.