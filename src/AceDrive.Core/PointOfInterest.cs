// A spawned point of interest (PORTPLAN 24/27): a typed business at one
// addressable house on the map. Pure data; the HUD and the arrival router
// read it, the dialogue formats it.
namespace AceDrive.Core;

/// <summary>
/// A destination the player can be sent to: the business identity plus the
/// resolved street and house number. The side stays internal metadata, like
/// <see cref="AddressLocation.Side"/>. Type/Owner/Category/GuaranteedItems ride
/// along from <see cref="PoiDefinition"/> so the arrival handler and the host
/// never need a second lookup.
/// </summary>
public sealed record PointOfInterest(
    string Id,
    string Name,
    AddressLocation Address,
    string Type = PoiTypes.Grocery,
    string OwnerName = "",
    string Category = "",
    IReadOnlyList<string>? GuaranteedItems = null);
