// ---------------------------------------------------------------------------
// WinSpaces — Définitions COM (interfaces Shell Windows non documentées pour
// les bureaux virtuels).
//
// Portage des interfaces établies par le projet open-source MScholtes/
// VirtualDesktop (https://github.com/MScholtes/VirtualDesktop), licence MIT
// © 2017 Markus Scholtes. L'ordre EXACT des méthodes (vtable) est préservé,
// indispensable pour appeler ces interfaces non documentées.
// Cachet de compatibilité : Windows 10 1809 → Windows 11 23H2.
// ---------------------------------------------------------------------------

using System.Runtime.InteropServices;
using WinSpaces.Native;

namespace WinSpaces.Desktops.Interop;

// ---------------------------------------------------------------------------
// CLSID / GUIDs
// ---------------------------------------------------------------------------
internal static class Guids
{
    public static readonly Guid CLSID_ImmersiveShell = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    public static readonly Guid CLSID_VirtualDesktopManagerInternal = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
    public static readonly Guid CLSID_VirtualDesktopManager = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");
    public static readonly Guid CLSID_VirtualDesktopPinnedApps = new("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
}

// ---------------------------------------------------------------------------
// Structures & énumérations partagées
// ---------------------------------------------------------------------------

[StructLayout(LayoutKind.Sequential)]
internal struct SIZE
{
    public int X;
    public int Y;
}

internal enum APPLICATION_VIEW_CLOAK_TYPE : int
{
    AVCT_NONE = 0,
    AVCT_DEFAULT = 1,
    AVCT_VIRTUAL_DESKTOP = 2
}

internal enum APPLICATION_VIEW_COMPATIBILITY_POLICY : int
{
    AVCP_NONE = 0,
    AVCP_SMALL_SCREEN = 1,
    AVCP_TABLET_SMALL_SCREEN = 2,
    AVCP_VERY_SMALL_SCREEN = 3,
    AVCP_HIGH_SCALE_FACTOR = 4
}

// ---------------------------------------------------------------------------
// IApplicationView — {372E1D3B-38D3-42E4-A15B-8AB2B178F513}
// (InterfaceIsIInspectable) : vue d'une application (fenêtre UWP/Win32).
// ---------------------------------------------------------------------------
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
[Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
internal interface IApplicationView
{
    int SetFocus();
    int SwitchTo();
    int TryInvokeBack(IntPtr /* IAsyncCallback* */ callback);
    int GetThumbnailWindow(out IntPtr hwnd);
    int GetMonitor(out IntPtr /* IImmersiveMonitor */ immersiveMonitor);
    int GetVisibility(out int visibility);
    int SetCloak(APPLICATION_VIEW_CLOAK_TYPE cloakType, int unknown);
    int GetPosition(ref Guid /* GUID for IApplicationViewPosition */ guid, out IntPtr position);
    int SetPosition(ref IntPtr position);
    int InsertAfterWindow(IntPtr hwnd);
    int GetExtendedFramePosition(out RECT rect);
    int GetAppUserModelId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    int SetAppUserModelId(string id);
    int IsEqualByAppUserModelId(string id, out int result);
    int GetViewState(out uint state);
    int SetViewState(uint state);
    int GetNeediness(out int neediness);
    int GetLastActivationTimestamp(out ulong timestamp);
    int SetLastActivationTimestamp(ulong timestamp);
    int GetVirtualDesktopId(out Guid guid);
    int SetVirtualDesktopId(ref Guid guid);
    int GetShowInSwitchers(out int flag);
    int SetShowInSwitchers(int flag);
    int GetScaleFactor(out int factor);
    int CanReceiveInput(out bool canReceiveInput);
    int GetCompatibilityPolicyType(out APPLICATION_VIEW_COMPATIBILITY_POLICY flags);
    int SetCompatibilityPolicyType(APPLICATION_VIEW_COMPATIBILITY_POLICY flags);
    int GetSizeConstraints(IntPtr /* IImmersiveMonitor* */ monitor, out SIZE size1, out SIZE size2);
    int GetSizeConstraintsForDpi(uint uint1, out SIZE size1, out SIZE size2);
    int SetSizeConstraintsForDpi(ref uint uint1, ref SIZE size1, ref SIZE size2);
    int OnMinSizePreferencesUpdated(IntPtr hwnd);
    int ApplyOperation(IntPtr /* IApplicationViewOperation* */ operation);
    int IsTray(out bool isTray);
    int IsInHighZOrderBand(out bool isInHighZOrderBand);
    int IsSplashScreenPresented(out bool isSplashScreenPresented);
    int Flash();
    int GetRootSwitchableOwner(out IApplicationView rootSwitchableOwner);
    int EnumerateOwnershipTree(out IObjectArray ownershipTree);
    int GetEnterpriseId([MarshalAs(UnmanagedType.LPWStr)] out string enterpriseId);
    int IsMirrored(out bool isMirrored);
    int Unknown1(out int unknown);
    int Unknown2(out int unknown);
    int Unknown3(out int unknown);
    int Unknown4(out int unknown);
    int Unknown5(out int unknown);
    int Unknown6(int unknown);
    int Unknown7();
    int Unknown8(out int unknown);
    int Unknown9(int unknown);
    int Unknown10(int unknownX, int unknownY);
    int Unknown11(int unknown);
    int Unknown12(out SIZE size1);
}

// ---------------------------------------------------------------------------
// IApplicationViewCollection — {1841C6D7-4F9D-42C0-AF41-8747538F10E5}
// ---------------------------------------------------------------------------
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
internal interface IApplicationViewCollection
{
    int GetViews(out IObjectArray array);
    int GetViewsByZOrder(out IObjectArray array);
    int GetViewsByAppUserModelId(string id, out IObjectArray array);
    int GetViewForHwnd(IntPtr hwnd, out IApplicationView view);
    int GetViewForApplication(object application, out IApplicationView view);
    int GetViewForAppUserModelId(string id, out IApplicationView view);
    int GetViewInFocus(out IntPtr view);
    int Unknown1(out IntPtr view);
    void RefreshCollection();
    int RegisterForApplicationViewChanges(object listener, out int cookie);
    int UnregisterForApplicationViewChanges(int cookie);
}

// ---------------------------------------------------------------------------
// IVirtualDesktop — {FF72FFDD-BE7E-43FC-9C03-AD81681E88E4}
// ---------------------------------------------------------------------------
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4")]
internal interface IVirtualDesktop
{
    bool IsViewVisible(IApplicationView view);
    Guid GetId();
}

// ---------------------------------------------------------------------------
// IVirtualDesktopManagerInternal — {F31574D6-B682-4CDC-BD56-1827860ABEC6}
// API interne du Shell (non documentée) : cœur de WinSpaces.
// ---------------------------------------------------------------------------
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("F31574D6-B682-4CDC-BD56-1827860ABEC6")]
internal interface IVirtualDesktopManagerInternal
{
    int GetCount();
    void MoveViewToDesktop(IApplicationView view, IVirtualDesktop desktop);
    bool CanViewMoveDesktops(IApplicationView view);
    IVirtualDesktop GetCurrentDesktop();
    void GetDesktops(out IObjectArray desktops);
    [PreserveSig]
    int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
    void SwitchDesktop(IVirtualDesktop desktop);
    IVirtualDesktop CreateDesktop();
    void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
    IVirtualDesktop FindDesktop(ref Guid desktopid);
}

// ---------------------------------------------------------------------------
// IVirtualDesktopManagerInternal2 — {0F3A72B0-4566-487E-9A33-4ED302F6D6CE}
// (Windows 10 2004+) : ajoute le nommage des bureaux. Optionnel.
// ---------------------------------------------------------------------------
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("0F3A72B0-4566-487E-9A33-4ED302F6D6CE")]
internal interface IVirtualDesktopManagerInternal2
{
    int GetCount();
    void MoveViewToDesktop(IApplicationView view, IVirtualDesktop desktop);
    bool CanViewMoveDesktops(IApplicationView view);
    IVirtualDesktop GetCurrentDesktop();
    void GetDesktops(out IObjectArray desktops);
    [PreserveSig]
    int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
    void SwitchDesktop(IVirtualDesktop desktop);
    IVirtualDesktop CreateDesktop();
    void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
    IVirtualDesktop FindDesktop(ref Guid desktopid);
    void Unknown1(IVirtualDesktop desktop, out IntPtr unknown1, out IntPtr unknown2);
    void SetName(IVirtualDesktop desktop, [MarshalAs(UnmanagedType.HString)] string name);
}

// ---------------------------------------------------------------------------
// IVirtualDesktopManager — {A5CD92FF-29BE-454C-8D04-D82879FB3F1B}
// API OFFICIELLE (CLSID {AA509086-5CA9-4C25-8F95-589D3C07B48A}) : sert de
// fallback et de vérificateur (IsWindowOnCurrentVirtualDesktop, déplacement
// direct de fenêtre par GUID de bureau).
// ---------------------------------------------------------------------------
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
internal interface IVirtualDesktopManager
{
    bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
    Guid GetWindowDesktopId(IntPtr topLevelWindow);
    void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
}

// ---------------------------------------------------------------------------
// IVirtualDesktopPinnedApps — {4CE81583-1E4C-4632-A621-07A53543148F}
// Applicatif "sur tous les bureaux" (shim de l'API interne).
// ---------------------------------------------------------------------------
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("4CE81583-1E4C-4632-A621-07A53543148F")]
internal interface IVirtualDesktopPinnedApps
{
    bool IsAppIdPinned(string appId);
    void PinAppID(string appId);
    void UnpinAppID(string appId);
    bool IsViewPinned(IApplicationView applicationView);
    void PinView(IApplicationView applicationView);
    void UnpinView(IApplicationView applicationView);
}

// ---------------------------------------------------------------------------
// IObjectArray — {92CA9DCD-5622-4BBA-A805-5E9F541BD8C9}
// Tableau d'objets COM (liste des bureaux, des vues…).
// ---------------------------------------------------------------------------
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
internal interface IObjectArray
{
    void GetCount(out int count);
    void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object obj);
}

// ---------------------------------------------------------------------------
// IServiceProvider10 — {6D5140C1-7436-11CE-8034-00AA006009FA}
// Variante "10" de IServiceProvider : résout les services du Shell à partir
// d'une instance de CLSID_ImmersiveShell (schéma de résolution utilisé par
// le Shell lui-même plutôt que des CLSID_* d'interface).
// ---------------------------------------------------------------------------
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
internal interface IServiceProvider10
{
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object QueryService(ref Guid service, ref Guid riid);
}