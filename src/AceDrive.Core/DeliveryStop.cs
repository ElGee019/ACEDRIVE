// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobHandler).
namespace AceDrive.Core;

/// <summary>
/// One stop of a delivery run: the hub that loads a category's freight or the
/// store that receives it. Name and key are the POI identity the HUD and the
/// board already use; the address stays in the host's directory/catalog.
/// </summary>
public sealed record DeliveryStop(DeliveryStopKind Kind, string Key, string Name);
