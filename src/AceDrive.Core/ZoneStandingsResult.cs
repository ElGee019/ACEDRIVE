// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md items 12/58 and LATESTDRIVEBS2 ZoneStandingsResult.
namespace AceDrive.Core;

/// <summary>One operating-zone world-standings snapshot, data only.</summary>
public sealed record ZoneStandingsResult(
    int CurrentRank,
    int TotalCompetitors,
    int RankChange,
    int PreviousRank,
    bool IsFirstTime);
