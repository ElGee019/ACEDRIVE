// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 18/51 (Unity
// PlayerData.credP + ownedWholesaleHubs + hubDeliveryStats).
namespace AceDrive.Core;

/// <summary>
/// The delivery career as it survives a restart: the CredP balance, the hubs
/// the player bought and each hub's delivery count (the CredP ramp's progress).
/// It rides the driver file (this port's PlayerData) so a bought career reopens
/// exactly where it was left, instead of being granted back from a seed.
/// Lists and dictionary may be null: a save written before the career
/// persisted has no value here, which the constructor treats as a fresh career.
/// </summary>
public sealed record DeliveryCareerState(
    double CredP,
    IReadOnlyList<OwnedHub>? Owned = null,
    IReadOnlyDictionary<string, int>? Deliveries = null);
