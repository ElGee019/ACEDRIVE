// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

/// <summary>
/// Phase of the delivery career page. Unity DeliveryJobScreen rule: the
/// main button reads OPEN NEW CAREER while nothing waits in the pickup
/// cart and flips to DELIVER once an order is confirmed.
/// </summary>
public enum DeliveryPhase
{
    OpenCareer,
    Deliver,
}
