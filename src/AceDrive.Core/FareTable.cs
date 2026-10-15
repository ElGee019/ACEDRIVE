// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11-13.
namespace AceDrive.Core;

/// <summary>
/// Fare balance table. Loaded from Data/fares.json at startup; this Default
/// is the test-shell fallback (and the deterministic fixture for tests),
/// not the source of truth.
/// </summary>
public sealed record FareTable(double BaseFare, double PerKmRate, double TipRate)
{
    public static readonly FareTable Default = new(3.50, 1.80, 0.10);
}
