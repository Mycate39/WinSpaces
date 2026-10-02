using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
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
internal sealed class PrecisionTouchpadWatcher : HealthCheckableBase, IDisposable
{
    // IHealthCheckable Implementation
    public override string ComponentName => "Precision Touchpad (Raw Input HID)";
    public override bool IsHealthy => PrecisionTouchpadPresent && _preparsedData != IntPtr.Zero;
    public override string StatusMessage => GetStatusMessage();

    private string GetStatusMessage()
    {
        if (!PrecisionTouchpadPresent) return "Aucun Precision Touchpad détecté (fallback momentum actif)";
        if (_preparsedData == IntPtr.Zero) return "Touchpad détecté mais preparsed data non initialisée";
        return "Touchpad opérationnel : swipe 3+ doigts actif";
    }

    private const int MaxContacts = 16;
    private const int MaxButtons = 8;
    private const int MaxReportSize = 4096;

    // Buffer pool to avoid allocations on each WM_INPUT
    private static readonly ConcurrentQueue<byte[]> _bufferPool = new();
    private static readonly object _poolLock = new();

    // Configuration (remplace les constantes hardcodées)
    private int _minContactsForSwipe = 3;
    private int _swipeDeltaThreshold = 800;
    private int _maxContactStep = 0x4000;
    private long _contactStaleTicks;
    private long _newSessionTicks;

    // === Gestes style macOS ===
    private bool _verticalGesturesEnabled = true;
    private int _verticalSwipeDeltaThreshold = 600;
    private bool _differentiateThreeFourFingers = true;
    private string _threeFingerUpAction = "ShowDashboard";
    private string _threeFingerDownAction = "None";

    // Suivi du mouvement vertical
    private readonly long[] _contactY = new long[MaxContacts];
    private long _verticalDeltaAccum;
    private bool _verticalSwipeTriggered;

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

    private static long MsToTicks(int ms) => TimeSpan.FromMilliseconds(ms).Ticks;

    public PrecisionTouchpadWatcher(MessageWindow? sourceWindow)
        => _sourceWindow = sourceWindow;

    /// <summary>Initialise la configuration depuis le service de configuration.</summary>
    public void Configure(WinSpaces.Configuration.GestureConfig config)
    {
        if (config == null) return;
        _minContactsForSwipe = Math.Max(2, config.MinContactsForSwipe);
        _swipeDeltaThreshold = Math.Max(100, config.SwipeDeltaThreshold);
        _contactStaleTicks = MsToTicks(Math.Max(50, config.ContactStaleTimeoutMs));
        _newSessionTicks = MsToTicks(Math.Max(100, config.NewSessionTimeoutMs));

        // === Gestes style macOS ===
        _verticalGesturesEnabled = config.VerticalGesturesEnabled;
        _verticalSwipeDeltaThreshold = Math.Max(100, config.VerticalSwipeDeltaThreshold);
        _differentiateThreeFourFingers = config.DifferentiateThreeFourFingers;
        _threeFingerUpAction = config.ThreeFingerUpAction;
        _threeFingerDownAction = config.ThreeFingerDownAction;

        AppLog.Info($"PrecisionTouchpadWatcher configuré : MinContacts={_minContactsForSwipe}, SwipeThreshold={_swipeDeltaThreshold}, VerticalEnabled={_verticalGesturesEnabled}, VerticalThreshold={_verticalSwipeDeltaThreshold}, Differentiate3_4={_differentiateThreeFourFingers}, 3FingerUp={_threeFingerUpAction}, 3FingerDown={_threeFingerDownAction}, StaleTimeout={config.ContactStaleTimeoutMs}ms, NewSessionTimeout={config.NewSessionTimeoutMs}ms");
    }

    /// <summary>true si un Précision Touchpad est présent dans le système.</summary>
    public bool PrecisionTouchpadPresent { get; private set; }

    // === Gestes horizontaux (style macOS) ===
    /// <summary>Swipe horizontal détecté : -1 (gauche/précédent), +1 (droite/suivant).</summary>
    public event EventHandler<int>? SwipeDetected;

    /// <summary>Swipe horizontal 4 doigts détecté (différencié du 3 doigts si configuré).</summary>
    public event EventHandler<int>? FourFingerSwipeDetected;

    // === Gestes verticaux (style macOS Mission Control / App Exposé) ===
    /// <summary>Swipe vertical 3 doigts détecté : -1 (bas/App Exposé), +1 (haut/Mission Control).</summary>
    public event EventHandler<int>? VerticalSwipeDetected;

    /// <summary>Action 3 doigts vers le haut (Mission Control style).</summary>
    public event EventHandler? ThreeFingerUpActionRequested;

    /// <summary>Action 3 doigts vers le bas (App Exposé style).</summary>
    public event EventHandler? ThreeFingerDownActionRequested;

    public void Start()
    {
        if (_sourceWindow is null || _disposed) return;

        PrecisionTouchpadPresent = EnumeratePrecisionTouchpad();
        if (!PrecisionTouchpadPresent)
        {
            AppLog.Info("Précision Touchpad absent : mode momentum (fallback) utilisé.");
            return;
        }

        bool ok = RegisterTouchpad();
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
        // CORRECTION: Utiliser HidD_GetPreparsedData qui est l'API correcte
        // pour obtenir les données preparsed d'un périphérique HID.
        // GetRawInputDeviceInfo avec RIDI_PREPARSEDDATA est moins fiable.
        if (NativeMethods.HidD_GetPreparsedData(hDevice, out IntPtr preparsedData))
        {
            return preparsedData;
        }

        AppLog.Warning("HidD_GetPreparsedData a échoué pour le touchpad.");
        return IntPtr.Zero;
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
            if (PrecisionTouchpadPresent) RegisterTouchpad();
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
                
            // Only log at debug level to avoid log spam
            if (AppLog.IsDebugEnabled)
                AppLog.Debug($"WM_INPUT reçu. Handle: 0x{hRawInput:X}. Taille calculée: {size} octets.");
                
            if (size == 0 || size > MaxReportSize) return;

            // Use buffer pool to avoid allocations
            byte[] buffer = _bufferPool.TryDequeue(out var pooledBuffer) && pooledBuffer.Length >= size
                ? pooledBuffer
                : new byte[Math.Max(size, 1024)];
            
            // Pin buffer for unmanaged access
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr bufferPtr = handle.AddrOfPinnedObject();
                uint read = NativeMethods.GetRawInputData(hRawInput, RawInputConstants.RID_INPUT,
                    bufferPtr, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
                if (read == uint.MaxValue) 
                {
                    AppLog.Warning("Erreur GetRawInputData : uint.MaxValue retourné.");
                    return;
                }

                // Parse header directly from buffer without Marshal.PtrToStructure
                // RAWINPUTHEADER: dwType(4), dwSize(4), hDevice(8), wParam(8) = 24 bytes on x64
                int headerType = Marshal.ReadInt32(bufferPtr, 0);
                int headerSize = Marshal.ReadInt32(bufferPtr, 4);
                IntPtr hDevice = Marshal.ReadIntPtr(bufferPtr, 8);
                
                if (AppLog.IsDebugEnabled)
                    AppLog.Debug($"RAWINPUTHEADER → Type: {headerType}, Size: {headerSize}, hDevice: 0x{hDevice:X}");

                if (headerType != RawInputConstants.RIM_TYPE_HID)
                {
                    if (AppLog.IsDebugEnabled)
                        AppLog.Debug($"Rejeté: Le périphérique n'est pas de type HID (Type {headerType}).");
                    return;
                }

                // Le rapport HID suit l'en-tête RAWINPUTHEADER :
                //   [RAWINPUTHEADER] + dwSizeHid(4) + dwCount(4) + rapports…
                int hidOffset = Marshal.SizeOf<RAWINPUTHEADER>();
                if (size < hidOffset + 8) 
                {
                    if (AppLog.IsDebugEnabled)
                        AppLog.Debug($"Taille insuffisante pour les métadonnées HID ({size} < {hidOffset + 8}).");
                    return;
                }
                
                uint dwSizeHid = (uint)Marshal.ReadInt32(bufferPtr, hidOffset);
                uint dwCount = (uint)Marshal.ReadInt32(bufferPtr, hidOffset + 4);
                int dataOffset = hidOffset + 8;
                
                if (AppLog.IsDebugEnabled)
                    AppLog.Debug($"HID Info → dwSizeHid: {dwSizeHid}, dwCount: {dwCount}, offset: {dataOffset}");
                
                if (dwSizeHid == 0 || dwCount == 0) return;

                for (uint i = 0; i < dwCount; i++)
                {
                    int reportStart = dataOffset + (int)(i * dwSizeHid);
                    if (reportStart + (int)dwSizeHid <= size)
                    {
                        ProcessHidReport(bufferPtr, reportStart, (int)dwSizeHid);
                    }
                }
            }
            finally
            {
                handle.Free();
                // Return buffer to pool for reuse
                if (buffer.Length <= MaxReportSize)
                    _bufferPool.Enqueue(buffer);
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
        if (_lastReportTs != 0 && now - _lastReportTs > _newSessionTicks)
        {
            _deltaAccum = 0;
            _verticalDeltaAccum = 0;
            _swipeTriggered = false;
            _verticalSwipeTriggered = false;
            if (!_haveContactCount) _activeIds.Clear();
        }
        _lastReportTs = now;

        var reportPtr = IntPtr.Add(buffer, start);

        // 1) Contact Count (0x54) — présent dans le rapport de scan/boutons.
        if (TryGetUsage(RawInputConstants.USAGE_PAGE_DIGITIZER,
                RawInputConstants.USAGE_CONTACT_COUNT, reportPtr, reportLen, out uint contactCount))
        {
            _haveContactCount = true;
            // Validate contact count: clamp to realistic range (1-10 fingers max)
            // Some touchpads report incorrect values (e.g., 255, 230)
            _activeContactCount = Math.Clamp((int)contactCount, 1, 10);
        }

        // 2) Rapport de contact : identifiant + position X + position Y (+ état du contact).
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
                if (_contactTs[id] != 0 && now - _contactTs[id] <= _contactStaleTicks)
                {
                    long d = x - _contactX[id];
                    if (Math.Abs(d) < _maxContactStep)
                        _deltaAccum += d;
                }
                _contactX[id] = x;
                _contactTs[id] = now;
            }

            // Position Y : pour les gestes verticaux (Mission Control / App Exposé style macOS)
            if (TryGetUsage(RawInputConstants.USAGE_PAGE_GENERIC_DESKTOP,
                    RawInputConstants.USAGE_Y, reportPtr, reportLen, out uint yv)
                || TryGetUsage(RawInputConstants.USAGE_PAGE_DIGITIZER,
                    RawInputConstants.USAGE_Y, reportPtr, reportLen, out yv))
            {
                long y = yv;
                if (_contactTs[id] != 0 && now - _contactTs[id] <= _contactStaleTicks)
                {
                    long d = y - _contactY[id];
                    if (Math.Abs(d) < _maxContactStep)
                        _verticalDeltaAccum += d;
                }
                _contactY[id] = y;
                _contactTs[id] = now;
            }
        }

        // 3) Décision de balayage.
        int count = _haveContactCount ? _activeContactCount : _activeIds.Count;

        // Pas assez de doigts : on remet à zéro et on ré-arme le geste.
        if (count < _minContactsForSwipe)
        {
            _deltaAccum = 0;
            _verticalDeltaAccum = 0;
            _swipeTriggered = false;
            _verticalSwipeTriggered = false;
            return;
        }

        // === GESTES HORIZONTAUX (switch bureau) ===
        
        // Si différenciation 3 vs 4 doigts activée et 4+ doigts
        bool isFourFingerSwipe = _differentiateThreeFourFingers && count >= 4;

        if (_swipeTriggered)
        {
            // Balayage déjà consommé : on attend le lever des doigts.
            _deltaAccum = 0;
            return;
        }

        if (Math.Abs(_deltaAccum) >= _swipeDeltaThreshold)
        {
            // CORRECTION: Logique de direction corrigée.
            // Delta positif (mouvement vers la droite) = bureau suivant (+1)
            // Delta négatif (mouvement vers la gauche) = bureau précédent (-1)
            int direction = _deltaAccum > 0 ? 1 : -1;
            _swipeTriggered = true;
            _deltaAccum = 0;

            AppLog.Info($"Swipe {count} doigts → espace {(direction > 0 ? "suivant" : "précédent")}.");

            // Différencier 3 doigts vs 4 doigts si configuré
            if (isFourFingerSwipe)
            {
                FourFingerSwipeDetected?.Invoke(this, direction);
            }
            else
            {
                SwipeDetected?.Invoke(this, direction);
            }
        }

        // === GESTES VERTICAUX (Mission Control / App Exposé style macOS) ===
        if (_verticalGesturesEnabled && count >= 3 && !_verticalSwipeTriggered)
        {
            if (Math.Abs(_verticalDeltaAccum) >= _verticalSwipeDeltaThreshold)
            {
                // Delta positif (mouvement vers le bas sur trackpad) = App Exposé (-1)
                // Delta négatif (mouvement vers le haut sur trackpad) = Mission Control (+1)
                // Note: sur trackpad macOS, swipe UP = doigts vers le haut = Mission Control
                // Sur Windows, Y augmente vers le bas, donc delta négatif = vers le haut
                int direction = _verticalDeltaAccum < 0 ? 1 : -1; // +1 = haut (Mission Control), -1 = bas (App Exposé)
                _verticalSwipeTriggered = true;
                _verticalDeltaAccum = 0;

                AppLog.Info($"Vertical swipe {count} doigts → {(direction > 0 ? "haut (Mission Control)" : "bas (App Exposé)")}.");
                VerticalSwipeDetected?.Invoke(this, direction);

                // Déclencher les actions spécifiques 3 doigts
                if (count == 3 || (!_differentiateThreeFourFingers && count >= 3))
                {
                    if (direction > 0 && _threeFingerUpAction == "ShowDashboard")
                    {
                        ThreeFingerUpActionRequested?.Invoke(this, EventArgs.Empty);
                    }
                    else if (direction < 0 && _threeFingerDownAction == "ShowDashboard")
                    {
                        ThreeFingerDownActionRequested?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
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

    private bool RegisterTouchpad()
    {
        if (_sourceWindow == null) 
        {
            AppLog.Error("PrecisionTouchpadWatcher: _sourceWindow is null, cannot register touchpad.");
            return false;
        }

        // Réception des rapports HID du toucher même sans focus (INPUTSINK).
        var device = new RAWINPUTDEVICE
        {
            usUsagePage = RawInputConstants.USAGE_PAGE_DIGITIZER,
            usUsage = RawInputConstants.USAGE_TOUCHPAD,
            dwFlags = RawInputConstants.RIDEV_INPUTSINK | RawInputConstants.RIDEV_DEVNOTIFY,
            hwndTarget = _sourceWindow.Handle
        };

        bool result = NativeMethods.RegisterRawInputDevices(
            new[] { device }, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());

        if (result)
        {
            AppLog.Info($"PrecisionTouchpadWatcher: RegisterRawInputDevices succès pour hwndTarget={_sourceWindow.Handle}.");
        }
        else
        {
            AppLog.Error(new Win32Exception(Marshal.GetLastWin32Error(), 
                $"Échec RegisterRawInputDevices pour hwndTarget={_sourceWindow.Handle}."));
        }

        return result;
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
            // CORRECTION: Utiliser HidD_FreePreparsedData au lieu de Marshal.FreeHGlobal
            // pour libérer correctement les données preparsed allouées par HidD_GetPreparsedData
            NativeMethods.HidD_FreePreparsedData(_preparsedData);
            _preparsedData = IntPtr.Zero;
        }
    }
}