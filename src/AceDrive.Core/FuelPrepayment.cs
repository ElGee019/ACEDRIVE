// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The roadside refuel's paid-for tank (PORTPLAN item 38): EVO's tank is
// read-only to the overlay, so the repair pays for the next fill instead of
// writing the level. This split is where that credit is spent.
namespace AceDrive.Core;

/// <summary>One fill split into the litres a repair already paid for and the
/// litres the pump still has to bill.</summary>
public readonly record struct FuelPrepayResult(double BillableLitres, double PrepaidLitres);

/// <summary>
/// Applies a roadside-refuel credit to a detected fill. The credit is tied to
/// the car it was bought for: switching cars must never fuel another tank for
/// free, and the credit stays banked until that car is filled again.
/// </summary>
public static class FuelPrepayment
{
    /// <summary>
    /// Splits <paramref name="fillLitres"/> into billable and prepaid litres.
    /// A missing, non-positive or other-car credit leaves the whole fill
    /// billable.
    /// </summary>
    public static FuelPrepayResult Apply(
        double fillLitres, double prepaidLitres, string? prepaidCarKey, string? carKey)
    {
        if (!double.IsFinite(fillLitres) || fillLitres <= 0.0
            || !double.IsFinite(prepaidLitres) || prepaidLitres <= 0.0
            || prepaidCarKey is null || carKey is null
            || !string.Equals(prepaidCarKey, carKey, StringComparison.Ordinal))
        {
            return new FuelPrepayResult(fillLitres, 0.0);
        }

        double used = Math.Min(prepaidLitres, fillLitres);
        return new FuelPrepayResult(fillLitres - used, used);
    }
}
