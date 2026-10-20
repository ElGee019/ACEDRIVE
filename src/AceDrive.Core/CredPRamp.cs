// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (Unity
// DeliveryJobHandler CredP ramp: startRates/caps by hub tier and level).
namespace AceDrive.Core;

/// <summary>
/// Balance table behind the per-delivery CredP ramp: each completed
/// delivery earns the producing hub's level start rate (scaled by tier),
/// capped at its level cap (also scaled by tier). Indexed from 1 like
/// Unity's arrays; an owned hub's level is clamped into the table.
/// </summary>
public sealed record CredPRamp(IReadOnlyList<int> StartRates, IReadOnlyList<int> Caps)
{
    /// <summary>Unity's hardcoded numbers, kept as the built-in fallback the
    /// JSON overrides; not the source of truth.</summary>
    public static readonly CredPRamp UnityDefault = new(
        [1, 2, 4, 7, 12],
        [10, 20, 40, 70, 120]);

    /// <summary>CredP for one delivery: Unity's min(startRate + count, cap)
    /// with both scaled by the hub tier before the cap compares.</summary>
    public int Earn(int tier, int level, int deliveriesBefore)
    {
        int scaledTier = Math.Max(1, tier);
        int rateLevel = Math.Clamp(level, 1, StartRates.Count);
        int startRate = StartRates[rateLevel - 1] * scaledTier;
        int cap = Caps[Math.Clamp(rateLevel - 1, 0, Caps.Count - 1)] * scaledTier;
        return Math.Min(startRate + Math.Max(0, deliveriesBefore), cap);
    }
}
