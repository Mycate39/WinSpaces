namespace WinSpaces.Native;

// ---------------------------------------------------------------------------
// WinSpaces — Constantes Win32 (messages, hooks, raw input).
// ---------------------------------------------------------------------------

internal static class Win32Messages
{
    /// <summary>Raccourci global déclenché (RegisterHotKey).</summary>
    public const int WM_HOTKEY = 0x0312;
    /// <summary>Données Raw Input disponibles pour un dispositif.</summary>
    public const int WM_INPUT = 0x00FF;
    /// <summary>Un dispositif Raw Input a été ajouté ou retiré.</summary>
    public const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
    public const int WM_MOUSEWHEEL = 0x020A;
    public const int WM_MOUSEHWHEEL = 0x020E;
    public const int WM_QUERYENDSESSION = 0x0011;
    public const int WM_QUIT = 0x0012;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
}

internal static class HookIds
{
    /// <summary>Hook global clavier bas niveau (WH_KEYBOARD_LL).</summary>
    public const int WH_KEYBOARD_LL = 13;
    /// <summary>Hook global souris bas niveau (WH_MOUSE_LL).</summary>
    public const int WH_MOUSE_LL = 14;
}

internal static class KeyboardKeys
{
    public const uint VK_LEFT = 0x25;
    public const uint VK_UP = 0x26;
    public const uint VK_RIGHT = 0x27;
    public const uint VK_DOWN = 0x28;
    public const uint VK_N = 0x4E;
    public const uint VK_W = 0x57;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_NOREPEAT = 0x4000;
}

internal static class WinEventConstants
{
    /// <summary>L'application au premier plan a changé.</summary>
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    public const uint MONITOR_DEFAULTTONEAREST = 0x0002;
}

internal static class RawInputConstants
{
    public const uint RIM_TYPE_MOUSE = 0;
    public const uint RIM_TYPE_KEYBOARD = 1;
    public const uint RIM_TYPE_HID = 2;
    public const uint RID_INPUT = 0x10000003;
    public const uint RIDI_DEVICEINFO = 0x2000000B;
    public const uint RIDI_DEVICENAME = 0x20000007;
    public const uint RIDI_PREPARSEDDATA = 0x20000005;
    public const uint RIDEV_INPUTSINK = 0x00000100;
    public const uint RIDEV_DEVNOTIFY = 0x00002000;
    public const ushort USAGE_PAGE_DIGITIZER = 0x0D;
    public const ushort USAGE_PAGE_GENERIC_DESKTOP = 0x01;
    public const ushort USAGE_TOUCHPAD = 0x05;        // Digitizer : pavé tactile
    public const ushort USAGE_TOUCH_SCREEN = 0x04;    // Digitizer : écran tactile

    // Usages « Windows Precision Touchpad » (page Digitizer 0x0D).
    public const ushort USAGE_X = 0x30;               // position absolue X d'un contact
    public const ushort USAGE_Y = 0x31;               // position absolue Y d'un contact
    public const ushort USAGE_TIP_SWITCH = 0x42;      // contact posé (bouton)
    public const ushort USAGE_CONTACT_IDENTIFIER = 0x51; // slot du contact
    public const ushort USAGE_CONTACT_COUNT = 0x54;   // nombre de contacts actifs
    public const ushort USAGE_CONTACT_COUNT_MAX = 0x55;
    public const ushort USAGE_SCAN_TIME = 0x56;
    public const ushort USAGE_CONFIDENCE = 0x47;
    public const ushort USAGE_WIDTH = 0x48;
    public const ushort USAGE_HEIGHT = 0x49;

    // HidP_* (HID parser).
    public const uint HIDP_INPUT = 0;
    public const uint HIDP_STATUS_SUCCESS = 0x00110000;
}