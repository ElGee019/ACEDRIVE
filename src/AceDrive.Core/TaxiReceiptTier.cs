// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11 (Unity
// TaxiReceiptScreen tier lines).
namespace AceDrive.Core;

/// <summary>One receipt tier line: the km range actually used and its charge.</summary>
public sealed record TaxiReceiptTier(double StartKm, double WidthKm, double RatePerKm, double Km, double Charge)
{
    /// <summary>True for the open top tier (no upper bound).</summary>
    public bool IsOpen => WidthKm <= 0.0;
}
