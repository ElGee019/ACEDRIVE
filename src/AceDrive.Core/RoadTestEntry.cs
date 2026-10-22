// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 49 and LATESTDRIVEBS2 RoadTestStandingsService.
namespace AceDrive.Core;

/// <summary>
/// One road-test board row. Both metrics are lower-is-better (0-60 seconds,
/// brake metres); the timestamp breaks ties, earlier first.
/// </summary>
public readonly record struct RoadTestEntry(
    string Uid,
    string PlayerName,
    string CarKey,
    double Value,
    string LastUpdatedAt);
