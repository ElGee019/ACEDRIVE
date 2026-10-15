// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11 (Unity
// TaxiReceiptScreen.CalculateEarnings + TaxiJobLifecycleHandler XP block).
namespace AceDrive.Core;

/// <summary>
/// Builds a settled receipt from dropoff inputs. Skips what has no system
/// here yet (brand loyalty, starter bonus, auto fuel, double-tip card):
/// those arrive as new receipt fields with their own systems, never as
/// branches in this method.
/// </summary>
public static class TaxiReceiptBuilder
{
    public static TaxiReceipt Build(
        double distanceKm,
        int capacity,
        int carClass,
        int carRarity,
        int streetKnowledgePct,
        bool tipEligible,
        int fines,
        FareTable table,
        double currentXp,
        FriendshipResult? friendship = null)
    {
        // Unity scales the distance charge by passenger capacity (the
        // forfait stays flat): a 4-seater runs the same ride at 4x.
        int seats = Math.Max(1, capacity);
        List<TaxiReceiptTier> lines = BuildTierLines(distanceKm, table, seats);
        double charge = Cents(TaxiFare.ComputeDistanceCharge(distanceKm, table) * seats);
        double @base = Cents(table.Forfait + charge);
        double tip = TaxiFare.ComputeTip(@base, tipEligible, table);
        double fine = TaxiFare.ComputeFineDeduction(fines, table);
        double total = Cents(@base + tip - fine);
        double xp = XpEarning.Compute(
            distanceKm, carClass, carRarity, streetKnowledgePct, currentXp);
        return new TaxiReceipt(
            Math.Max(0.0, distanceKm),
            seats,
            lines,
            charge,
            table.Forfait,
            @base,
            tip,
            Math.Max(0, fines),
            fine,
            total,
            xp,
            carClass,
            carRarity,
            streetKnowledgePct,
            friendship?.OwnerName ?? "",
            friendship?.Level ?? 0,
            friendship?.TierIncreased ?? false);
    }

    // Per-km breakdown for the receipt page. Only tiers the ride actually
    // reaches produce a line, like the Unity screen hiding unused rows.
    // Line charges scale with capacity like the total does.
    private static List<TaxiReceiptTier> BuildTierLines(double distanceKm, FareTable table, int capacity)
    {
        List<TaxiReceiptTier> lines = [];
        double remaining = Math.Max(0.0, distanceKm);
        double start = 0.0;
        foreach (FareTier tier in table.Tiers)
        {
            if (remaining <= 0.0)
            {
                break;
            }
            double km = tier.WidthKm <= 0.0 ? remaining : Math.Min(remaining, tier.WidthKm);
            // Binary residue (8.2 − 2 − 3 = 3.1999…): the page shows one
            // decimal, and the record must compare exact in tests.
            km = Math.Round(km, 6);
            lines.Add(new TaxiReceiptTier(
                start, tier.WidthKm, tier.RatePerKm, km,
                Math.Round(km * tier.RatePerKm * capacity, 2, MidpointRounding.AwayFromZero)));
            remaining -= km;
            start += tier.WidthKm;
        }
        return lines;
    }

    private static double Cents(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
