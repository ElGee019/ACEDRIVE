// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 47, exact port
// of Unity Drive.Utils.ConsecutiveDayBonus.
namespace AceDrive.Core;

/// <summary>
/// Pure calculation for the consecutive-days login bonus shown on the
/// Todo rewards screen. Day 1 is the base (1 CR); each further day
/// multiplies the running total by (2.00 - 0.05 * (n - 1)), floored at
/// 1.0 so the streak never decays and the product plateaus once the
/// factor reaches 1.0 (day 20).
/// </summary>
public static class ConsecutiveDayBonus
{
    private const double BaseBonus = 1.0;
    private const double DayOneFactor = 2.00;
    private const double FactorStep = 0.05;
    private const double MinFactor = 1.0;

    public static int ComputeBonus(int days)
    {
        if (days <= 1) return (int)BaseBonus;
        double total = BaseBonus;
        for (int n = 2; n <= days; n++)
        {
            double factor = DayOneFactor - FactorStep * (n - 1);
            if (factor < MinFactor) factor = MinFactor;
            total *= factor;
        }
        return (int)System.Math.Round(total, System.MidpointRounding.AwayFromZero);
    }
}
