using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The window-side seam of the call input: the shell hands over its HWND (the
/// listener needs one to receive raw input) and forwards the messages the
/// window sees. Everything behind it — the raw reports, the HID parsing, the
/// pad poll — belongs to the service, and none of it claims the input: the
/// press still reaches the game.
/// </summary>
public interface IWindowInput
{
    /// <summary>Binds the listener to the window and to the action a call runs.</summary>
    void Attach(IntPtr window, Action callAction);

    /// <summary>Handles one window message; true when it was the call's own.</summary>
    bool HandleWindowMessage(int message, IntPtr wParam, IntPtr lParam);

    /// <summary>Stops listening; safe to call after the window is gone.</summary>
    void Detach();
}
