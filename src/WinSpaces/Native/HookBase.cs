namespace WinSpaces.Native;

/// <summary>
/// Base commune des hooks bas niveau (WH_MOUSE_LL / WH_KEYBOARD_LL).
/// Le délégué géré est conservé en champ pour éviter qu'il soit ramassé par
/// le GC pendant que le hook est installé (cause classique de crash).
/// </summary>
internal abstract class LowLevelHookBase : IDisposable
{
    private readonly int _hookId;
    private IntPtr _hook = IntPtr.Zero;
    private LowLevelHookProc? _managedProc;

    protected LowLevelHookBase(int hookId) => _hookId = hookId;

    public bool IsInstalled => _hook != IntPtr.Zero;

    protected IntPtr HookHandle => _hook;

    /// <summary>Installe le hook dans la chaîne globale du système.</summary>
    public bool Install()
    {
        if (_hook != IntPtr.Zero) return true;
        _managedProc = OnHookProc;
        _hook = NativeMethods.SetWindowsHookEx(_hookId, _managedProc, IntPtr.Zero, 0);
        return _hook != IntPtr.Zero;
    }

    /// <summary>
    /// Traite un message du hook. Doit retourner le résultat de
    /// CallNextHookEx pour laisser passer l'événement.
    /// </summary>
    protected abstract IntPtr Process(int nCode, IntPtr wParam, IntPtr lParam);

    private IntPtr OnHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        return nCode >= 0
            ? Process(nCode, wParam, lParam)
            : NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        _managedProc = null;
    }
}