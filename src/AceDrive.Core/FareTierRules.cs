// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11/16 (the
// fare-tier shape both the taxi and delivery tables share).
namespace AceDrive.Core;

/// <summary>
/// Invariants a fare-tier table must hold to price a distance: rates are
/// never negative and only the open top tier may carry a non-positive
/// width (the JSON's way of saying "all remaining km"). Shared by the taxi
/// and delivery tables so their loaders can never diverge.
/// </summary>
public static class FareTierRules
{
    public static bool Valid(IReadOnlyList<FareTier> tiers)
    {
        if (tiers.Count == 0)
        {
            return false;
        }
        for (int i = 0; i < tiers.Count; i++)
        {
            FareTier tier = tiers[i];
            bool isLast = i == tiers.Count - 1;
            if ((!isLast && tier.WidthKm <= 0.0) || tier.RatePerKm < 0.0)
            {
                return false;
            }
        }
        return true;
    }
}
