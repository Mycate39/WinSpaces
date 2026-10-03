using System.Runtime.InteropServices;
using System.Text;
using WinSpaces.Desktops;
using WinSpaces.Native;
using WinSpaces.Diagnostics;


namespace WinSpaces.Services;

/// <summary>
/// Fenêtre "plein écran" → "espace dédié" à la manière de macOS.
/// Détecte les apps dont la fenêtre couvre tout le moniteur (plein écran),
/// leur crée un bureau virtuel dédié et y bascule. À la sortie du plein écran,
/// supprime l'espace dédié et revient à l'espace d'origine.
/// </summary>
internal sealed class FullscreenSpaceManager : HealthCheckableBase, IDisposable
{
    // IHealthCheckable Implementation
    public override string ComponentName => "Fullscreen & Maximized Manager";
    public override bool IsHealthy => _foregroundHook.IsActive && _locationHook.IsActive;
    public override string StatusMessage => IsHealthy ? "Actif : hook événements système opérationnel" : "Inactif : hook non initialisé";
    private const int PollMs = 600;
    private const int MinGapMs = 1100;
    private const int GeoTol = 6;

    private static readonly HashSet<string> ExcludedClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow"
    };

    private readonly VirtualDesktopService _vds;
    private readonly AppOptions _options;
    // Deux hooks ciblés : un seul hook FOREGROUND..LOCATIONCHANGE (0x0003..0x800B)
    // abonnait le processus à des dizaines de milliers de types d'événements
    // (création, focus, nom, valeur… de tous les objets du système).
    private readonly WindowEventHook _foregroundHook = new();
    private readonly WindowEventHook _locationHook = new();
    private readonly System.Windows.Forms.Timer _poll;
    private readonly Dictionary<IntPtr, SpaceSession> _sessions = new();
    private DateTime _lastActionUtc = DateTime.MinValue;

    // Les appels COM vers l'Explorer pompent les messages (STA) : un WinEvent
    // peut alors ré-entrer dans Eval avant que la session ne soit enregistrée,
    // et créer un second bureau pour la même fenêtre.
    private bool _evaluating;

    public FullscreenSpaceManager(VirtualDesktopService vds, AppOptions options)
    {
        _vds = vds;
        _options = options;
        _poll = new System.Windows.Forms.Timer { Interval = PollMs };
        _poll.Tick += (_, _) => Check();
    }

    public event EventHandler<SpaceSession>? SpaceCreated;
    public event EventHandler<SpaceSession>? SpaceDestroyed;

    public void Start()
    {
        // On écoute le changement de focus et les changements de position/taille (maximisation)
        _foregroundHook.Start(WinEventConstants.EVENT_SYSTEM_FOREGROUND, WinEventConstants.EVENT_SYSTEM_FOREGROUND, OnWinEvent);
        _locationHook.Start(WinEventConstants.EVENT_OBJECT_LOCATIONCHANGE, WinEventConstants.EVENT_OBJECT_LOCATIONCHANGE, OnWinEvent);
        _poll.Start();
        AppLog.Info("FullscreenSpaceManager démarré (Plein écran & Maximisation).");
    }

    private void OnWinEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Callback natif : une exception qui s'en échapperait tuerait le processus.
        try
        {
            if (idObject != 0) return; // OBJID_WINDOW uniquement (pas curseur, caret…)
            // Seule la fenêtre au premier plan peut entrer en plein écran « à la
            // macOS » ; sans ce filtre, une fenêtre maximisée en arrière-plan qui
            // bouge déclenchait la création d'un bureau.
            if (ev == WinEventConstants.EVENT_OBJECT_LOCATIONCHANGE && hwnd != NativeMethods.GetForegroundWindow())
                return;
            Eval(hwnd);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }

    private void Check()
    {
        // Fenêtres isolées fermées entre-temps : sans ce balayage, leur session
        // (et le bureau dédié) restaient orphelins jusqu'à la fin du processus.
        if (_evaluating) return;
        _evaluating = true;
        try
        {
            foreach (var dead in _sessions.Values.Where(x => !NativeMethods.IsWindow(x.WindowHandle)).ToList())
                End(dead);
        }
        finally
        {
            _evaluating = false;
        }

        Eval(NativeMethods.GetForegroundWindow());
    }

    private void Eval(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !_vds.IsInternalApiAvailable || _evaluating) return;

        _evaluating = true;
        try
        {
            bool currentlyIsolated = _sessions.TryGetValue(hwnd, out var s);
            if (currentlyIsolated && !IsEligible(hwnd))
            {
                End(s!);
                return;
            }

            if (!currentlyIsolated && _options.FullscreenSpacesEnabled && IsEligible(hwnd))
            {
                // Éviter de traiter si l'action est trop récente (anti-rebond)
                if ((DateTime.UtcNow - _lastActionUtc).TotalMilliseconds < MinGapMs) return;
                Begin(hwnd);
            }
        }
        finally
        {
            _evaluating = false;
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
            // Si on est toujours sur le bureau dédié (bascule impossible),
            // RemoveDesktop refuse id == fallback : le bureau est simplement conservé.
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

    public void Dispose()
    {
        _poll.Stop();
        _poll.Dispose();
        _foregroundHook.Dispose();
        _locationHook.Dispose();
    }
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