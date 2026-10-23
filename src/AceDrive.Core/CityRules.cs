// Balance table for the town's economy (PORTPLAN item 25, port of Unity's
// StoreEconomyConfig asset): every edge the citizen tick compares against,
// the cadence and the demand ladder live here, never in code. Stocked-unit
// capacity is derived per store from the shop catalogue layout (Unity's single
// defaultMaxStock became a per-store number in this port).
namespace AceDrive.Core;

/// <summary>
/// Citizen growth and demand rules. Percent edges are shelf fill
/// (0-100), deltas are citizens per <see cref="CitizenTickMinutes"/>.
/// </summary>
/// <param name="DefaultCitizens">Citizens a store starts with (Unity's
/// defaultCitizens: the app opens on 100 per store).</param>
/// <param name="MinCitizens">Floor a declining store stops at.</param>
/// <param name="CitizenTickMinutes">Game minutes between citizen adjustments.</param>
/// <param name="DepletionTickMinutes">Game minutes between demand pulls.</param>
/// <param name="HighPercent">At or above this fill the store thrives.</param>
/// <param name="MediumPercent">At or above this fill it grows slowly.</param>
/// <param name="LowPercent">Above this (and under medium) it holds steady.</param>
/// <param name="VeryLowPercent">Above this (and under low) it declines slowly.</param>
/// <param name="HighDelta">Citizens per tick at high fill.</param>
/// <param name="MediumDelta">Citizens per tick at medium fill.</param>
/// <param name="NeutralDelta">Citizens per tick while steady.</param>
/// <param name="LowDelta">Citizens per tick at low fill.</param>
/// <param name="VeryLowDelta">Citizens per tick at empty shelves.</param>
/// <param name="DepletionFirstCitizens">Citizens at which demand rises to two
/// units an hour: the first rung of the ladder.</param>
/// <param name="DepletionStepCitizens">Extra citizens each further unit of
/// demand costs, on top of the previous rung.</param>
public sealed record CityRules(
    int DefaultCitizens,
    int MinCitizens,
    int CitizenTickMinutes,
    int DepletionTickMinutes,
    double HighPercent,
    double MediumPercent,
    double LowPercent,
    double VeryLowPercent,
    double HighDelta,
    double MediumDelta,
    double NeutralDelta,
    double LowDelta,
    double VeryLowDelta,
    int DepletionFirstCitizens,
    int DepletionStepCitizens)
{
    /// <summary>Unity's shipped StoreEconomyConfig asset, unchanged. Fallback
    /// for tests and for a missing or invalid Data/city_rules.json — the
    /// loaded file is the source of truth.</summary>
    public static readonly CityRules Default = new(
        DefaultCitizens: 100,
        MinCitizens: 70,
        CitizenTickMinutes: 30,
        DepletionTickMinutes: 90,
        HighPercent: 75,
        MediumPercent: 60,
        LowPercent: 40,
        VeryLowPercent: 25,
        HighDelta: 1,
        MediumDelta: 0.5,
        NeutralDelta: 0,
        LowDelta: -0.5,
        VeryLowDelta: -1,
        DepletionFirstCitizens: 200,
        DepletionStepCitizens: 100);
}
