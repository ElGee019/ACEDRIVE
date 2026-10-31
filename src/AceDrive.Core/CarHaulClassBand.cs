// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: Unity CarHaulOfferService.ClassRangeForPct (car-hauling offer,
// PORTPLAN item 20), ported 1:1 with double maths.
namespace AceDrive.Core;

/// <summary>
/// The car-class window a brand's loyalty percent opens for a haul offer: the
/// customer's new car is drawn from this band, so a loyal customer's brand
/// delivers a better car. Unity compares absolute percentages (100% = the
/// unlock floor), never fractions, because the offer feeds it the percentage
/// its loyalty curve produces.
/// </summary>
public readonly record struct CarHaulClassBand(int MinClass, int MaxClass)
{
    /// <summary>
    /// Unity's inclusive bands, in order: everything below 200% (and therefore
    /// below the 100% unlock) answers the lowest band, and 900% or more holds
    /// the top band. A percent that is not a number falls through to the top
    /// band exactly like Unity's float comparisons do.
    /// </summary>
    public static CarHaulClassBand For(double loyaltyPercent)
    {
        if (loyaltyPercent < 200.0)
        {
            return new CarHaulClassBand(1, 6);
        }

        if (loyaltyPercent < 300.0)
        {
            return new CarHaulClassBand(1, 8);
        }

        if (loyaltyPercent < 400.0)
        {
            return new CarHaulClassBand(1, 10);
        }

        if (loyaltyPercent < 500.0)
        {
            return new CarHaulClassBand(1, 12);
        }

        if (loyaltyPercent < 600.0)
        {
            return new CarHaulClassBand(1, 14);
        }

        if (loyaltyPercent < 700.0)
        {
            return new CarHaulClassBand(1, 16);
        }

        if (loyaltyPercent < 800.0)
        {
            return new CarHaulClassBand(5, 17);
        }

        return loyaltyPercent < 900.0
            ? new CarHaulClassBand(6, 21)
            : new CarHaulClassBand(7, 21);
    }

    /// <summary>True when the class sits inside the inclusive band.</summary>
    public bool Contains(int carClass) => carClass >= MinClass && carClass <= MaxClass;
}
