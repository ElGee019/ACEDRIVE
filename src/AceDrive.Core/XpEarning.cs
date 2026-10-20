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

    public static double Compute(
        double distanceKm, int carClass, int carRarity,
        int streetKnowledgePct, double currentXp)
    {
        double raw = distanceKm * carClass * carRarity / 100.0
            * (streetKnowledgePct / 100.0);
        return Diminish(raw, currentXp);
    }

    /// <summary>
    /// Delivery XP (Unity ApplyStoreDeliveryEffects): distance / 100 scaled
    /// by street knowledge, then the same diminishing returns — no car
    /// class or rarity, the ride hauls goods, not passengers.
    /// </summary>
    public static double ComputeDelivery(
        double distanceKm, int streetKnowledgePct, double currentXp)
    {
        double raw = Math.Max(0.0, distanceKm) / 100.0
            * (Math.Clamp(streetKnowledgePct, 0, 100) / 100.0);
        return Diminish(raw, currentXp);
    }

    public static double GetMultiplier(double currentXp)
    {
        int tiers = (int)Math.Floor(Math.Max(0.0, currentXp) / TierWidth);
        return Math.Max(1.0 - tiers * TierPenalty, MinMultiplier);
    }

    public static double Diminish(double delta, double currentXp) =>
        delta * GetMultiplier(currentXp);
}
