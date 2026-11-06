// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The roadside refuel a dry tank buys (PORTPLAN item 38, Unity MapScreen's
// OnFuelOutDetected + RepairScreen):
//   litres needed × the pump price + the mechanic's service fee.
namespace AceDrive.Core;

/// <summary>
/// One fuel-out repair quote (Unity <c>CalculateOutOfFuelRepairCost</c>): the
/// litres a full tank still needs, the CR/L the market charges today and the
/// mechanic's flat service fee. Unity multiplies by the most expensive fuel
/// station's <c>priceMultiplier</c>; this port has one market price for every
/// forecourt (PORTPLAN 37), so that multiplier is always 1 here.
/// </summary>
public sealed record FuelOutage(
    double LitresNeeded,
    double PricePerLitre,
    double ServiceFee)
{
    /// <summary>
    /// Unity's bill: <c>litresNeeded × fuelPrice × maxMultiplier + 100</c>,
    /// rounded to the cent the ledger uses. A malformed reading (negative or
    /// non-finite) prices as zero fuel plus the service fee instead of throwing
    /// at a roadside.
    /// </summary>
    public double RepairCost =>
        Math.Round(
            Math.Max(0.0, LitresNeeded) * Math.Max(0.0, PricePerLitre)
            + Math.Max(0.0, ServiceFee),
            2,
            MidpointRounding.AwayFromZero);

    /// <summary>Quotes a fill from the current tank to full (never negative).</summary>
    public static FuelOutage Quote(
        double tankLitres, double fuelLitres, double pricePerLitre, double serviceFee)
    {
        // A torn read is treated as an empty tank: the player is on the
        // roadside and needs the full fill, not a free repair because a float
        // came back NaN.
        double tank = double.IsFinite(tankLitres) ? Math.Max(0.0, tankLitres) : 0.0;
        double fuel = double.IsFinite(fuelLitres) ? Math.Max(0.0, fuelLitres) : 0.0;
        return new FuelOutage(Math.Max(0.0, tank - fuel), pricePerLitre, serviceFee);
    }
}
