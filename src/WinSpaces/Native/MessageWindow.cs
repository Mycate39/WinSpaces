namespace WinSpaces.Native;

/// <summary>
/// Fenêtre invisible "message-only" (parent HWND_MESSAGE) : sert de cible
/// aux messages entrants du système — WM_HOTKEY (raccourcis globaux) et
/// WM_INPUT (Raw Input du Précision Touchpad) notamment.
/// </summary>
internal sealed class MessageWindow : NativeWindow
{
    private const nint HwndMessage = -3; // HWND_MESSAGE

    public MessageWindow()
    {
        var cp = new CreateParams
        {
            Caption = "WinSpaces.MessagePump",
            Style = 0,
            ExStyle = 0,
            Parent = HwndMessage
        };
        CreateHandle(cp);
    }

    /// <summary>Message système reçu (WM_HOTKEY, WM_INPUT, …).</summary>
    public event EventHandler<Message>? WindowMessage;

    protected override void WndProc(ref Message m)
    {
        WindowMessage?.Invoke(this, m);
        base.WndProc(ref m);
    }
}