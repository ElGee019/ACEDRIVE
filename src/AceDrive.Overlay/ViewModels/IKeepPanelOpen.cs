namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Marker for a page that must stay open until the player closes it, even
/// when it sits on screen longer than the idle window (the customer request
/// timer is 14 s against a 15 s auto-hide). New long-lived pages opt in by
/// implementing this instead of growing the shell's type checks.
/// </summary>
public interface IKeepPanelOpen
{
}
