// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The road test's receipt math (PORTPLAN item 49, Unity
// RoadTestScreen.PopulateReceipt over RoadTestAudienceCalculator).
namespace AceDrive.Core;

/// <summary>
/// Prices one finished road test (PORTPLAN item 49, Unity PopulateReceipt): the
/// crowd the issue reached, the variety of the new location against the last
/// shoot's, the car journalist's reputation, the magazine price and the
/// earnings, folded with the previous issue and the personal bests into a
/// <see cref="RoadTestReceipt"/>.
///
/// Deterministic and free of I/O, so the page charges nothing a second time.
/// </summary>
public static class RoadTestReceiptBuilder
{
    /// <summary>
    /// Builds the receipt. <paramref name="previous"/> is the record the last
    /// issue left behind: it supplies the variety baseline, the natural
    /// reputation (already raised for this test, Unity's MarkCurrentAsTested
    /// order), the "previously …" readings and the bests to beat.
    /// <paramref name="rules"/> is the issue's balance (forfeit, divisor,
    /// commission), loaded from Data/road_test.json.
    /// </summary>
    public static RoadTestReceipt Build(
        RoadTestShot shot, RoadTestAudienceCounts audience, RoadTestRecord previous,
        RoadTestAudienceRules rules)
    {
        ArgumentNullException.ThrowIfNull(shot);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(rules);

        int citizens = Math.Max(0, audience.Citizens);
        int friends = Math.Max(0, audience.Friends);
        int acquaintances = Math.Max(0, audience.Acquaintances);
        int peopleReached =
            RoadTestAudience.PeopleReached(citizens, friends, acquaintances);

        // The zone's widest span: the farther point on a closed lap is half the
        // loop away, so half the lap is the maximum separation two shoots can
        // have (Unity's "max distance in zone" between discovered houses).
        double maxDistance = double.IsFinite(shot.TrackLengthMeters)
            ? Math.Max(0.0, shot.TrackLengthMeters / 2.0)
            : 0.0;
        double distance = previous.HasPreviousPhoto
            ? LapGeometry.DistanceMeters(
                previous.PhotoOffsetMeters, shot.OffsetMeters, shot.TrackLengthMeters)
            : 0.0;
        double variety = RoadTestAudience.Variety(
            previous.HasPreviousPhoto, distance, maxDistance);

        // Both legs publish no placement, so their outcomes stay Unity's neutral
        // 0.5 and the result reputation lands on its neutral 50.
        int resultReputation = RoadTestAudience.ResultReputation(
            RoadTestAudience.NeutralOutcome, RoadTestAudience.NeutralOutcome);
        int naturalReputation = Math.Max(0, previous.NaturalReputation);
        double journalistReputation = RoadTestAudience.CarJournalistReputation(
            naturalReputation, resultReputation, variety);
        double magazinePrice =
            RoadTestAudience.MagazinePrice(journalistReputation, rules);
        double commission =
            RoadTestAudience.Commission(peopleReached, magazinePrice, rules);
        double earnings =
            RoadTestAudience.Earnings(peopleReached, magazinePrice, rules);

        return new RoadTestReceipt(
            shot.CarName,
            shot.HasAddress ? shot.Street : string.Empty,
            shot.HasAddress ? shot.HouseNumber : 0,
            shot.HasAddress && double.IsFinite(shot.OffsetMeters) ? Math.Max(0.0, shot.OffsetMeters) : 0.0,
            citizens,
            friends,
            acquaintances,
            peopleReached,
            Math.Max(0, previous.LastPeopleReached),
            variety,
            previous.LastVariety,
            resultReputation,
            naturalReputation,
            journalistReputation,
            previous.LastJournalistReputation,
            magazinePrice,
            previous.LastMagazinePrice,
            rules.BaseForfeit,
            rules.CommissionPercent,
            commission,
            earnings,
            previous.LastEarnings,
            Math.Max(peopleReached, previous.HighestPeopleReached),
            Math.Max(journalistReputation, previous.HighestJournalistReputation),
            Math.Max(magazinePrice, previous.HighestMagazinePrice),
            Math.Max(earnings, previous.HighestEarnings),
            FirstPhoto: !previous.HasPreviousPhoto,
            DistanceKm: distance / 1000.0,
            MaxDistanceKm: maxDistance / 1000.0);
    }
}
