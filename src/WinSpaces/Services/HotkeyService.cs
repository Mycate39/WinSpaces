using System.ComponentModel;
using System.Runtime.InteropServices;
using WinSpaces.Desktops;
using WinSpaces.Native;

namespace WinSpaces.Services;

/// <summary>
/// Raccourcis globaux via RegisterHotKey (aucune injection, aucune interception :
/// le système notifie WM_HOTKEY sur notre fenêtre message-only).
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    public const int IdNextSpace = 0xB01;
    public const int IdPreviousSpace = 0xB02;
    public const int IdNewSpace = 0xB03;
    public const int IdMoveWindow = 0xB04;

    private readonly MessageWindow _window;
    private readonly VirtualDesktopService _desktops;
    private readonly List<int> _registeredIds = new();

    public HotkeyService(MessageWindow window, VirtualDesktopService desktops)
    {
        _window = window;
        _desktops = desktops;
    }

    /// <summary>Article "Espace suivant".</summary>
    public event EventHandler? NextSpaceRequested;

    /// <summary>Article "Espace précédent".</summary>
    public event EventHandler? PreviousSpaceRequested;

    /// <summary>Article "Nouvel espace".</summary>
    public event EventHandler? NewSpaceRequested;

    /// <summary>Article "Déplacer la fenêtre vers l'espace suivant".</summary>
    public event EventHandler? MoveWindowRequested;

    /// <summary>Enregistre les combinaisons Ctrl+Alt+… dans le système.</summary>
    public bool Install()
    {
        _window.WindowMessage += OnWindowMessage;

        bool ok = true;
        ok &= Register(IdNextSpace, KeyboardKeys.VK_RIGHT);
        ok &= Register(IdPreviousSpace, KeyboardKeys.VK_LEFT);
        ok &= Register(IdNewSpace, KeyboardKeys.VK_N);
        ok &= Register(IdMoveWindow, KeyboardKeys.VK_W);
        return ok;
    }

    private bool Register(int id, uint vk)
    {
        const uint modifiers = KeyboardKeys.MOD_CONTROL | KeyboardKeys.MOD_ALT | KeyboardKeys.MOD_NOREPEAT;
        if (NativeMethods.RegisterHotKey(_window.Handle, id, modifiers, vk))
        {
            _registeredIds.Add(id);
            return true;
        }

        AppLog.Error(new Win32Exception(Marshal.GetLastWin32Error(), $"Échec RegisterHotKey id=0x{id:X} vk=0x{vk:X}."));
        return false;
    }

    private void OnWindowMessage(object? sender, Message m)
    {
        if (m.Msg != Win32Messages.WM_HOTKEY) return;

        switch (m.WParam.ToInt32())
        {
            case IdNextSpace:
                NextSpaceRequested?.Invoke(this, EventArgs.Empty);
                break;
            case IdPreviousSpace:
                PreviousSpaceRequested?.Invoke(this, EventArgs.Empty);
                break;
            case IdNewSpace:
                NewSpaceRequested?.Invoke(this, EventArgs.Empty);
                break;
            case IdMoveWindow:
                MoveWindowRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public void Dispose()
    {
        _window.WindowMessage -= OnWindowMessage;
        foreach (var id in _registeredIds)
            NativeMethods.UnregisterHotKey(_window.Handle, id);
        _registeredIds.Clear();
    }
}