namespace AceDrive.Overlay.Services;

/// <summary>
/// Starts a replacement overlay process after a reset. A fresh process is the
/// only way to guarantee that no store still holds the old career in memory:
/// it boots from the introduction exactly like a new install.
/// </summary>
public interface IAppRestarter
{
    /// <summary>
    /// Starts a replacement instance and ends this one. Returns false when the
    /// replacement could not be started; the caller then keeps the current
    /// session alive instead of leaving the player with no window.
    /// </summary>
    bool Restart();
}
