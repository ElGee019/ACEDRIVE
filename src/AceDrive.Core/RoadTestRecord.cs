// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The road test's persisted half (PORTPLAN item 49, Unity PlayerData's
// lastRoadTest* / highestRoadTest* / naturalCarJournalistReputation / lastPhoto*
// fields). The flow owns the timing; this record owns the readings a restart
// must reopen.
namespace AceDrive.Core;

/// <summary>
/// What one restart must reopen for the road test (PORTPLAN item 49): the
/// natural car-journalist reputation the formula scales, the last shoot's door
/// and lap offset (the variety baseline) and the previous issue's readings with
/// the personal bests beside them. Unity keeps these on PlayerData; the port
/// keeps them with the pick it already persists, because both belong to the
/// same weekly event.
/// </summary>
public sealed record RoadTestRecord(
    int NaturalReputation = 0,
    bool HasPhoto = false,
    string? PhotoStreet = null,
    int PhotoHouseNumber = 0,
    double PhotoOffsetMeters = 0.0,
    int LastPeopleReached = 0,
    double LastVariety = 0.0,
    double LastJournalistReputation = 0.0,
    double LastMagazinePrice = 0.0,
    double LastEarnings = 0.0,
    int HighestPeopleReached = 0,
    double HighestJournalistReputation = 0.0,
    double HighestMagazinePrice = 0.0,
    double HighestEarnings = 0.0)
{
    /// <summary>A career that never finished a road test.</summary>
    public static readonly RoadTestRecord Empty = new();

    /// <summary>True when a previous shoot exists to measure the variety against.</summary>
    public bool HasPreviousPhoto =>
        HasPhoto
        && !string.IsNullOrEmpty(PhotoStreet)
        && PhotoHouseNumber > 0
        && double.IsFinite(PhotoOffsetMeters)
        && PhotoOffsetMeters >= 0.0;
}
