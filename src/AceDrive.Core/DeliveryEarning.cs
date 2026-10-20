// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

/// <summary>The outcome of crediting one delivered store: which hub owner
/// the run befriends ("" when the store rests on no owned hub) and the
/// CredP the ramp earned for the hub.</summary>
public sealed record DeliveryEarning(string OwnerName, int CredPEarned);
