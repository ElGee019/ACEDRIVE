// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 12 and LATESTDRIVEBS2 ShiftStandingsResult.
namespace AceDrive.Core;

/// <summary>One best-shift world-standings snapshot, data only.</summary>
public sealed record ShiftStandingsResult(
    int CurrentRank,
    int TotalCompetitors,
    int RankChange,
    int PreviousRank,
    bool IsFirstTime);
