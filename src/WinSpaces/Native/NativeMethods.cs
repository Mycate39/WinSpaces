using System.Runtime.InteropServices;
using System.Text;

namespace WinSpaces.Native;

// ---------------------------------------------------------------------------
// WinSpaces — P/Invoke Win32 (user32/kernel32).
// ---------------------------------------------------------------------------

internal static class NativeMethods
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";
    private const string Hid = "hid.dll";
    private const string NtDll = "ntdll.dll";

    /// <summary>Numéro de build du vrai Windows (RtlGetVersion ignore la compatibilité).</summary>
    internal static int GetWindowsBuildNumber()
    {
        var osvi = new OSVERSIONINFOEX
        {
            dwOSVersionInfoSize = (uint)Marshal.SizeOf<OSVERSIONINFOEX>()
        };
        if (RtlGetVersion(ref osvi) != 0) return 0;
        return (int)osvi.dwBuildNumber;
    }

    // --- Fenêtres & moniteurs ----------------------------------------------
    [DllImport(User32, SetLastError = true)]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport(User32, SetLastError = true)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport(User32, SetLastError = true)]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport(User32, SetLastError = true)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    // --- RtlGetVersion ------------------------------------------------------
    [DllImport(NtDll, CharSet = CharSet.Unicode)]
    internal static extern int RtlGetVersion(ref OSVERSIONINFOEX lpVersionInformation);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport(User32, SetLastError = true)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport(User32, SetLastError = true)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport(User32, SetLastError = true)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport(User32, SetLastError = true)]
    [DllImport(User32, SetLastError = true)]
    internal static extern bool IsIconic(IntPtr hWnd);


    internal static extern bool IsZoomed(IntPtr hWnd);

    [DllImport(User32, SetLastError = true)]
    internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport(User32, SetLastError = true)]
    internal static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

    [DllImport(User32, SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport(User32, SetLastError = true)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    // --- Événements système (SetWinEventHook) -------------------------------
    [DllImport(User32, SetLastError = true)]
    internal static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate pfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport(User32, SetLastError = true)]
    internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    // --- Hooks bas niveau (SetWindowsHookEx) --------------------------------
    [DllImport(User32, SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport(User32, SetLastError = true)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport(User32, SetLastError = true)]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport(Kernel32, CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string lpModuleName);

    // --- Raccourcis globaux --------------------------------------------------
    [DllImport(User32, SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport(User32, SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // --- Raw Input (Précision Touchpad) --------------------------------------
    [DllImport(User32, SetLastError = true)]
    internal static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[]? pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, StringBuilder pData, ref uint pcbSize);

    [DllImport(User32, SetLastError = true)]
    internal static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport(User32, SetLastError = true)]
    internal static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    // --- HID parser (hid.dll) ------------------------------------------------
    [DllImport(Hid)]
    internal static extern uint HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

    [DllImport(Hid)]
    internal static extern uint HidP_GetUsageValue(
        uint reportType, ushort usagePage, ushort linkCollection, ushort usage,
        out uint usageValue, IntPtr preparsedData, IntPtr report, uint reportLength);

    [DllImport(Hid)]
    internal static extern uint HidP_GetUsages(
        uint reportType, ushort usagePage, ushort linkCollection,
        [Out] ushort[] usageList, ref uint usageLength, IntPtr preparsedData, IntPtr report, uint reportLength);
}