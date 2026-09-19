using System.Diagnostics;
using System.Runtime.InteropServices;
using WinSpaces.Native;
using WinSpaces.Diagnostics;


namespace WinSpaces.Services;

/// <summary>
/// Watcher Précision Touchpad (Windows 10/11) basé sur Raw Input + HidP_* :
///  1. énumère les dispositifs HID et reconnaît le « Précision Touchpad »
///     (usage page Digitizer 0x0D, usage 0x05) ;
///  2. récupère les données « preparsed » du rapport HID (RIDI_PREPARSEDDATA) ;
///  3. s'enregistre en récepteur (RIDEV_INPUTSINK) ;
///  4. décode chaque rapport via les API HidP_* (HidP_GetUsageValue/GetUsages)
///     au lieu d'offsets figés — indépendant du constructeur du pavé ;
///  5. un balayage horizontal soutenu avec ≥ 3 contacts déclenche le changement
///     d'espace (comme macOS : un seul déclenchement jusqu'au lever complet des
///     doigts, qui ré-arme le geste).
/// </summary>
internal sealed class PrecisionTouchpadWatcher : IDisposable, IHealthCheckable
{
    // IHealthCheckable Implementation
    public string ComponentName => "Precision Touchpad (Raw Input HID)";
    public bool IsHealthy => PrecisionTouchpadPresent && _preparsedData != IntPtr.Zero;
    public string StatusMessage => GetStatusMessage();

    private string GetStatusMessage()
    {
        if (!PrecisionTouchpadPresent) return "Aucun Precision Touchpad détecté (fallback momentum actif)";
        if (_preparsedData == IntPtr.Zero) return "Touchpad détecté mais preparsed data non initialisée";
        return "Touchpad opérationnel : swipe 3+ doigts actif";
    }

    private const int MaxContacts = 16;

    /// <summary>Nombre minimal de contacts pour activer le swipe.</summary>
    private const int MinContactsForSwipe = 3;

    /// <summary>Delta cumulé (somme des X des contacts) déclenchant le swipe.</summary>
    private const int SwipeDeltaThreshold = 800;

    /// <summary>Garde anti-saut : un écart X supérieur (rewrapping absolu) est ignoré.</summary>
    private const int MaxContactStep = 0x4000;

    private const int MaxButtons = 8;

    /// <summary>Un contact est « suivi » tant que ses rapports arrivent dans cette fenêtre.</summary>
    private static readonly long ContactStaleTicks = MsToTicks(120);

    /// <summary>Sans rapport pendant cette durée, la session de geste est réinitialisée.</summary>
    private static readonly long NewSessionTicks = MsToTicks(200);

    private static long MsToTicks(long ms) => ms * Stopwatch.Frequency / 1000;

    private readonly MessageWindow? _sourceWindow;

    /// <summary>Données « preparsed » du pavé (ownership via HidD_FreePreparsedData).</summary>
    private IntPtr _preparsedData;

    // Compteur de doigts : champ « Contact Count » (0x54) quand le pavé le fournit
    // (obligatoire pour la certification Précision Touchpad), sinon déduit des
    // identifiants de contact encore posés.
    private bool _haveContactCount;
    private int _activeContactCount;

    // Suivi des contacts (par slot) pour mesurer le mouvement horizontal sans
    // dépendre de l'ordre des rapports.
    private readonly HashSet<int> _activeIds = new();
    private readonly long[] _contactX = new long[MaxContacts];
    private readonly long[] _contactTs = new long[MaxContacts];

    private long _deltaAccum;
    private bool _swipeTriggered;
    private long _lastReportTs;
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

        LogHidCaps();

        _sourceWindow.WindowMessage += OnWindowMessage;
        AppLog.Info("Précision Touchpad détecté : Raw Input 3/4 doigts activé.");
    }

    private bool EnumeratePrecisionTouchpad()
    {
        try
        {
            FreePreparsedData();

            uint count = 0;
            uint result = NativeMethods.GetRawInputDeviceList(
                null, ref count, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>());
            if (result != 0 || count == 0) return false;

            var devices = new RAWINPUTDEVICELIST[count];
            uint actual = NativeMethods.GetRawInputDeviceList(
                devices, ref count, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>());

            for (var i = 0; i < actual; i++)
            {
                if (devices[i].dwType != RawInputConstants.RIM_TYPE_HID) continue;

                var info = new RID_DEVICE_INFO
                {
                    cbSize = (uint)Marshal.SizeOf<RID_DEVICE_INFO>()
                };

                IntPtr buffer = Marshal.AllocHGlobal((int)info.cbSize);
                try
                {
                    Marshal.StructureToPtr(info, buffer, false);
                    uint infoSize = info.cbSize;
                    uint read = NativeMethods.GetRawInputDeviceInfo(
                        devices[i].hDevice, RawInputConstants.RIDI_DEVICEINFO, buffer, ref infoSize);
                    if (read == uint.MaxValue) continue;

                    info = Marshal.PtrToStructure<RID_DEVICE_INFO>(buffer);

                    // Précision Touchpad : Digitizer/Touchpad (page 0x0D, usage 0x05)
                    // ou Digitizer/Pavé 4 doigts selon les constructeurs.
                    if (info.hid.usUsagePage == RawInputConstants.USAGE_PAGE_DIGITIZER
                        && (info.hid.usUsage == RawInputConstants.USAGE_TOUCHPAD
                            || info.hid.usUsage == 0x0E))
                    {
                        AppLog.Info($"Précision Touchpad détecté : VID=0x{info.hid.dwVendorId:X4} PID=0x{info.hid.dwProductId:X4}.");
                        _preparsedData = AcquirePreparsedData(devices[i].hDevice);
                        return true;
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

    private IntPtr AcquirePreparsedData(IntPtr hDevice)
    {
        uint size = 0;
        NativeMethods.GetRawInputDeviceInfo(
            hDevice, RawInputConstants.RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);
        if (size == 0) return IntPtr.Zero;

        IntPtr preparsed = Marshal.AllocHGlobal((int)size);
        uint result = NativeMethods.GetRawInputDeviceInfo(
            hDevice, RawInputConstants.RIDI_PREPARSEDDATA, preparsed, ref size);
        if (result == uint.MaxValue || result == 0)
        {
            Marshal.FreeHGlobal(preparsed);
            return IntPtr.Zero;
        }
        return preparsed;
    }

    /// <summary>Journalise les capacités HID du pavé (diagnostic, sans parsing figé).</summary>
    private void LogHidCaps()
    {
        try
        {
            if (_preparsedData == IntPtr.Zero) return;
            if (NativeMethods.HidP_GetCaps(_preparsedData, out HIDP_CAPS caps)
                != RawInputConstants.HIDP_STATUS_SUCCESS)
                return;

            AppLog.Info($"HID : rapport={caps.InputReportByteLength}o, valeurs={caps.NumberInputValueCaps}, boutons={caps.NumberInputButtonCaps}.");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
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
            // Un Précision Touchpad a pu être branché/débranché : on ré-évalue
            // et on recharge les données « preparsed » du nouveau périphérique.
            AppLog.Info("WM_INPUT_DEVICE_CHANGE : ré-inventaire des dispositifs.");
            PrecisionTouchpadPresent = EnumeratePrecisionTouchpad();
        }
    }

    private void ProcessRawInput(IntPtr hRawInput)
    {
        try
        {
            // Taille du tampon requise (appel avec buffer nul).
            uint size = 0;
            NativeMethods.GetRawInputData(hRawInput, RawInputConstants.RID_INPUT,
                IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
                
            AppLog.Info($"WM_INPUT reçu. Handle: 0x{hRawInput:X}. Taille calculée: {size} octets.");
                
            if (size == 0 || size > 4096) return;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                uint read = NativeMethods.GetRawInputData(hRawInput, RawInputConstants.RID_INPUT,
                    buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
                if (read == uint.MaxValue) 
                {
                    AppLog.Info("Erreur GetRawInputData : uint.MaxValue retourné.");
                    return;
                }

                var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
                AppLog.Info($"RAWINPUTHEADER → Type: {header.dwType}, Size: {header.dwSize}, hDevice: 0x{header.hDevice:X}");

                if (header.dwType != RawInputConstants.RIM_TYPE_HID)
                {
                    AppLog.Info($"Rejeté: Le périphérique n'est pas de type HID (Type {header.dwType}).");
                    return;
                }

                // Le rapport HID suit l'en-tête RAWINPUTHEADER :
                //   [RAWINPUTHEADER] + dwSizeHid(4) + dwCount(4) + rapports…
                int hidOffset = Marshal.SizeOf<RAWINPUTHEADER>();
                if (size < hidOffset + 8) 
                {
                    AppLog.Info($"Taille insuffisante pour les métadonnées HID ({size} < {hidOffset + 8}).");
                    return;
                }
                
                uint dwSizeHid = (uint)Marshal.ReadInt32(buffer, hidOffset);
                uint dwCount = (uint)Marshal.ReadInt32(buffer, hidOffset + 4);
                int dataOffset = hidOffset + 8;
                AppLog.Info($"HID Info → dwSizeHid: {dwSizeHid}, dwCount: {dwCount}, offset: {dataOffset}");
                
                if (dwSizeHid == 0 || dwCount == 0) return;

                for (uint i = 0; i < dwCount; i++)
                {
                    int reportStart = dataOffset + (int)(i * dwSizeHid);
                    if (reportStart + (int)dwSizeHid <= size)
                    {
                        byte[] rawBytes = new byte[dwSizeHid];
                        Marshal.Copy(buffer + reportStart, rawBytes, 0, (int)dwSizeHid);
                        string hex = BitConverter.ToString(rawBytes);
                        AppLog.Info($"Rapport HID [{i + 1}/{dwCount}] hex: {hex}");
                        
                        ProcessHidReport(buffer, reportStart, (int)dwSizeHid);
                    }
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
    /// Décodage d'un rapport HID du Précision Touchpad via HidP_* : le rapport
    /// contient soit le « scan/boutons » (avec Contact Count), soit les données
    /// d'un contact. Le décodage s'appuie sur le report descriptor réel du pavé
    /// (aucun offset lié au constructeur n'est présumé).
    /// </summary>
    private void ProcessHidReport(IntPtr buffer, int start, int reportLen)
    {
        if (_preparsedData == IntPtr.Zero) return;

        var now = Stopwatch.GetTimestamp();

        // Nouvelle session de geste après un silence : on remet à zéro les
        // accumulateurs, mais on garde l'armement tant que les doigts restent
        // posés — un seul déclenchement par geste, comme sur macOS.
        if (_lastReportTs != 0 && now - _lastReportTs > NewSessionTicks)
        {
            _deltaAccum = 0;
            if (!_haveContactCount) _activeIds.Clear();
        }
        _lastReportTs = now;

        var reportPtr = IntPtr.Add(buffer, start);

        // 1) Contact Count (0x54) — présent dans le rapport de scan/boutons.
        if (TryGetUsage(RawInputConstants.USAGE_PAGE_DIGITIZER,
                RawInputConstants.USAGE_CONTACT_COUNT, reportPtr, reportLen, out uint contactCount))
        {
            _haveContactCount = true;
            _activeContactCount = (int)contactCount;
        }

        // 2) Rapport de contact : identifiant + position X (+ état du contact).
        if (TryGetUsage(RawInputConstants.USAGE_PAGE_DIGITIZER,
                RawInputConstants.USAGE_CONTACT_IDENTIFIER, reportPtr, reportLen, out uint contactIdRaw))
        {
            if (contactIdRaw >= MaxContacts) return;
            int id = (int)contactIdRaw;

            if (IsTipDown(reportPtr, reportLen)) _activeIds.Add(id);
            else _activeIds.Remove(id);

            // Position X : usage Generic Desktop 0x30 (spécification Précision
            // Touchpad). Certains pavés l'exposent aussi sur Digitizer.
            if (TryGetUsage(RawInputConstants.USAGE_PAGE_GENERIC_DESKTOP,
                    RawInputConstants.USAGE_X, reportPtr, reportLen, out uint xv)
                || TryGetUsage(RawInputConstants.USAGE_PAGE_DIGITIZER,
                    RawInputConstants.USAGE_X, reportPtr, reportLen, out xv))
            {
                long x = xv;
                if (_contactTs[id] != 0 && now - _contactTs[id] <= ContactStaleTicks)
                {
                    long d = x - _contactX[id];
                    if (Math.Abs(d) < MaxContactStep)
                        _deltaAccum += d;
                }
                _contactX[id] = x;
                _contactTs[id] = now;
            }
        }

        // 3) Décision de balayage.
        int count = _haveContactCount ? _activeContactCount : _activeIds.Count;

        if (count < MinContactsForSwipe)
        {
            // Pas assez de doigts : on remet à zéro et on ré-arme le geste.
            _deltaAccum = 0;
            _swipeTriggered = false;
            return;
        }

        if (_swipeTriggered)
        {
            // Balayage déjà consommé : on attend le lever des doigts.
            _deltaAccum = 0;
            return;
        }

        if (Math.Abs(_deltaAccum) >= SwipeDeltaThreshold)
        {
            int direction = _deltaAccum > 0 ? -1 : 1;
            _swipeTriggered = true;
            _deltaAccum = 0;

            AppLog.Info($"Swipe {count} doigts → espace {(direction > 0 ? "suivant" : "précédent")}.");
            SwipeDetected?.Invoke(this, direction);
        }
    }

    /// <summary>
    /// HidP_GetUsageValue en essayant collection 0 (racine) puis 1 (premier
    /// contact imbriqué). Les pavés Précision Touchpad exposent Contact Count
    /// à la racine et X/Y/Contact Identifier dans des collections enfants.
    /// </summary>
    private bool TryGetUsage(ushort usagePage, ushort usage, IntPtr report, int reportLen, out uint value)
    {
        if (NativeMethods.HidP_GetUsageValue(RawInputConstants.HIDP_INPUT,
                usagePage, 0, usage, out value, _preparsedData, report, (uint)reportLen)
            == RawInputConstants.HIDP_STATUS_SUCCESS)
            return true;

        if (NativeMethods.HidP_GetUsageValue(RawInputConstants.HIDP_INPUT,
                usagePage, 1, usage, out value, _preparsedData, report, (uint)reportLen)
            == RawInputConstants.HIDP_STATUS_SUCCESS)
            return true;

        value = 0;
        return false;
    }

    /// <summary>
    /// Le contact de ce rapport est-il « posé » (Tip Switch) ? Si le rapport ne
    /// transporte aucun bouton, on considère le contact actif.
    /// </summary>
    private bool IsTipDown(IntPtr report, int reportLen)
    {
        var usages = new ushort[MaxButtons];
        uint length = (uint)usages.Length;
        uint status = NativeMethods.HidP_GetUsages(RawInputConstants.HIDP_INPUT,
            RawInputConstants.USAGE_PAGE_DIGITIZER, 0, usages, ref length,
            _preparsedData, report, (uint)reportLen);

        if (status != RawInputConstants.HIDP_STATUS_SUCCESS || length == 0)
        {
            length = (uint)usages.Length;
            status = NativeMethods.HidP_GetUsages(RawInputConstants.HIDP_INPUT,
                RawInputConstants.USAGE_PAGE_DIGITIZER, 1, usages, ref length,
                _preparsedData, report, (uint)reportLen);
        }

        if (status != RawInputConstants.HIDP_STATUS_SUCCESS || length == 0)
            return true; // pas de bouton décrit : le contact est considéré actif

        for (var i = 0; i < length; i++)
            if (usages[i] == RawInputConstants.USAGE_TIP_SWITCH)
                return true;
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_sourceWindow is not null)
            _sourceWindow.WindowMessage -= OnWindowMessage;

        FreePreparsedData();
    }

    private void FreePreparsedData()
    {
        if (_preparsedData != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_preparsedData);
            _preparsedData = IntPtr.Zero;
        }
    }
}