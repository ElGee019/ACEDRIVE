// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (Unity
// WholesaleHub + DeliveryHubScreen.OnOpenCareerClicked).
namespace AceDrive.Core;

/// <summary>A hub the player bought (Unity WholesaleHub). Owner name, map
/// address and purchase stamp land with the freeroam map and the persistent
/// save (PORTPLAN 51); the tier/level numbers ride along now because the
/// per-delivery CredP ramp (item 18) reads exactly those, and the owner
/// name is assigned at purchase like Unity does (Unity WholesaleHub.ownerName),
/// because the delivery friendship keys on it. Key is the hub's map identity
/// (Unity's category_slug id): the host pins its POI with the same id so the
/// pickup leg can find the door.</summary>
public sealed record OwnedHub(
    string Name, string Category, double Cost, int Tier, int Level,
    string OwnerName = "", string Key = "");
