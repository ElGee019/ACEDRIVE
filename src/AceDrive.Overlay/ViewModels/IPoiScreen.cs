namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Marker for a POI interior (a shop, a bank, …) instead of an onboarding or
/// job page. Keeps idle auto-hide from closing a page the player is reading,
/// and lets the host refuse a second ENTER while already inside.
/// </summary>
public interface IPoiScreen
{
}
