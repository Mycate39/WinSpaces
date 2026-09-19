using System.Runtime.InteropServices;
using System.Text;
using WinSpaces.Desktops;
using WinSpaces.Native;

namespace WinSpaces.Services;

/// <summary>
/// Fenêtre "plein écran" → "espace dédié" à la manière de macOS.
/// Détecte les apps dont la fenêtre couvre tout le moniteur (plein écran),
/// leur crée un bureau virtuel dédié et y bascule. À la sortie du plein écran,
/// supprime l'espace dédié et revient à l'espace d'origine.
/// </summary>
internal sealed class FullscreenSpaceManager : IDisposable
{
    private const int PollMs = 600;
    private const int MinGapMs = 1100;
    private const int GeoTol = 6;

    private static readonly HashSet<string> ExcludedClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow"
    };

    private readonly VirtualDesktopService _vds;
    private readonly WindowEventHook _hook = new();
    private readonly System.Windows.Forms.Timer _poll;
    private readonly Dictionary<IntPtr, SpaceSession> _sessions = new();
    private DateTime _lastActionUtc = DateTime.MinValue;

    public FullscreenSpaceManager(VirtualDesktopService vds)
    {
        _vds = vds;
        _poll = new System.Windows.Forms.Timer { Interval = PollMs };
        _poll.Tick += (_, _) => Check();
    }

    public event EventHandler<SpaceSession>? SpaceCreated;
    public event EventHandler<SpaceSession>? SpaceDestroyed;

    public void Start()
    {
        // On écoute le changement de focus et les changements de position/taille (maximisation)
        _hook.Start(WinEventConstants.EVENT_SYSTEM_FOREGROUND, WinEventConstants.EVENT_OBJECT_LOCATIONCHANGE,
            (_, ev, hwnd, idObj, _, _, _) => 
            { 
                if (idObj == 0 && (ev == WinEventConstants.EVENT_SYSTEM_FOREGROUND || ev == WinEventConstants.EVENT_OBJECT_LOCATIONCHANGE))
                    Eval(hwnd); 
            });
        _poll.Start();
        AppLog.Info("FullscreenSpaceManager démarré (Plein écran & Maximisation).");
    }

    private void Check() => Eval(NativeMethods.GetForegroundWindow());

    private void Eval(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !_vds.IsInternalApiAvailable) return;

        bool currentlyIsolated = _sessions.TryGetValue(hwnd, out var s);
        bool shouldBeIsolated = IsEligible(hwnd);

        if (currentlyIsolated && !shouldBeIsolated)
        {
            End(s!);
            return;
        }

        if (!currentlyIsolated && shouldBeIsolated)
        {
            // Éviter de traiter si l'action est trop récente (anti-rebond)
            if ((DateTime.UtcNow - _lastActionUtc).TotalMilliseconds < MinGapMs) return;
            
            // On ne crée pas d'espace si la fenêtre est déjà "seule" ou si elle est sur un bureau qu'on ne gère pas
            // (Note : On pourrait complexifier ici pour vérifier si le bureau actuel contient d'autres fenêtres visibles)
            
            Begin(hwnd);
        }
    }

    private bool IsEligible(IntPtr hwnd)
    {
        if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd)) 
            return false;
            
        if (IsExcluded(hwnd)) return false;

        return IsFullscreen(hwnd) || NativeMethods.IsZoomed(hwnd);
    }

    // @@FSM2@@

    private void Begin(IntPtr hwnd)
    {
        try
        {
            var prev = _vds.CurrentDesktopId;
            var newId = _vds.CreateDesktop();
            if (newId == Guid.Empty) return;

            if (!_vds.MoveWindowToDesktop(hwnd, newId))
            {
                _vds.RemoveDesktop(newId, prev);
                return;
            }
            _vds.SwitchToDesktop(newId);
            _lastActionUtc = DateTime.UtcNow;

            var session = new SpaceSession(hwnd, newId, prev, GetTitle(hwnd), DateTime.UtcNow);
            _sessions[hwnd] = session;
            AppLog.Info($"Plein écran « {session.AppName} » → espace dédié.");
            SpaceCreated?.Invoke(this, session);
        }
        catch (Exception ex) { AppLog.Error(ex); }
    }

    private void End(SpaceSession s)
    {
        _sessions.Remove(s.WindowHandle);
        try
        {
            if (_vds.CurrentDesktopId == s.DesktopId && s.PreviousDesktopId != Guid.Empty)
                _vds.SwitchToDesktop(s.PreviousDesktopId);
            _vds.RemoveDesktop(s.DesktopId, _vds.CurrentDesktopId);
            AppLog.Info($"Sortie plein écran « {s.AppName} ».");
            SpaceDestroyed?.Invoke(this, s);
        }
        catch (Exception ex) { AppLog.Error(ex); }
    }

    private bool IsFullscreen(IntPtr hwnd)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var r)) return false;
        var mon = NativeMethods.MonitorFromWindow(hwnd, WinEventConstants.MONITOR_DEFAULTTONEAREST);
        if (mon == IntPtr.Zero) return false;
        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(mon, ref mi)) return false;
        var f = mi.rcMonitor;
        return r.Left <= f.Left + GeoTol && r.Top <= f.Top + GeoTol
            && r.Right >= f.Right - GeoTol && r.Bottom >= f.Bottom - GeoTol;
    }

    private static bool IsExcluded(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == (uint)Environment.ProcessId) return true;
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return ExcludedClasses.Contains(sb.ToString());
    }

    private static string GetTitle(IntPtr hwnd)
    {
        int len = NativeMethods.GetWindowTextLength(hwnd);
        if (len == 0) return "(sans titre)";
        var sb = new StringBuilder(len + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public void Dispose() { _poll.Stop(); _poll.Dispose(); _hook.Dispose(); }
}

internal sealed class SpaceSession
{
    public IntPtr WindowHandle { get; }
    public Guid DesktopId { get; }
    public Guid PreviousDesktopId { get; }
    public string AppName { get; }
    public DateTime StartedAt { get; }

    public SpaceSession(IntPtr wh, Guid did, Guid prev, string name, DateTime at)
    {
        WindowHandle = wh; DesktopId = did; PreviousDesktopId = prev;
        AppName = name; StartedAt = at;
    }
}