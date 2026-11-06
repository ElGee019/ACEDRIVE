// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

/// <summary>
/// A staged cart run handed over at DELIVER: the order lines the cart held.
/// Its money and volume are derived from those lines, never kept beside them,
/// so what the run moves cannot drift from the quantities it delivers. The
/// wholesale sum is charged to cash at the hand-over; the payout sum comes
/// back on the settle receipt; the volume must fit the driven car.
/// </summary>
public sealed record DeliveryRun(IReadOnlyList<DeliveryOrder> Orders)
{
    // Order volumes are priced to the millilitre, so a cart loaded to the
    // exact limit can read a fraction of a millilitre per line over.
    private const double VolumeTolerance = 0.001;

    /// <summary>
    /// The planned route's length, stamped at DELIVER from the stops the run
    /// will drive (PORTPLAN 19), exactly like the taxi job's ACCEPT stamp: the
    /// odometer never enters the charge, so a detour, a shortcut or a longer
    /// way around the lap cannot move the fare. 0 when the map cannot price a
    /// route (a run with no resolvable door).
    /// </summary>
    public double DistanceKm { get; init; }

    /// <summary>Wholesale CR the run pays for the goods (Unity's cart total).</summary>
    public double PaidForGoods => Sum(order => order.WholesaleCost);

    /// <summary>Retail CR the stores pay back for the goods (Unity's 0.9x take).</summary>
    public double ReceivedForGoods => Sum(order => order.RetailPayout);

    /// <summary>Trunk litres the whole cart takes, rounded like a line.</summary>
    public double TotalVolume => Sum(order => order.Volume, decimals: 3);

    /// <summary>True when the cart's goods fit the given free trunk litres.</summary>
    public bool FitsIn(double freeLitres) =>
        TotalVolume <= freeLitres + VolumeTolerance;

    private double Sum(Func<DeliveryOrder, double> amount, int decimals = 2)
    {
        double total = 0.0;
        foreach (DeliveryOrder order in Orders)
        {
            total += amount(order);
        }
        return Math.Round(total, decimals, MidpointRounding.AwayFromZero);
    }
}
