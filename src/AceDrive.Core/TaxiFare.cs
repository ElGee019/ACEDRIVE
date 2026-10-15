// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11 (Unity
// TaxiReceiptScreen.CalculateDistanceCharge/CalculateEarnings).
namespace AceDrive.Core;

/// <summary>
/// Fare math. Deterministic, no I/O. Rates come from the table.
/// Money rounds to cents at every step (AwayFromZero): EUR has no
/// fractions of a cent, and receipt lines must add up on the page.
/// </summary>
public static class TaxiFare
{
    public static double ComputeDistanceCharge(double distanceKm, FareTable table)
    {
        double remaining = Math.Max(0.0, distanceKm);
        double charge = 0.0;
        foreach (FareTier tier in table.Tiers)
        {
            if (remaining <= 0.0)
            {
                break;
            }
            double tierKm = tier.WidthKm <= 0.0 ? remaining : Math.Min(remaining, tier.WidthKm);
            charge += tierKm * tier.RatePerKm;
            remaining -= tierKm;
        }
        return Cents(charge);
    }

    public static double ComputeBase(double distanceKm, FareTable table) =>
        Cents(table.Forfait + ComputeDistanceCharge(distanceKm, table));

    public static double ComputeTip(double baseTotal, bool tipEligible, FareTable table) =>
        tipEligible ? Cents(baseTotal * table.TipRate) : 0.0;

    public static double ComputeFineDeduction(int fines, FareTable table) =>
        Cents(Math.Max(0, fines) * table.FineAmount);

    private static double Cents(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
