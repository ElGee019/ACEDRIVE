// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: ToWhereIsWilly.md §2.5/§3.1 (Unity
// WhereIsWillyHandler.CalculateReward + WillyReceiptScreen's line math).
namespace AceDrive.Core;

/// <summary>
/// Prices one settled missing-person run from the inputs the flow already
/// resolved. The rules are applied once, here, so the receipt page and the flow
/// always print the same fee, multipliers and base the fee was computed from;
/// the page never re-derives a number.
/// </summary>
public static class WhereIsWillyReceiptBuilder
{
    public static WhereIsWillyReceipt Build(
        WhereIsWillyRules rules,
        Resident person,
        PointOfInterest shop,
        int attempts,
        IEnumerable<CarSpec?> ownedCars,
        int shopCount,
        int week,
        int year,
        int friendshipLevel)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(shop);

        int visits = Math.Max(0, attempts);
        int shops = Math.Max(0, shopCount);
        double average = WhereIsWillyRules.AverageCarValue(ownedCars);
        return new WhereIsWillyReceipt(
            person.Name.Full,
            person.Name.FirstName,
            shop.Name,
            rules.Reward(average, shops, visits),
            average,
            average * rules.FinderFeeRate,
            shops,
            rules.ShopMultiplier(shops),
            visits,
            rules.AttemptMultiplier(shops, visits),
            week,
            year,
            Math.Max(0, friendshipLevel));
    }
}
