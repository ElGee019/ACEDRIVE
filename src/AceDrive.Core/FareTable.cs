// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11 (Unity
// TaxiReceiptScreen.CalculateEarnings: forfait + tiered distance charge,
// 20% tip when eligible, flat fine per speeding fine).
namespace AceDrive.Core;

/// <summary>
/// Fare balance table. Loaded from Data/fares.json at startup; this Default
/// is the test-shell fallback (and the deterministic fixture for tests),
/// not the source of truth.
/// </summary>
public sealed record FareTable(
    double Forfait,
    double TipRate,
    double FineAmount,
    IReadOnlyList<FareTier> Tiers)
{
    public static readonly FareTable Default = new(
        10.0,
        0.20,
        50.0,
        [new FareTier(2.0, 6.67),
         new FareTier(3.0, 5.67),
         new FareTier(5.0, 4.0),
         new FareTier(40.0, 2.0),
         new FareTier(0.0, 1.0)]);
}
