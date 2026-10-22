// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11 (Unity
// TaxiReceiptScreen.CalculateEarnings + TaxiJobLifecycleHandler XP block).
namespace AceDrive.Core;

/// <summary>
/// Builds a settled receipt from dropoff inputs. Skips what has no system
/// here yet (auto fuel, double-tip card): those arrive as new receipt fields
/// with their own systems, never as branches in this method. Brand
/// sponsorship rides along via <see cref="BrandLoyalty"/>: zero km (or no
/// brand) means no bonus and the receipt hides the lines. The business
/// starter bonus rides along via <see cref="StarterBonus"/> when the caller
/// supplies the operating zone; a null zone keeps it out of the math.
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
        FriendshipResult? friendship = null,
        string brand = "",
        double brandDistanceKm = 0.0,
        string carKey = "",
        double? zoneKm = null,
        int ownedCars = 0)
    {
        // Unity scales the distance charge by passenger capacity (the
        // forfait stays flat): a 4-seater runs the same ride at 4x.
        int seats = Math.Max(1, capacity);
        List<ReceiptTier> lines = FareTierLines.Of(distanceKm, table.Tiers, seats);
        double charge = Cents(TaxiFare.ComputeDistanceCharge(distanceKm, table) * seats);
        double @base = Cents(table.Forfait + charge);
        // Unity pays the sponsorship on the base (forfait + charge): the
        // rate comes from the lifetime km in the driven brand, rounded to
        // cents like every other money line.
        string brandName = (brand ?? "").Trim();
        double brandKm = Math.Max(0.0, brandDistanceKm);
        double rate = brandName.Length > 0 && brandKm > 0.0 ? BrandLoyalty.Rate(brandKm) : 0.0;
        double brandBonus = rate > 0.0 ? Cents(@base * rate) : 0.0;
        double tip = TaxiFare.ComputeTip(@base, tipEligible, table);
        double fine = TaxiFare.ComputeFineDeduction(fines, table);
        // Unity prices the starter bonus on the settled total (base,
        // sponsorship, tip and fine included), then adds it on top.
        double preBonus = Cents(@base + brandBonus + tip - fine);
        double starterRate = 0.0;
        double starterBonus = 0.0;
        if (zoneKm is { } operatingZoneKm)
        {
            starterRate = StarterBonus.Percentage(operatingZoneKm, ownedCars);
            starterBonus = Cents(StarterBonus.Amount(preBonus, operatingZoneKm, ownedCars));
        }
        double total = Cents(preBonus + starterBonus);
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
            friendship?.TierIncreased ?? false,
            brandName,
            brandKm,
            rate,
            brandBonus,
            starterRate,
            starterBonus,
            carKey ?? string.Empty);
    }

    private static double Cents(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
