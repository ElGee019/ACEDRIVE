using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One discovered POI as the addresses page shows it: the display name, the
/// formatted street + house number and whether its row is the destination
/// just picked. The POI rides along so the row's command can pin the HUD
/// without a second lookup.
/// </summary>
public sealed record AddressRow(
    PointOfInterest Poi, string Name, string Address, bool IsSelected);
