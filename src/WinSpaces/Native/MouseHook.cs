using System.Runtime.InteropServices;

namespace WinSpaces.Native;

/// <summary>
/// Hook global de souris (WH_MOUSE_LL) : surveille la molette (verticale et
/// horizontale) des souris et des trackpads "héritage" (non Précision).
/// </summary>
internal sealed class MouseHook : LowLevelHookBase
{
    public MouseHook() : base(HookIds.WH_MOUSE_LL) { }

    /// <summary>Levé à chaque événement de molette (delta signé, ±120/cran).</summary>
    public event EventHandler<MouseWheelEventArgs>? MouseWheel;

    protected override IntPtr Process(int nCode, IntPtr wParam, IntPtr lParam)
    {
        int msg = unchecked((int)(long)wParam);
        if (msg == Win32Messages.WM_MOUSEWHEEL || msg == Win32Messages.WM_MOUSEHWHEEL)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int delta = (short)((data.mouseData >> 16) & 0xFFFF);
            MouseWheel?.Invoke(this, new MouseWheelEventArgs(
                data.pt.X, data.pt.Y, delta,
                msg == Win32Messages.WM_MOUSEHWHEEL,
                data.time));
        }

        return NativeMethods.CallNextHookEx(HookHandle, nCode, wParam, lParam);
    }
}