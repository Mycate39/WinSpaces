using System.Runtime.InteropServices;
using WinSpaces.Desktops.Interop;
using WinSpaces.Services;

namespace WinSpaces.Desktops;

/// <summary>
/// Service de haut niveau autour des bureaux virtuels Windows.
/// S'appuie sur l'API interne « explorateur » (IVirtualDesktopManagerInternal
/// résolu via IServiceProvider10/CLSID_ImmersiveShell) et sur l'API OFFICIELLE
/// IVirtualDesktopManager en fallback et pour les opérations par fenêtre.
/// </summary>
internal sealed class VirtualDesktopService : IDisposable
{
    private IVirtualDesktopManagerInternal? _internal;
    private IApplicationViewCollection? _viewCollection;
    private IVirtualDesktopManager? _official;
    private bool _disposed;

    /// <summary>L'API interne (création/suppression/bascule) est disponible.</summary>
    public bool IsInternalApiAvailable => _internal != null;

    /// <summary>L'API officielle est disponible.</summary>
    public bool IsOfficialApiAvailable => _official != null;

    /// <summary>Uniquement l'API officielle (fonctionnalités réduites).</summary>
    public bool IsFallbackOnly => _internal == null && _official != null;

    // @@VMETHODS@@

    /// <summary>GUID de tous les bureaux existants, dans l'ordre.</summary>
    public IReadOnlyList<Guid> GetDesktopIds()
    {
        var result = new List<Guid>();
        var mgr = _internal;
        if (mgr is null) return result;

        try
        {
            mgr.GetDesktops(out IObjectArray desktops);
            try
            {
                desktops.GetCount(out var count);
                for (var i = 0; i < count; i++)
                {
                    var iid = typeof(IVirtualDesktop).GUID;
                    desktops.GetAt(i, ref iid, out object obj);
                    if (obj is IVirtualDesktop vd)
                        result.Add(vd.GetId());
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

    public int Count
    {
        get
        {
            try
            {
                return _internal?.GetCount() ?? 0;
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
                if (_internal is null) return Guid.Empty;
                var current = _internal.GetCurrentDesktop();
                try
                {
                    return current is null ? Guid.Empty : current.GetId();
                }
                finally
                {
                    if (current is not null)
                        Marshal.ReleaseComObject(current);
                }
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

    // @@VMETHODS2@@

    /// <summary>Bascule vers le bureau d'identifiant <paramref name="id"/>.</summary>
    public bool SwitchToDesktop(Guid id)
    {
        if (_internal is null || id == Guid.Empty) return false;

        try
        {
            var vd = FindDesktop(id);
            if (vd is null) return false;
            try
            {
                _internal.SwitchDesktop(vd);
                return true;
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
        var mgr = _internal;
        if (mgr is null) return false;
        if (offset != -1 && offset != 1) return false;

        try
        {
            var current = mgr.GetCurrentDesktop();
            try
            {
                int hr = mgr.GetAdjacentDesktop(current, offset, out IVirtualDesktop target);
                if (hr != 0 || target is null) return false;

                try
                {
                    mgr.SwitchDesktop(target);
                    return true;
                }
                finally
                {
                    Marshal.ReleaseComObject(target);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(current);
            }
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
        if (_internal is null) return Guid.Empty;

        try
        {
            var vd = _internal.CreateDesktop();
            if (vd is null) return Guid.Empty;
            try
            {
                return vd.GetId();
            }
            finally
            {
                Marshal.ReleaseComObject(vd);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return Guid.Empty;
        }
    }

    // @@VMETHODS3@@

    /// <summary>
    /// Supprime le bureau <paramref name="id"/> ; les fenêtres résiduelles sont
    /// déplacées sur <paramref name="fallbackId"/>.
    /// </summary>
    public bool RemoveDesktop(Guid id, Guid fallbackId)
    {
        var mgr = _internal;
        if (mgr is null) return false;

        try
        {
            var target = FindDesktop(id);
            var fallback = FindDesktop(fallbackId);
            if (target is null || fallback is null) return false;

            try
            {
                mgr.RemoveDesktop(target, fallback);
                return true;
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
        if (_internal is not null && _viewCollection is not null)
        {
            try
            {
                _viewCollection.GetViewForHwnd(hwnd, out var view);
                if (view is null) return false;

                var target = FindDesktop(desktopId);
                if (target is null) return false;

                try
                {
                    _internal.MoveViewToDesktop(view, target);
                    return true;
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

    private IVirtualDesktop? FindDesktop(Guid id)
    {
        var mgr = _internal;
        if (mgr is null) return null;

        mgr.GetDesktops(out IObjectArray desktops);
        try
        {
            desktops.GetCount(out var count);
            for (var i = 0; i < count; i++)
            {
                var iid = typeof(IVirtualDesktop).GUID;
                desktops.GetAt(i, ref iid, out object obj);
                if (obj is IVirtualDesktop vd && vd.GetId() == id)
                    return vd;
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
    /// Initialise les accès au Shell : API interne (IVirtualDesktopManagerInternal),
    /// collection de vues et API officielle. Résolution par CLSID_ImmersiveShell +
    /// IServiceProvider10, comme le fait le Shell lui-même.
    /// </summary>
    public bool Initialize()
    {
        try
        {
            var immersiveType = Type.GetTypeFromCLSID(Guids.CLSID_ImmersiveShell);
            if (immersiveType is null)
            {
                AppLog.Error(new Exception("CLSID_ImmersiveShell introuvable (Windows trop ancien ?)."));
                return false;
            }

            var shellInstance = Activator.CreateInstance(immersiveType);
            if (shellInstance is null)
            {
                AppLog.Error(new Exception("Impossible d'instancier l'ImmersiveShell."));
                return false;
            }

            if (shellInstance is IServiceProvider10 provider)
            {
                // API interne : création/suppression/bascule de bureaux.
                var svcInternal = Guids.CLSID_VirtualDesktopManagerInternal;
                var riidInternal = typeof(IVirtualDesktopManagerInternal).GUID;
                _internal = provider.QueryService(ref svcInternal, ref riidInternal)
                    as IVirtualDesktopManagerInternal;

                // Collection de vues (déplacement de fenêtre par HWND sans l'API officielle).
                var svcViews = typeof(IApplicationViewCollection).GUID;
                _viewCollection = provider.QueryService(ref svcViews, ref svcViews)
                    as IApplicationViewCollection;
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

    private void DisposeInternal()
    {
        if (_disposed) return;
        _disposed = true;

        static void Release(object? rcw)
        {
            if (rcw is not null && Marshal.IsComObject(rcw))
                Marshal.ReleaseComObject(rcw);
        }

        Release(_internal);
        Release(_viewCollection);
        Release(_official);
        _internal = null;
        _viewCollection = null;
        _official = null;
    }

    public void Dispose() => DisposeInternal();
}