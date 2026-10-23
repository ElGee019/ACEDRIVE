// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 06, Unity
// Driving/Services/DriverRatingService.cs (+ DriverRatingProfile.cs).
namespace AceDrive.Core;

/// <summary>
/// The driver's three skill shares (Unity DriverRatingProfile): cornering,
/// concentration and off-the-line, each a percentage in (11, 100]. The
/// overall rating is their plain average (Unity DriverRatingService.Overall).
/// Immutable: the wallet holds the persisted values, and a future skill-event
/// system replaces them through that same owner instead of mutating a copy
/// the UI is reading.
/// </summary>
public readonly record struct DriverRating(
    double Cornering,
    double Concentration,
    double OffTheLine)
{
    /// <summary>Exclusive lower bound: a skill can never be 11 or below.</summary>
    public const double MinStat = 11.0;

    /// <summary>Inclusive upper bound: a skill can never exceed 100.</summary>
    public const double MaxStat = 100.0;

    /// <summary>Smallest representable valid value, just above MinStat.</summary>
    private const double MinValid = 11.01;

    /// <summary>Overall rating a brand-new game starts with (Unity
    /// DriverRatingService.NewGameOverall). Unity spreads the three skills
    /// randomly around this sum; the port seeds them flat because onboarding
    /// does not roll a profile yet, and a flat seed keeps the save
    /// deterministic.</summary>
    public const double NewGameOverall = 20.0;

    /// <summary>The fresh-driver profile: overall exactly 20, like Unity's
    /// random new-game spread.</summary>
    public static DriverRating NewGame => new(NewGameOverall, NewGameOverall, NewGameOverall);

    /// <summary>Unity Overall: the average of the three skills.</summary>
    public double Overall => (Cornering + Concentration + OffTheLine) / 3.0;

    /// <summary>
    /// Reads the persisted skills through Unity's clamp: a value at or below
    /// the exclusive floor (0 from an old or hand-edited save) falls back to
    /// the smallest legal stat, NaN included; anything above 100 caps there.
    /// </summary>
    public static DriverRating FromSaved(double cornering, double concentration, double offTheLine) =>
        new(
            ClampStat(cornering),
            ClampStat(concentration),
            ClampStat(offTheLine));

    /// <summary>Unity DriverRatingService.ClampStat.</summary>
    public static double ClampStat(double value, double fallback = MinValid)
    {
        if (double.IsNaN(value) || value <= MinStat)
        {
            return fallback;
        }
        return Math.Clamp(value, MinValid, MaxStat);
    }
}
