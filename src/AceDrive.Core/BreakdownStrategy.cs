// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN item 39, port of Unity
// Driving/Services/StandardBreakdownStrategy.cs — one roll at a trip start
// against the wear-derived chance, the hit landing at a random point of the
// route. The per-job consequences and the outcome screen stay with the host.
namespace AceDrive.Core;

/// <summary>
/// The trip-start breakdown roll (Unity StandardBreakdownStrategy): given the
/// current car's breakdown chance, either nothing or a fraction of the route
/// (10%..90%) at which the car dies. Randomness is a seeded instance — never
/// the shared global (AGENTS.md) — so tests pin the seed.
/// </summary>
public sealed class BreakdownStrategy
{
    private readonly MaintenanceRules _rules;
    private readonly Random _rng;

    public BreakdownStrategy(MaintenanceRules rules, int seed)
    {
        _rules = rules;
        _rng = new Random(seed);
    }

    /// <summary>Unity TryTriggerBreakdown: null when the chance is 0 or the
    /// roll misses; otherwise the route fraction (min..max) at which the car
    /// breaks down. A chance at or above 1 always triggers.</summary>
    public double? TryTrigger(double breakdownChance)
    {
        if (breakdownChance <= 0.0 || _rng.NextDouble() >= breakdownChance)
        {
            return null;
        }
        double span = _rules.BreakdownFractionMax - _rules.BreakdownFractionMin;
        return _rules.BreakdownFractionMin + _rng.NextDouble() * span;
    }
}
