using System.Runtime.InteropServices;

namespace WinSpaces.Native;

/// <summary>
/// Hook global de clavier (WH_KEYBOARD_LL). Utile pour des combinaisons
/// supplémentaires à l'avenir (les raccourcis principaux passent par
/// RegisterHotKey, plus économique).
/// </summary>
internal sealed class KeyboardHook : LowLevelHookBase
{
    public KeyboardHook() : base(HookIds.WH_KEYBOARD_LL) { }

    /// <summary>Levé à chaque pression de touche (vkCode).</summary>
    public event EventHandler<uint>? KeyDown;

    protected override IntPtr Process(int nCode, IntPtr wParam, IntPtr lParam)
    {
        int msg = unchecked((int)(long)wParam);
        if (msg == Win32Messages.WM_KEYDOWN)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            KeyDown?.Invoke(this, data.vkCode);
        }

        return NativeMethods.CallNextHookEx(HookHandle, nCode, wParam, lParam);
    }
}