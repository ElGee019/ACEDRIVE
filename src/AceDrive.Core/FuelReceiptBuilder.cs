// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Prices one settled fill (PORTPLAN items 37/38).
namespace AceDrive.Core;

/// <summary>
/// Unity's FuelStationScreen.OnFuelClicked arithmetic on a fill the game
/// itself pumped: <c>billable litres × CR/L</c>, with a Free Tank Refill card
/// zeroing the bill and a roadside repair's prepaid litres taken off the top
/// (PORTPLAN 38). Kept in Core so the rounding rule is testable and the flow
/// stays an event coordinator.
/// </summary>
public static class FuelReceiptBuilder
{
    public static FuelReceipt Build(
        double litres, double pricePerLitre, bool free, FriendshipResult? friendship,
        double prepaidLitres = 0.0)
    {
        double prepaid = double.IsFinite(prepaidLitres)
            ? Math.Clamp(prepaidLitres, 0.0, Math.Max(0.0, litres))
            : 0.0;
        double billable = Math.Max(0.0, litres - prepaid);
        double total = free
            ? 0.0
            : Math.Round(
                Math.Max(0.0, billable * pricePerLitre), 2, MidpointRounding.AwayFromZero);
        return new FuelReceipt(litres, pricePerLitre, total, free, friendship, prepaid);
    }
}
