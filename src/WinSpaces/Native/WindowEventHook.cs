namespace WinSpaces.Native;

/// <summary>
/// Wrapper autour de SetWinEventHook : abonne le processus à des événements
/// système Win32 (changement de fenêtre au premier plan, déplacements…).
/// Le délégué géré doit rester vivant : il est stocké en champ.
/// </summary>
internal sealed class WindowEventHook : IDisposable
{
    private WinEventDelegate? _callback;
    private IntPtr _hook = IntPtr.Zero;

    public bool IsActive => _hook != IntPtr.Zero;

    public void Start(uint eventMin, uint eventMax, WinEventDelegate callback)
    {
        if (_hook != IntPtr.Zero) return;

        _callback = callback;
        _hook = NativeMethods.SetWinEventHook(
            eventMin,
            eventMax,
            IntPtr.Zero,
            _callback,
            0,
            0,
            WinEventConstants.WINEVENT_OUTOFCONTEXT | WinEventConstants.WINEVENT_SKIPOWNPROCESS);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }

        _callback = null;
    }
}