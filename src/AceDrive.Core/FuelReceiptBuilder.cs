// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Prices one settled fill (PORTPLAN item 37).
namespace AceDrive.Core;

/// <summary>
/// Unity's FuelStationScreen.OnFuelClicked arithmetic on a fill the game
/// itself pumped: <c>litres × CR/L</c>, with a Free Tank Refill card zeroing
/// the bill. Kept in Core so the rounding rule is testable and the flow stays
/// an event coordinator.
/// </summary>
public static class FuelReceiptBuilder
{
    public static FuelReceipt Build(
        double litres, double pricePerLitre, bool free, FriendshipResult? friendship)
    {
        double total = free
            ? 0.0
            : Math.Round(
                Math.Max(0.0, litres * pricePerLitre), 2, MidpointRounding.AwayFromZero);
        return new FuelReceipt(litres, pricePerLitre, total, free, friendship);
    }
}
