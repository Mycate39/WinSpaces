using System.ComponentModel;
using System.Runtime.InteropServices;
using WinSpaces.Diagnostics;
using WinSpaces.Native;

namespace WinSpaces.Services;

/// <summary>
/// Gestionnaire centralisé des raccourcis globaux.
/// Remplace HotkeyService pour supporter l'enregistrement dynamique par les modules.
/// </summary>
internal sealed class GlobalHotkeyManager : HealthCheckableBase, IDisposable
{
    private readonly MessageWindow _window;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 0xB01;

    public override string ComponentName => "Global Hotkey Manager";
    public override bool IsHealthy => _handlers.Count > 0;
    public override string StatusMessage { get; protected set; } = "Actif : 0 raccourcis enregistrés";

    public GlobalHotkeyManager(MessageWindow window)
    {
        _window = window;
        _window.WindowMessage += OnWindowMessage;
        UpdateStatus();
    }

    public int Register(uint modifiers, uint vk, Action handler)
    {
        int id = _nextId++;
        if (NativeMethods.RegisterHotKey(_window.Handle, id, modifiers, vk))
        {
            _handlers[id] = handler;
            UpdateStatus();
            return id;
        }
        throw new Win32Exception(Marshal.GetLastWin32Error(), $"Échec enregistrement hotkey 0x{vk:X}");
    }

    private void UpdateStatus()
    {
        StatusMessage = $"Actif : {_handlers.Count} raccourcis enregistrés";
        SetMetric("registered_count", _handlers.Count);
        UpdateLastCheck();
    }

    private void OnWindowMessage(object? sender, Message m)
    {
        if (m.Msg == Win32Messages.WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (_handlers.TryGetValue(id, out var handler))
            {
                handler();
            }
        }
    }

    public void Unregister(int id)
    {
        if (_handlers.Remove(id))
        {
            NativeMethods.UnregisterHotKey(_window.Handle, id);
            UpdateStatus();
        }
    }

    public void Dispose()
    {
        _window.WindowMessage -= OnWindowMessage;
        foreach (int id in _handlers.Keys.ToList())
        {
            NativeMethods.UnregisterHotKey(_window.Handle, id);
        }
        _handlers.Clear();
    }
}

