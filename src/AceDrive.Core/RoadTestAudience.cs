// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate this
// file instead of the UI. Spec source: PORTPLAN.md item 49 and LATESTDRIVEBS2
// RoadTestAudienceCalculator.
namespace AceDrive.Core;

/// <summary>
/// The road test's audience and earnings arithmetic (PORTPLAN item 49, Unity
/// RoadTestAudienceCalculator): who read the magazine, how fresh the shoot's
/// location was, the car journalist's reputation, the magazine price and what
/// the issue paid. Returns NUMBERS only — the receipt page formats the
/// sentences from the resource files (AGENTS.md: Core never returns copy).
/// The money comes in as <see cref="RoadTestAudienceRules"/>, never as literals.
/// </summary>
public static class RoadTestAudience
{
    /// <summary>
    /// Unity's NeutralResultReputation: the reading the car journalist formula
    /// starts from. The two legs publish no placement in this port, so both
    /// outcomes stay at <see cref="NeutralOutcome"/> and the reputation lands
    /// exactly on this constant, the same neutral answer Unity computes before
    /// a board read.
    /// </summary>
    public const int NeutralResultReputation = 50;

    /// <summary>Unity's neutral leg outcome (its <c>_accelOutcome</c> default).</summary>
    public const double NeutralOutcome = 0.5;

    /// <summary>Familiarity that makes a resident a friend (Unity's 5+ band).</summary>
    public const int FriendLevel = 5;

    /// <summary>Familiarity that makes a resident an acquaintance (Unity's 2-4 band).</summary>
    public const int AcquaintanceLevel = 2;

    /// <summary>Unity stops raising the natural reputation at this level.</summary>
    public const int MaxNaturalReputation = 100;

    /// <summary>
    /// Unity CalculatePeopleReached: friends count whole, acquaintances count
    /// half, and the pair is a percentage of the town. WHY ToEven: Unity rounds
    /// through Mathf.RoundToInt, which is .NET's banker's rounding, so a
    /// half-way reading lands the same digit here.
    /// </summary>
    public static int PeopleReached(int citizens, int friends, int acquaintances)
    {
        double reached = (friends + 0.5 * acquaintances) * citizens / 100.0;
        return (int)Math.Round(reached);
    }

    /// <summary>Unity CalculateMagazinePrice: the journalist reputation divided.</summary>
    public static double MagazinePrice(
        double journalistReputation, RoadTestAudienceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.MagazinePriceDivisor > 0.0
            ? journalistReputation / rules.MagazinePriceDivisor
            : 0.0;
    }

    /// <summary>Unity's commission line: the crowd's copies times the share.</summary>
    public static double Commission(
        int peopleReached, double magazinePrice, RoadTestAudienceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return peopleReached * magazinePrice * rules.CommissionPercent / 100.0;
    }

    /// <summary>Unity CalculateEarnings: the forfeit plus that commission.</summary>
    public static double Earnings(
        int peopleReached, double magazinePrice, RoadTestAudienceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.BaseForfeit + Commission(peopleReached, magazinePrice, rules);
    }

    /// <summary>
    /// Unity CalculateResultReputation: the two legs' outcomes averaged into a
    /// 1..100 reputation, never zero (a finished test always counts).
    /// </summary>
    public static int ResultReputation(double accelOutcome, double decelOutcome)
    {
        int reputation = (int)Math.Round(100.0 * (accelOutcome + decelOutcome) / 2.0);
        return Math.Max(1, reputation);
    }

    /// <summary>
    /// Unity MarkCurrentAsTested's reputation tail: one point per tested car,
    /// capped at <see cref="MaxNaturalReputation"/>.
    /// </summary>
    public static int NextNaturalReputation(int current) =>
        current < MaxNaturalReputation ? current + 1 : current;

    /// <summary>
    /// Unity CalculateCarJournalistReputation: the natural reputation scaled by
    /// the test result and by the square root of the variety.
    /// </summary>
    public static double CarJournalistReputation(
        int naturalReputation, int resultReputation, double variety) =>
        naturalReputation * resultReputation * Math.Sqrt(Math.Max(0.0, variety));

    /// <summary>
    /// Unity CalculateVariety: how far this shoot is from the last one, as a
    /// fraction of the zone's widest span. A first shoot is full variety by
    /// definition; a shoot on the last one's spot keeps Unity's 0.001 floor
    /// instead of paying nothing at all.
    /// </summary>
    public static double Variety(
        bool hasPrevious, double distanceMeters, double maxDistanceMeters)
    {
        if (!hasPrevious
            || !double.IsFinite(distanceMeters)
            || !double.IsFinite(maxDistanceMeters)
            || maxDistanceMeters <= 0.0)
        {
            return 1.0;
        }

        double ratio = distanceMeters / maxDistanceMeters;
        if (!double.IsFinite(ratio))
        {
            return 1.0;
        }
        return Math.Clamp(ratio, 0.001, 1.0);
    }
}
