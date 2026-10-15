// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11-13.
namespace AceDrive.Core;

/// <summary>Fare math. Deterministic, no I/O. Rates come from the table.</summary>
public static class TaxiFare
{
    public static double ComputeFare(double distanceKm, FareTable table) =>
        Math.Round(table.BaseFare + table.PerKmRate * Math.Max(0.0, distanceKm), 2);

    public static double ComputeTip(double fare, bool cleanDrive, FareTable table) =>
        cleanDrive ? Math.Round(fare * table.TipRate, 2) : 0.0;
}
