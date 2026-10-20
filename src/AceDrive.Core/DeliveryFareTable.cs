// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 16 (Unity
// CalculateDeliveryDistanceCharge + DeliveryReceiptScreen.PopulateFare:
// the longer the run the lower the per-km rate, unlike the taxi table).
namespace AceDrive.Core;

/// <summary>
/// Delivery distance-charge balance table (no forfait, no tip, no fine —
/// a drop-off fare is distance only). Loaded from
/// Data/delivery_fares.json at startup; this Default is the test-shell
/// fallback (and the deterministic fixture for tests), not the source
/// of truth.
/// </summary>
public sealed record DeliveryFareTable(IReadOnlyList<FareTier> Tiers)
{
    public static readonly DeliveryFareTable Default = new(
        [new FareTier(2.0, 20.0),
         new FareTier(3.0, 17.0),
         new FareTier(5.0, 12.0),
         new FareTier(40.0, 6.0),
         new FareTier(0.0, 3.0)]);
}
