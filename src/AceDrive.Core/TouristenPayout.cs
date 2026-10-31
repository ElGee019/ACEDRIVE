// Pure gameplay pricing for the Saturday Touristenfahrten run: what the shop
// pays for one delivered order, including the real-world race weekend's bonus.
// No WPF, no time source — the caller passes the factor in.
namespace AceDrive.Core;

/// <summary>
/// The one money rule a delivered Touristenfahrten order has: its own goods
/// total, scaled by the race calendar's factor (1.0 on an ordinary day, the
/// calendar's bonus while a real-world event runs) and rounded to cents like
/// every other money line.
/// </summary>
public static class TouristenPayout
{
    /// <summary>
    /// The shop's payout for <paramref name="order"/>. A factor that is not a
    /// usable number can only leave the price alone, never make a delivery
    /// cheaper than the goods it carried.
    /// </summary>
    public static double Of(TouristenOrder order, double multiplier)
    {
        ArgumentNullException.ThrowIfNull(order);

        double factor = double.IsFinite(multiplier) ? Math.Max(1.0, multiplier) : 1.0;
        return Math.Round(order.Total * factor, 2, MidpointRounding.AwayFromZero);
    }
}
