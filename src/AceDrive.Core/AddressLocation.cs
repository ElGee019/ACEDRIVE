// One resolved point on the placeholder free-roam address map.
namespace AceDrive.Core;

/// <summary>
/// The street and house number behind a normalised lap position. Pure data:
/// the HUD formats it, the resolver owns the geometry. The side is not shown
/// today; it is the data the future marker network places a house from.
/// </summary>
public readonly record struct AddressLocation(
    string StreetName, int HouseNumber, HouseSide Side);
