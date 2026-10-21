// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity ProceduralQuestionService bands).
namespace AceDrive.Core;

/// <summary>
/// One quiz category: the stat it reads, the resx format key its options use,
/// and the tiered question variants. Bands are ordered; the first match wins.
/// A null bound is open on that side, and both bounds are exclusive so the
/// Unity edges (mass 1800 is "medium", 1200 is "medium") stay exact.
/// </summary>
public sealed record CarStatCategory(
    string Key,
    CarStat Stat,
    string UnitKey,
    IReadOnlyList<CarStatBand> Bands)
{
    /// <summary>The band the asked car's value falls in, or null for data gaps.</summary>
    public CarStatBand? BandFor(double value)
    {
        foreach (CarStatBand band in Bands)
        {
            if (band.Matches(value))
            {
                return band;
            }
        }
        return null;
    }
}

/// <summary>
/// One tier of a category (heavy/light/medium, high/normal, ...) with the
/// pre-authored question variants. Bounds are exclusive; null is open.
/// </summary>
public sealed record CarStatBand(
    string Tier,
    IReadOnlyList<string> Variants,
    double? Min = null,
    double? Max = null)
{
    public bool Matches(double value) =>
        (Min is not { } min || value > min)
        && (Max is not { } max || value < max);
}
