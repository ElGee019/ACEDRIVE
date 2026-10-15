// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

/// <summary>Stock color band for a store row (Unity thresholds, as data —
/// the view owns the actual paints).</summary>
public enum StockLevel
{
    Red,
    Amber,
    Green,
}
