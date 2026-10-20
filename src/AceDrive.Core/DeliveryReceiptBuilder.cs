// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 16 (Unity
// DeliveryJobHandler.ApplyStoreDeliveryEffects + DeliveryReceiptScreen).
namespace AceDrive.Core;

/// <summary>
/// Builds a settled delivery receipt from the run's inputs: the tiered
/// distance charge, the goods money (wholesale out, retail payout in) and
/// the delivery XP (distance / 100 × street knowledge, then diminishing —
/// Unity applies no car class or rarity here, unlike the taxi). Skips what
/// has no system here yet (starter bonus, profit-boost and free-tank-refill
/// cards, auto fuel): those arrive as new receipt fields with their own
/// systems, never as branches in this method.
/// </summary>
public static class DeliveryReceiptBuilder
{
    public static DeliveryReceipt Build(
        double distanceKm,
        double paidForGoods,
        double receivedForGoods,
        DeliveryFareTable table,
        int streetKnowledgePct,
        double currentXp,
        FriendshipResult? friendship = null,
        int credPEarned = 0,
        double totalCredP = 0.0)
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
        double totalEarned = Cents(Math.Max(0.0, receivedForGoods) - Math.Max(0.0, paidForGoods) + charge);
        double xp = XpEarning.ComputeDelivery(distanceKm, streetKnowledgePct, currentXp);
        return new DeliveryReceipt(
            distanceKm,
            lines,
            charge,
            Cents(Math.Max(0.0, paidForGoods)),
            Cents(Math.Max(0.0, receivedForGoods)),
            totalEarned,
            xp,
            Math.Clamp(streetKnowledgePct, 0, 100),
            friendship?.OwnerName ?? "",
            friendship?.Level ?? 0,
            friendship?.TierIncreased ?? false,
            Math.Max(0, credPEarned),
            Math.Max(0.0, totalCredP));
    }

    private static double Cents(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
