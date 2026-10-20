// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 20 (Unity
// CarHaulingScreen.TierForCarClass + CalculateEarnings; settles in Unity
// MapScreen's CarHaulArrivedAtCustomer block).
namespace AceDrive.Core;

/// <summary>
/// Builds a settled car-hauling receipt from the drive's inputs. The pay
/// is time-based, not distance-based: the hauled car's class band scales
/// the driver rating into a per-minute rate, and the total is that rate
/// times the delivery leg's minutes (Unity stamps both leg ends in
/// in-game minutes). Skips what has no system here (dealer familiarity
/// until the dealer POIs land, positional job gating): those arrive as new
/// receipt fields with their own systems, never as branches in this method.
/// </summary>
public static class CarHaulReceiptBuilder
{
    /// <summary>Base pay in EUR per minute of delivery driving.</summary>
    public const double BaseRatePerMin = 0.83;

    public static CarHaulReceipt Build(
        string carName,
        int carClass,
        int driverRating,
        int driveMinutes,
        string dealerOwnerName = "",
        int dealerFriendshipLevel = 0,
        bool dealerFriendshipTierIncreased = false)
    {
        int tier = TierForCarClass(carClass);
        // Unity: rate/min is driverRating * tier percent of the base price.
        double ratePerMin = BaseRatePerMin * (driverRating * tier / 100.0);
        double minutes = Math.Max(0, driveMinutes);
        double total = Cents(ratePerMin * minutes);
        return new CarHaulReceipt(
            carName,
            tier,
            Math.Clamp(driverRating, 0, 100),
            Cents(ratePerMin),
            total,
            Math.Max(0, driveMinutes),
            dealerOwnerName,
            Math.Max(0, dealerFriendshipLevel),
            dealerFriendshipTierIncreased);
    }

    /// <summary>
    /// Pay tier from the hauled car's class (inclusive bands):
    /// 1-6 → 1, 7-8 → 2, 9-10 → 3, 11-12 → 4, 13-14 → 5, 15-16 → 6, 17+ → 7.
    /// </summary>
    public static int TierForCarClass(int carClass) => carClass switch
    {
        >= 17 => 7,
        >= 15 => 6,
        >= 13 => 5,
        >= 11 => 4,
        >= 9 => 3,
        >= 7 => 2,
        _ => 1,
    };

    private static double Cents(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
