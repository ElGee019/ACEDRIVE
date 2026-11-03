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
/// Skips what has no system here yet (starter bonus, free-tank-refill cards,
/// auto fuel): those arrive as new receipt fields with their own systems,
/// never as branches in this method. <paramref name="profitBoostMultiplier"/>
/// is the Delivery Profit Boost card's scale on the goods payout; the charge is
/// never boosted (Unity boosts the goods earnings only).
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
        double profitBoostMultiplier = 1.0)
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
            boosted ? boost - 1.0 : 0.0);
    }

    private static double Cents(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
