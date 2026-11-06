// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 16 (Unity
// DeliveryJobHandler.ApplyStoreDeliveryEffects + DeliveryReceiptScreen).
namespace AceDrive.Core;

/// <summary>
/// Builds a settled delivery receipt from the run's inputs: the tiered
/// distance charge, the goods money (wholesale out, retail payout in) and
/// the delivery XP (the taxi formula at <see cref="XpEarning.DeliveryShare"/>:
/// distance × class × rarity / 100 × street knowledge, then diminishing).
/// Skips what has no system here yet (free-tank-refill cards, auto fuel):
/// those arrive as new receipt fields with their own systems, never as
/// branches in this method. <paramref name="profitBoostMultiplier"/>
/// is the Delivery Profit Boost card's scale on the goods payout; the charge is
/// never boosted (Unity boosts the goods earnings only). The starter bonus
/// (PORTPLAN 47) is priced on the run's own profit, `max(0, gross − wholesale)`
/// — Unity keys it to what the ride actually made, so the wholesale CR the
/// goods cost can never inflate it; a null <paramref name="zoneKm"/> pays none.
/// </summary>
public static class DeliveryReceiptBuilder
{
    public static DeliveryReceipt Build(
        double distanceKm,
        double paidForGoods,
        double receivedForGoods,
        DeliveryFareTable table,
        int carClass,
        int carRarity,
        int streetKnowledgePct,
        double currentXp,
        FriendshipResult? friendship = null,
        int credPEarned = 0,
        double totalCredP = 0.0,
        bool awardsXp = true,
        double profitBoostMultiplier = 1.0,
        double? zoneKm = null,
        int ownedCars = 0)
    {
        distanceKm = Math.Max(0.0, distanceKm);
        List<ReceiptTier> lines = FareTierLines.Of(distanceKm, table.Tiers);
        // The charge totals the exact lines the page shows, so receipt rows
        // add up on the page (Unity sums the same walked tiers).
        double charge = 0.0;
        foreach (ReceiptTier line in lines)
        {
            charge += line.Charge;
        }
        charge = Cents(charge);
        double goods = Math.Max(0.0, receivedForGoods);
        double boost = Math.Max(1.0, profitBoostMultiplier);
        bool boosted = boost > 1.0;
        goods = Cents(goods * boost);
        double totalEarned = Cents(goods - Math.Max(0.0, paidForGoods) + charge);
        // Unity's delivery starter bonus: the taxi's rate function on this
        // run's profit base, added on top and banked with the total. Null
        // means no zone yet, which pays nothing (Unity's 0 km zone would
        // read 100%; a receipt before onboarding has no business to support).
        double starterRate = 0.0;
        double starterBonus = 0.0;
        if (zoneKm is { } operatingZoneKm)
        {
            starterRate = StarterBonus.Percentage(operatingZoneKm, ownedCars);
            starterBonus = Cents(
                StarterBonus.Amount(Math.Max(0.0, totalEarned), operatingZoneKm, ownedCars));
            totalEarned = Cents(totalEarned + starterBonus);
        }
        // An unowned car pays the run but earns no player XP: the caller owns
        // the ownership rule, this builder only knows whether to award.
        double xp = awardsXp
            ? XpEarning.ComputeDelivery(
                distanceKm, carClass, carRarity, streetKnowledgePct, currentXp)
            : 0.0;
        return new DeliveryReceipt(
            distanceKm,
            lines,
            charge,
            Cents(Math.Max(0.0, paidForGoods)),
            goods,
            totalEarned,
            xp,
            carClass,
            carRarity,
            Math.Clamp(streetKnowledgePct, 0, 100),
            friendship?.OwnerName ?? "",
            friendship?.Level ?? 0,
            friendship?.TierIncreased ?? false,
            Math.Max(0, credPEarned),
            Math.Max(0.0, totalCredP),
            boosted,
            boosted ? boost - 1.0 : 0.0,
            starterRate,
            starterBonus);
    }

    private static double Cents(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
