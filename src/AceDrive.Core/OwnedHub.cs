// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (wholesale
// hub ownership + credibility/CredP ramp), port of Unity WholesaleHub.
namespace AceDrive.Core;

/// <summary>A hub the player bought (Unity WholesaleHub). Owner name, map
/// address and purchase stamp land with the freeroam map and the persistent
/// save (PORTPLAN 51); the tier/level numbers ride along now because the
/// per-delivery CredP ramp (item 18) reads exactly those.</summary>
public sealed record OwnedHub(string Name, string Category, double Cost, int Tier, int Level);
