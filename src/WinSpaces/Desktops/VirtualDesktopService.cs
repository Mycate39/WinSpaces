using System.Runtime.InteropServices;
using WinSpaces.Desktops.Interop;
using WinSpaces.Native;
using WinSpaces.Services;

namespace WinSpaces.Desktops;

/// <summary>
/// Service de haut niveau autour des bureaux virtuels Windows.
/// S'appuie sur l'API interne « explorateur » (IVirtualDesktopManagerInternal
/// résolu via IServiceProvider10/CLSID_ImmersiveShell) et sur l'API OFFICIELLE
/// IVirtualDesktopManager en fallback et pour les opérations par fenêtre.
///
/// Trois schémas COM coexistent — un seul est actif selon la version Windows :
///  - Windows 10 1809 → 22H2 : IVirtualDesktopManagerInternal10 (IID F31574D6) ;
///  - Windows 11 21H2 → 23H2 : IVirtualDesktopManagerInternal (IID 53F5CA0B,
///    vtable historique, CreateDesktop en position 7) ;
///  - Windows 11 24H2+        : IVirtualDesktopManagerInternal24H2 (même IID,
///    vtable allongée : SwitchDesktopAndMoveForegroundView est inséré).
/// </summary>
internal sealed class VirtualDesktopService : IDisposable
{
    private IVirtualDesktopManagerInternal? _w11Last;     // Win11 ≤ 23H2
    private IVirtualDesktopManagerInternal24H2? _w11New;  // Win11 ≥ 24H2
    private IVirtualDesktopManagerInternal10? _w10;       // Windows 10
    private IApplicationViewCollection? _viewCollection;
    private IVirtualDesktopManager? _official;
    private bool _disposed;

    /// <summary>L'API interne (création/suppression/bascule) est disponible.</summary>
    public bool IsInternalApiAvailable => _w11Last != null || _w11New != null || _w10 != null;

    /// <summary>L'API officielle est disponible.</summary>
    public bool IsOfficialApiAvailable => _official != null;

    /// <summary>Uniquement l'API officielle (fonctionnalités réduites).</summary>
    public bool IsFallbackOnly => !IsInternalApiAvailable && _official != null;

    // @@LISTES@@

    /// <summary>GUID de tous les bureaux existants, dans l'ordre.</summary>
    public IReadOnlyList<Guid> GetDesktopIds()
    {
        var result = new List<Guid>();
        try
        {
            var desktops = GetDesktopsArray();
            if (desktops is null) return result;
            try
            {
                desktops.GetCount(out var count);
                for (var i = 0; i < count; i++)
                {
                    var iid = DesktopGuid;
                    desktops.GetAt(i, ref iid, out object obj);
                    if (obj is not null)
                        result.Add(GetId(obj));
                }
            }
            finally
            {
                Marshal.ReleaseComObject(desktops);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }

        return result;
    }

    /// <summary>GUID de l'interface IVirtualDesktop correspondant au schéma actif.</summary>
    private Guid DesktopGuid
        => _w10 != null ? typeof(IVirtualDesktop10).GUID : typeof(IVirtualDesktop).GUID;

    private static Guid GetId(object desktop)
        => desktop switch
        {
            IVirtualDesktop d11 => d11.GetId(),
            IVirtualDesktop10 d10 => d10.GetId(),
            _ => Guid.Empty
        };

    private IObjectArray? GetDesktopsArray()
    {
        if (_w11New != null) { _w11New.GetDesktops(out var a); return a; }
        if (_w11Last != null) { _w11Last.GetDesktops(out var a); return a; }
        if (_w10 != null) { _w10.GetDesktops(out var a); return a; }
        return null;
    }

    public int Count
    {
        get
        {
            try
            {
                if (_w11New != null) return _w11New.GetCount();
                if (_w11Last != null) return _w11Last.GetCount();
                if (_w10 != null) return _w10.GetCount();
                return 0;
            }
            catch (Exception ex)
            {
                AppLog.Error(ex);
                return 0;
            }
        }
    }
    public Guid CurrentDesktopId
    {
        get
        {
            try
            {
                if (_w11New is not null || _w11Last is not null)
                {
                    var cur = _w11New?.GetCurrentDesktop() ?? _w11Last!.GetCurrentDesktop();
                    if (cur is null) return Guid.Empty;
                    try { return cur.GetId(); }
                    finally { Marshal.ReleaseComObject(cur); }
                }
                if (_w10 is not null)
                {
                    var cur = _w10.GetCurrentDesktop();
                    if (cur is null) return Guid.Empty;
                    try { return cur.GetId(); }
                    finally { Marshal.ReleaseComObject(cur); }
                }
                return Guid.Empty;
            }
            catch (Exception ex)
            {
                AppLog.Error(ex);
                return Guid.Empty;
            }
        }
    }

    /// <summary>Index (0-based) du bureau courant, -1 si indisponible.</summary>
    public int CurrentDesktopIndex
    {
        get
        {
            var id = CurrentDesktopId;
            if (id == Guid.Empty) return -1;
            var ids = GetDesktopIds();
            for (var i = 0; i < ids.Count; i++)
                if (ids[i] == id) return i;
            return -1;
        }
    }

    // @@BASCULE@@

    /// <summary>Bascule vers le bureau d'identifiant <paramref name="id"/>.</summary>
    public bool SwitchToDesktop(Guid id)
    {
        if (id == Guid.Empty) return false;

        try
        {
            var vd = FindDesktop(id);
            if (vd is null) return false;
            try
            {
                if (_w11New != null && vd is IVirtualDesktop d112) { _w11New.SwitchDesktopAndMoveForegroundView(d112); return true; }
                if (_w11Last != null && vd is IVirtualDesktop d11) { _w11Last.SwitchDesktop(d11); return true; }
                if (_w10 != null && vd is IVirtualDesktop10 d10) { _w10.SwitchDesktop(d10); return true; }
                return false;
            }
            finally
            {
                Marshal.ReleaseComObject(vd);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return false;
        }
    }
    /// <summary>Bascule de <paramref name="offset"/> bureaux (-1 = précédent, +1 = suivant).</summary>
    public bool SwitchByOffset(int offset)
    {
        if (offset != -1 && offset != 1) return false;

        try
        {
            if (_w11New != null)
            {
                var current = _w11New.GetCurrentDesktop();
                if (current is null) return false;
                try
                {
                    int hr = _w11New.GetAdjacentDesktop(current, offset, out var target);
                    if (hr != 0 || target is null) return false;
                    try { _w11New.SwitchDesktopAndMoveForegroundView(target); return true; }
                    finally { Marshal.ReleaseComObject(target); }
                }
                finally { Marshal.ReleaseComObject(current); }
            }
            if (_w11Last != null)
            {
                var current = _w11Last.GetCurrentDesktop();
                if (current is null) return false;
                try
                {
                    int hr = _w11Last.GetAdjacentDesktop(current, offset, out var target);
                    if (hr != 0 || target is null) return false;
                    try { _w11Last.SwitchDesktop(target); return true; }
                    finally { Marshal.ReleaseComObject(target); }
                }
                finally { Marshal.ReleaseComObject(current); }
            }
            if (_w10 != null)
            {
                var current = _w10.GetCurrentDesktop();
                if (current is null) return false;
                try
                {
                    int hr = _w10.GetAdjacentDesktop(current, offset, out var target);
                    if (hr != 0 || target is null) return false;
                    try { _w10.SwitchDesktop(target); return true; }
                    finally { Marshal.ReleaseComObject(target); }
                }
                finally { Marshal.ReleaseComObject(current); }
            }
            return false;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return false;
        }
    }

    /// <summary>Crée un nouveau bureau après le bureau courant ; retourne son GUID.</summary>
    public Guid CreateDesktop()
    {
        try
        {
            if (_w11New != null)
            {
                var vd = _w11New.CreateDesktop();
                if (vd is null) return Guid.Empty;
                try { return vd.GetId(); }
                finally { Marshal.ReleaseComObject(vd); }
            }
            if (_w11Last != null)
            {
                var vd = _w11Last.CreateDesktop();
                if (vd is null) return Guid.Empty;
                try { return vd.GetId(); }
                finally { Marshal.ReleaseComObject(vd); }
            }
            if (_w10 != null)
            {
                var vd = _w10.CreateDesktop();
                if (vd is null) return Guid.Empty;
                try { return vd.GetId(); }
                finally { Marshal.ReleaseComObject(vd); }
            }
            return Guid.Empty;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return Guid.Empty;
        }
    }

    /// <summary>
    /// Supprime le bureau <paramref name="id"/> ; les fenêtres résiduelles sont
    /// déplacées sur <paramref name="fallbackId"/>.
    /// </summary>
    public bool RemoveDesktop(Guid id, Guid fallbackId)
    {
        try
        {
            var target = FindDesktop(id);
            var fallback = FindDesktop(fallbackId);
            if (target is null || fallback is null)
            {
                if (target is not null) Marshal.ReleaseComObject(target);
                if (fallback is not null) Marshal.ReleaseComObject(fallback);
                return false;
            }

            try
            {
                if (_w11New != null && target is IVirtualDesktop t112 && fallback is IVirtualDesktop f112)
                { _w11New.RemoveDesktop(t112, f112); return true; }
                if (_w11Last != null && target is IVirtualDesktop t11 && fallback is IVirtualDesktop f11)
                { _w11Last.RemoveDesktop(t11, f11); return true; }
                if (_w10 != null && target is IVirtualDesktop10 t10 && fallback is IVirtualDesktop10 f10)
                { _w10.RemoveDesktop(t10, f10); return true; }
                return false;
            }
            finally
            {
                Marshal.ReleaseComObject(target);
                Marshal.ReleaseComObject(fallback);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return false;
        }
    }

    /// <summary>Déplace la fenêtre <paramref name="hwnd"/> vers le bureau <paramref name="desktopId"/>.</summary>
    public bool MoveWindowToDesktop(IntPtr hwnd, Guid desktopId)
    {
        if (hwnd == IntPtr.Zero || desktopId == Guid.Empty) return false;

        // 1) Passage préféré : API officielle (documentée).
        if (_official is not null)
        {
            try
            {
                var id = desktopId;
                _official.MoveWindowToDesktop(hwnd, ref id);
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Info($"Déplacement fenêtre via API officielle impossible ({ex.Message}), bascule API interne.");
            }
        }

        // 2) Fallback : API interne via IApplicationView.
        if (_viewCollection is not null && IsInternalApiAvailable)
        {
            try
            {
                _viewCollection.GetViewForHwnd(hwnd, out var view);
                if (view is null) return false;

                var target = FindDesktop(desktopId);
                if (target is null)
                {
                    Marshal.ReleaseComObject(view);
                    return false;
                }

                try
                {
                    if (_w11New != null && target is IVirtualDesktop d112) { _w11New.MoveViewToDesktop(view, d112); return true; }
                    if (_w11Last != null && target is IVirtualDesktop d11) { _w11Last.MoveViewToDesktop(view, d11); return true; }
                    if (_w10 != null && target is IVirtualDesktop10 d10) { _w10.MoveViewToDesktop(view, d10); return true; }
                    return false;
                }
                finally
                {
                    Marshal.ReleaseComObject(view);
                    Marshal.ReleaseComObject(target);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error(ex);
            }
        }

        return false;
    }

    /// <summary>La fenêtre est-elle sur le bureau courant ? (requiert l'API officielle)</summary>
    public bool IsWindowOnCurrentDesktop(IntPtr hwnd)
    {
        if (_official is null || hwnd == IntPtr.Zero) return false;
        try
        {
            return _official.IsWindowOnCurrentVirtualDesktop(hwnd);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return false;
        }
    }

    /// <summary>GUID du bureau sur lequel se trouve la fenêtre.</summary>
    public Guid GetWindowDesktopId(IntPtr hwnd)
    {
        if (_official is null || hwnd == IntPtr.Zero) return Guid.Empty;
        try
        {
            return _official.GetWindowDesktopId(hwnd);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return Guid.Empty;
        }
    }

    /// <summary>RCW du bureau correspondant à <paramref name="id"/>, ou null.</summary>
    private object? FindDesktop(Guid id)
    {
        var desktops = GetDesktopsArray();
        if (desktops is null) return null;
        try
        {
            desktops.GetCount(out var count);
            for (var i = 0; i < count; i++)
            {
                var iid = DesktopGuid;
                desktops.GetAt(i, ref iid, out object obj);
                if (obj is not null && GetId(obj) == id)
                    return obj;
            }
        }
        finally
        {
            Marshal.ReleaseComObject(desktops);
        }

        return null;
    }
// @@INIT@@

    /// <summary>
    /// Initialise les accès au Shell : API interne (IVirtualDesktopManagerInternal
    /// du schéma correspondant au build Windows), collection de vues et API
    /// officielle. Résolution par CLSID_ImmersiveShell + IServiceProvider10,
    /// comme le fait le Shell lui-même (portage MScholtes/VirtualDesktop).
    /// </summary>
    public bool Initialize()
    {
        try
        {
            int build = NativeMethods.GetWindowsBuildNumber();
            AppLog.Info($"Windows : {Environment.OSVersion} (build {build}).");

            var immersiveType = Type.GetTypeFromCLSID(Guids.CLSID_ImmersiveShell);
            if (immersiveType is null)
            {
                AppLog.Error(new Exception("CLSID_ImmersiveShell introuvable (Windows trop ancien ?)."));
                return false;
            }

            var shellInstance = Activator.CreateInstance(immersiveType);
            if (shellInstance is IServiceProvider10 provider)
            {
                if (build >= 26100)
                    _w11New = TryQueryInternal<IVirtualDesktopManagerInternal24H2>(provider, "11 24H2");
                else if (build >= 22000)
                    _w11Last = TryQueryInternal<IVirtualDesktopManagerInternal>(provider, "11");
                else if (build == 0)
                {
                    // RtlGetVersion a échoué : on tente 24H2 puis 11, puis 10.
                    _w11New = TryQueryInternal<IVirtualDesktopManagerInternal24H2>(provider, "11 24H2 (build inconnu)");
                    if (_w11New is null)
                        _w11Last = TryQueryInternal<IVirtualDesktopManagerInternal>(provider, "11 (build inconnu)");
                }

                // En dernier recours (y compris si le schéma 11 n'a pas abouti) :
                // schéma Windows 10. Chaque tentative est blindée par try/catch.
                if (_w11Last is null && _w11New is null)
                    _w10 = TryQueryInternal<IVirtualDesktopManagerInternal10>(provider, "10 (fallback)");

                ResolveViewCollection(provider);
            }
            else
            {
                AppLog.Error(new Exception("Shell immersif sans IServiceProvider10 (Windows 10 < 1809 ?)."));
            }

            // API officielle (documentée) : fallback + vérification par fenêtre.
            var officialType = Type.GetTypeFromCLSID(Guids.CLSID_VirtualDesktopManager);
            if (officialType is not null)
            {
                try
                {
                    _official = Activator.CreateInstance(officialType) as IVirtualDesktopManager;
                }
                catch (Exception ex)
                {
                    AppLog.Error(ex);
                }
            }

            AppLog.Info($"Bureaux virtuels → interne={IsInternalApiAvailable}, officiel={IsOfficialApiAvailable}.");
            return IsInternalApiAvailable || IsOfficialApiAvailable;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return false;
        }
    }

    private T? TryQueryInternal<T>(IServiceProvider10 provider, string label) where T : class
    {
        try
        {
            var svc = Guids.CLSID_VirtualDesktopManagerInternal;
            var riid = typeof(T).GUID;
            int hr = provider.QueryService(ref svc, ref riid, out object? unk);
            if (hr != 0 || unk is null)
            {
                AppLog.Info($"Schéma COM Windows {label} : QueryService hr=0x{hr:X8}.");
                return null;
            }
            return unk as T;
        }
        catch (Exception ex)
        {
            AppLog.Info($"Schéma COM Windows {label} indisponible : {ex.Message}");
            return null;
        }
    }

    private void ResolveViewCollection(IServiceProvider10 provider)
    {
        try
        {
            var svc = typeof(IApplicationViewCollection).GUID;
            int hr = provider.QueryService(ref svc, ref svc, out object? unk);
            if (hr != 0 || unk is null)
            {
                AppLog.Info($"IApplicationViewCollection : QueryService hr=0x{hr:X8}.");
                return;
            }
            _viewCollection = unk as IApplicationViewCollection;
        }
        catch (Exception ex)
        {
            AppLog.Info($"IApplicationViewCollection indisponible : {ex.Message}");
        }
    }

    private void DisposeInternal()
    {
        if (_disposed) return;
        _disposed = true;

        static void Release(object? rcw)
        {
            if (rcw is not null && Marshal.IsComObject(rcw))
                Marshal.ReleaseComObject(rcw);
        }

        Release(_w11Last);
        Release(_w11New);
        Release(_w10);
        Release(_viewCollection);
        Release(_official);
        _w11Last = null;
        _w11New = null;
        _w10 = null;
        _viewCollection = null;
        _official = null;
    }

    public void Dispose() => DisposeInternal();
}