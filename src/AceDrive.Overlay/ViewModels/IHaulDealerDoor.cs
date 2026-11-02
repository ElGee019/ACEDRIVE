using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The dealer door a haul starts from: the first dealer the career has
/// discovered, or the one the first haul opens for itself. The flow asks for a
/// door; the collaborator owns the directory, the placement and the save, so
/// no window, file or spawn rule lives in the flow.
/// </summary>
public interface IHaulDealerDoor
{
    /// <summary>A dealer with a street the HUD can pin, or null when the map
    /// has no free house left to open one on.</summary>
    PointOfInterest? Dealer();
}
