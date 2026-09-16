using System.Runtime.InteropServices;
using System.Text;
using WinSpaces.Native;

namespace WinSpaces.Services;

/// <summary>
/// Watcher Précision Touchpad (Windows 10/11) basé sur Raw Input :
///  1. énumère les dispositifs HID et reconnaît le "Précision Touchpad"
///     (usage page Digitizer 0x0D, usage 0x05) ;
///  2. s'enregistre en récepteur (RIDEV_INPUTSINK) ;
///  3. reçoit WM_INPUT sur la fenêtre message-only et analyse les rapports HID.
///
/// Chaque rapport contient le nombre de contacts ; un balayage horizontal
/// soutenu avec ≥3 contacts déclenche un changement d'espace (macOS utilise
/// 3/4 doigts). Le parsing est best-effort : les fabricants varient dans
/// l'ordre/la taille des champs, d'où des seuils et offsets documentés.
/// </summary>
internal sealed class PrecisionTouchpadWatcher : IDisposable
{
    /// <summary>Nombre minimal de contacts pour activer le swipe.</summary>
    private const int MinContactsForSwipe = 3;

    /// <summary>Seuil de distance cumulée (unités report) pour déclencher.</summary>
    private const int SwipeDeltaThreshold = 60;

    /// <summary>Fenêtre maxi d'un balayage (durée).</summary>
    private static readonly TimeSpan SwipeWindow = TimeSpan.FromMilliseconds(400);

    private readonly MessageWindow? _sourceWindow;

    // État courant du suivi de balayage.
    private long _lastContactsXTotal;
    private int _lastContactCount;
    private long _swipeDeltaAccum;
    private DateTime _swipeStartUtc = DateTime.MinValue;

    private bool _disposed;

    public PrecisionTouchpadWatcher(MessageWindow? sourceWindow)
        => _sourceWindow = sourceWindow;

    /// <summary>true si un Précision Touchpad est présent dans le système.</summary>
    public bool PrecisionTouchpadPresent { get; private set; }

    public event EventHandler<int>? SwipeDetected;

    public void Start()
    {
        if (_sourceWindow is null || _disposed) return;

        PrecisionTouchpadPresent = EnumeratePrecisionTouchpad();

        if (!PrecisionTouchpadPresent)
        {
            AppLog.Info("Précision Touchpad absent : mode momentum (fallback) utilisé.");
            return;
        }

        // Réception des rapports HID du toucher même sans focus (INPUTSINK).
        var device = new RAWINPUTDEVICE
        {
            usUsagePage = RawInputConstants.USAGE_PAGE_DIGITIZER,
            usUsage = RawInputConstants.USAGE_TOUCHPAD,
            dwFlags = RawInputConstants.RIDEV_INPUTSINK | RawInputConstants.RIDEV_DEVNOTIFY,
            hwndTarget = _sourceWindow.Handle
        };

        bool ok = NativeMethods.RegisterRawInputDevices(
            new[] { device }, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        if (!ok)
        {
            AppLog.Error(new InvalidOperationException(
                $"RegisterRawInputDevices échoué (erreur {Marshal.GetLastWin32Error()})."));
            PrecisionTouchpadPresent = false;
            return;
        }

        _sourceWindow.WindowMessage += OnWindowMessage;
        AppLog.Info("Précision Touchpad détecté : Raw Input 3/4 doigts activé.");
    }

    private bool EnumeratePrecisionTouchpad()
    {
        try
        {
            uint count = 0;
            uint result = NativeMethods.GetRawInputDeviceList(null, ref count, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>());
            if (result != 0 || count == 0) return false;

            var devices = new RAWINPUTDEVICELIST[count];
            uint actual = NativeMethods.GetRawInputDeviceList(devices, ref count, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>());

            for (var i = 0; i < actual; i++)
            {
                if (devices[i].dwType != RawInputConstants.RIM_TYPE_HID) continue;

                var info = new RID_DEVICE_INFO();
                uint size = (uint)Marshal.SizeOf<RID_DEVICE_INFO>();
                info.cbSize = size;

                IntPtr buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    Marshal.StructureToPtr(info, buffer, false);
                    uint infoSize = (uint)Marshal.SizeOf<RID_DEVICE_INFO>();
                    uint read = NativeMethods.GetRawInputDeviceInfo(devices[i].hDevice, RawInputConstants.RIDI_DEVICEINFO, buffer, ref infoSize);
                    if (read != uint.MaxValue)
                    {
                        info = Marshal.PtrToStructure<RID_DEVICE_INFO>(buffer);
                        // Précision Touchpad : Digitizer/Touchpad (page 0x0D, usage 0x05)
                        // ou Digitizer/Pavé 4 doigts selon les constructeurs.
                        if (info.hid.usUsagePage == RawInputConstants.USAGE_PAGE_DIGITIZER
                            && (info.hid.usUsage == RawInputConstants.USAGE_TOUCHPAD
                                || info.hid.usUsage == 0x0E))
                        {
                            AppLog.Info($"Précision Touchpad détecté : VID=0x{info.hid.dwVendorId:X4} PID=0x{info.hid.dwProductId:X4}.");
                            return true;
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return false;
        }
    }

    private void OnWindowMessage(object? sender, Message m)
    {
        if (m.Msg == Win32Messages.WM_INPUT)
        {
            ProcessRawInput(m.LParam);
        }
        else if (m.Msg == Win32Messages.WM_INPUT_DEVICE_CHANGE)
        {
            // Un Précision Touchpad a pu être branché/débranché : on ré-évalue.
            AppLog.Info("WM_INPUT_DEVICE_CHANGE : ré-inventaire des dispositifs.");
            PrecisionTouchpadPresent = EnumeratePrecisionTouchpad();
        }
    }

    // @@RAW@@

    private void ProcessRawInput(IntPtr hRawInput)
    {
        try
        {
            // Taille du tampon requise (appel avec buffer nul).
            uint size = 0;
            NativeMethods.GetRawInputData(hRawInput, RawInputConstants.RID_INPUT,
                IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            if (size == 0 || size > 4096) return;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                uint read = NativeMethods.GetRawInputData(hRawInput, RawInputConstants.RID_INPUT,
                    buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
                if (read == uint.MaxValue) return;

                var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
                if (header.dwType != RawInputConstants.RIM_TYPE_HID) return;

                // Le rapport HID suit l'en-tête : [dwSizeHid(4) | dwCount(4) | données...].
                if (size < 8) return;
                uint dwSizeHid = (uint)Marshal.ReadInt32(buffer, 0);
                uint dwCount = (uint)Marshal.ReadInt32(buffer, 4);
                int dataOffset = 8;
                if (dwSizeHid == 0 || dwCount == 0) return;

                for (uint i = 0; i < dwCount; i++)
                {
                    int reportStart = dataOffset + (int)(i * dwSizeHid);
                    if (reportStart + dwSizeHid <= size)
                        ProcessHidReport(buffer, reportStart);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }

    /// <summary>
    /// Analyse d'un rapport de contacts "Précision Touchpad" (best-effort).
    /// Structure générique du rapport de contacts :
    ///   [0]        boutons (1 octet)
    ///   [1..4]     scan time (4 octets)
    ///   [5]        count contacts (1 octet)
    ///   [6]        count max (1 octet)
    ///   [7..]      contacts : id(1) + X(2) + Y(2) en little-endian
    /// Les constructeurs peuvent différer légèrement ; les seuils sont calibrés
    /// pour la plupart des ordinateurs portables modernes.
    /// </summary>
    private void ProcessHidReport(IntPtr buffer, int start)
    {
        int contacts = Marshal.ReadByte(buffer, start + 5);
        if (contacts < MinContactsForSwipe) return;

        // Positions X/Y par contact : id(1) + X(2) little-endian signé.
        long xTotal = 0;
        int cursor = start + 7;
        for (int i = 0; i < contacts && cursor + 5 <= start + 100; i++)
        {
            short x = (short)(Marshal.ReadByte(buffer, cursor + 1)
                              | (Marshal.ReadByte(buffer, cursor + 2) << 8));
            xTotal += x;
            cursor += 5; // 1 (id) + 2 (X) + 2 (Y)
        }

        var now = DateTime.UtcNow;

        // Nouveau balayage ou changement de nombre de doigts : réinitialise.
        if (_lastContactCount == 0 || contacts != _lastContactCount
            || now - _swipeStartUtc > SwipeWindow)
        {
            _lastContactsXTotal = xTotal;
            _lastContactCount = contacts;
            _swipeDeltaAccum = 0;
            _swipeStartUtc = now;
            return;
        }

        _swipeDeltaAccum += xTotal - _lastContactsXTotal;
        _lastContactsXTotal = xTotal;

        if (Math.Abs(_swipeDeltaAccum) < SwipeDeltaThreshold) return;

        int direction = _swipeDeltaAccum > 0 ? -1 : 1;
        _lastContactCount = 0;
        _swipeDeltaAccum = 0;

        AppLog.Info($"Swipe {contacts} doigts → espace {(direction > 0 ? "suivant" : "précédent")}.");
        SwipeDetected?.Invoke(this, direction);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_sourceWindow is not null)
            _sourceWindow.WindowMessage -= OnWindowMessage;
    }
}