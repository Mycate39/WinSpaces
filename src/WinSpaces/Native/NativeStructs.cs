using System.Runtime.InteropServices;

namespace WinSpaces.Native;

// ---------------------------------------------------------------------------
// WinSpaces — Structures et délégués Win32.
// ---------------------------------------------------------------------------

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSLLHOOKSTRUCT
{
    public POINT pt;
    public uint mouseData;   // HIWORD = delta de molette
    public uint flags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT
{
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MONITORINFO
{
    public uint cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTDEVICE
{
    public ushort usUsagePage;
    public ushort usUsage;
    public uint dwFlags;
    public IntPtr hwndTarget;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTDEVICELIST
{
    public IntPtr hDevice;
    public uint dwType;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RID_DEVICE_INFO_MOUSE
{
    public uint dwId;
    public uint dwNumberOfButtons;
    public uint dwSampleRate;
    public bool fHasHorizontalWheel;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RID_DEVICE_INFO_KEYBOARD
{
    public uint dwType;
    public uint dwSubType;
    public uint dwKeyboardMode;
    public uint dwNumberOfFunctionKeys;
    public uint dwNumberOfIndicators;
    public uint dwNumberOfKeysTotal;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RID_DEVICE_INFO_HID
{
    public uint dwVendorId;
    public uint dwProductId;
    public uint dwVersionNumber;
    public ushort usUsagePage;
    public ushort usUsage;
}

[StructLayout(LayoutKind.Explicit)]
internal struct RID_DEVICE_INFO
{
    [FieldOffset(0)] public uint cbSize;
    [FieldOffset(4)] public uint dwType;
    [FieldOffset(8)] public RID_DEVICE_INFO_MOUSE mouse;
    [FieldOffset(8)] public RID_DEVICE_INFO_KEYBOARD keyboard;
    [FieldOffset(8)] public RID_DEVICE_INFO_HID hid;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTHEADER
{
    public uint dwType;
    public uint dwSize;
    public IntPtr hDevice;
    public IntPtr wParam;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWHID
{
    public uint dwSizeHid;
    public uint dwCount;
    // Les octets du rapport HID (bRawData) suivent, hors structure.
}

// ---------------------------------------------------------------------------
// Délégués
// ---------------------------------------------------------------------------

/// <summary>Callback des événements système (SetWinEventHook).</summary>
internal delegate void WinEventDelegate(
    IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
    int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

/// <summary>Procédure des hooks bas niveau (SetWindowsHookEx).</summary>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

/// <summary>Événement de molette (souris ou trackpad en mode "héritage").</summary>
internal sealed class MouseWheelEventArgs : EventArgs
{
    public MouseWheelEventArgs(int x, int y, int delta, bool isHorizontal, uint time)
    {
        X = x;
        Y = y;
        Delta = delta;
        IsHorizontal = isHorizontal;
        Time = time;
    }

    public int X { get; }
    public int Y { get; }
    public int Delta { get; }
    public bool IsHorizontal { get; }
    public uint Time { get; }
}