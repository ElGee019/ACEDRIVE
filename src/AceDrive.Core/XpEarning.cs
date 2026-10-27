// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 43 (Unity
// XpEarningHelper + TaxiJobLifecycleHandler earnedXP block).
namespace AceDrive.Core;

/// <summary>
/// Taxi XP awards with progressive diminishing returns: once the driver's
/// XP reaches 100 the multiplier for newly earned XP drops to 99%, at 200
/// to 98%, and so on, floored at 1%. Evaluated against the XP before the
/// award is applied, fixed at the moment it is earned.
/// </summary>
public static class XpEarning
{
    private const double TierWidth = 100.0;
    private const double TierPenalty = 0.01;
    private const double MinMultiplier = 0.01;

    /// <summary>
    /// What a delivery keeps of the taxi award for the same drive: a fifth.
    /// WHY: Unity paid the goods run on distance alone (no class/rarity), so
    /// the same car and route earned an order of magnitude less on a
    /// delivery than on a passenger ride and the job felt like no progress.
    /// A fifth keeps the delivery clearly the lesser job without stalling the
    /// career.
    /// </summary>
    public const double DeliveryShare = 0.2;

    public static double Compute(
        double distanceKm, int carClass, int carRarity,
        int streetKnowledgePct, double currentXp)
    {
        double raw = distanceKm * carClass * carRarity / 100.0
            * (streetKnowledgePct / 100.0);
        return Diminish(raw, currentXp);
    }

    /// <summary>
    /// Delivery XP: the taxi formula at <see cref="DeliveryShare"/> — the
    /// same distance, car class, rarity and street knowledge, then the same
    /// diminishing returns. The car still hauls the goods, so it still
    /// supplies the class/rarity terms.
    /// </summary>
    public static double ComputeDelivery(
        double distanceKm, int carClass, int carRarity,
        int streetKnowledgePct, double currentXp) =>
        Compute(
            Math.Max(0.0, distanceKm), carClass, carRarity,
            Math.Clamp(streetKnowledgePct, 0, 100), currentXp) * DeliveryShare;

    public static double GetMultiplier(double currentXp)
    {
        int tiers = (int)Math.Floor(Math.Max(0.0, currentXp) / TierWidth);
        return Math.Max(1.0 - tiers * TierPenalty, MinMultiplier);
    }

    public static double Diminish(double delta, double currentXp) =>
        delta * GetMultiplier(currentXp);
}
