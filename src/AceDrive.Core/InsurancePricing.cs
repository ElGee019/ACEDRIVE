// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN item 34, port of Unity
// Utils/InsurancePricing.cs — the daily-cost math shared by the broker display
// and the nightly charge so both always agree.
namespace AceDrive.Core;

/// <summary>
/// A car's daily premium: base = price × rate × the general increase, then the
/// driver's overall rating applies as a straight percent discount, floored at
/// CR1 (Unity InsurancePricing). A negative price or rate reads as 0; the
/// driver rating clamps to 0..100. Pure math, no state.
/// </summary>
public static class InsurancePricing
{
    /// <summary>Unity BaseDailyCost: the undiscounted daily premium.</summary>
    public static double BaseDailyCost(double carPrice, InsuranceRules rules)
    {
        double price = double.IsFinite(carPrice) ? Math.Max(0.0, carPrice) : 0.0;
        double rate = double.IsFinite(rules.PremiumRate) ? Math.Max(0.0, rules.PremiumRate) : 0.0;
        return price * rate * rules.BaseIncreaseFactor;
    }

    /// <summary>Unity DiscountedDailyCost: the rating shaves a percent off the
    /// base, never below <see cref="InsuranceRules.MinimumDailyCost"/>. Example
    /// from the source: CR6.90 base at rating 21 → CR5.45.</summary>
    public static double DiscountedDailyCost(double carPrice, double driverRating, InsuranceRules rules)
    {
        double baseCost = BaseDailyCost(carPrice, rules);
        double pct = double.IsFinite(driverRating) ? Math.Clamp(driverRating, 0.0, 100.0) : 0.0;
        return Math.Max(rules.MinimumDailyCost, baseCost - baseCost * pct / 100.0);
    }
}
