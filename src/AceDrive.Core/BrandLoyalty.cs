// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 35 (Unity
// TaxiReceiptScreen.CalculateBrandLoyaltyBonus, ported 1:1 including the
// halving phases and the 1000% cap; double math replaces Mathf).
namespace AceDrive.Core;

/// <summary>
/// Brand sponsorship rate for the driven car's brand. Grows with the
/// lifetime km driven in that brand's cars (tracked by the mileage
/// store): 1% below 1 km, escalating to 100% and halving at every
/// doubling boundary past it (200%, 400%, ...), capped at 1000%.
/// </summary>
public static class BrandLoyalty
{
    /// <summary>Maximum payout rate: 10.00 (1000%).</summary>
    public const double MaxRate = 10.0;

    /// <summary>Sponsorship rate as a fraction (0.05 = 5%).</summary>
    public static double Rate(double brandDistanceKm)
    {
        // The bonus grows at half the rate of real distance: every
        // sponsorship tier takes twice the km to reach. Real km stay
        // truthful on the receipt; only the curve input is scaled.
        brandDistanceKm *= 0.5;

        if (brandDistanceKm < 1.0)
        {
            return 0.01;
        }

        if (brandDistanceKm <= 5.0)
        {
            return 0.01 + Math.Floor(brandDistanceKm) * 0.01;
        }

        double remaining = brandDistanceKm - 5.0;
        int groupNumber = 0;
        double totalBonus = 0.05;
        double kmPerStep;
        double bonusPerStep;

        // Escalating groups below 100%. The loop consumes whole steps and
        // hands off to the halving phase once the bonus is about to pass
        // the 100% boundary.
        while (true)
        {
            kmPerStep = 3.0 + groupNumber;
            bonusPerStep = 0.02 + 0.01 * groupNumber;
            double groupSize = kmPerStep * 5.0;

            if (remaining > groupSize)
            {
                remaining -= groupSize;
                totalBonus += 5.0 * bonusPerStep;
                groupNumber++;
                continue;
            }

            // Whole steps that fit in the remaining km, never past 100%.
            int steps = (int)Math.Floor(remaining / kmPerStep);
            double stepsToHundred = (1.00 - totalBonus) / bonusPerStep;
            int bounded = Math.Min(steps, (int)Math.Floor(stepsToHundred));
            if (bounded > 0)
            {
                totalBonus += bounded * bonusPerStep;
                remaining -= bounded * kmPerStep;
                continue;
            }

            // The 100% boundary cuts through the next step: finish it at
            // the old rate, then hand the rest to the halving phase.
            double kmToHundred = stepsToHundred * kmPerStep;
            if (remaining >= kmToHundred)
            {
                totalBonus = 1.00;
                remaining -= kmToHundred;
                break;
            }

            return Math.Min(totalBonus, MaxRate);
        }

        // 100% and beyond: halve the rate at every doubling boundary
        // (200%, 400%, 800%...), capped at 1000%.
        kmPerStep *= 2.0;
        double nextBoundary = 2.00;

        while (remaining > 0.0 && totalBonus < MaxRate)
        {
            double kmToBoundary = (nextBoundary - totalBonus) / bonusPerStep * kmPerStep;

            if (remaining < kmToBoundary)
            {
                int steps = (int)Math.Floor(remaining / kmPerStep);
                totalBonus += steps * bonusPerStep;
                return Math.Min(totalBonus, MaxRate);
            }

            remaining -= kmToBoundary;
            totalBonus = nextBoundary;

            kmPerStep *= 2.0;
            nextBoundary = Math.Min(nextBoundary * 2.0, MaxRate);
        }

        return Math.Min(totalBonus, MaxRate);
    }
}
