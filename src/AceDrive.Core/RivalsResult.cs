// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 60 (Unity
// RivalsService.RivalsUpdateResult).
namespace AceDrive.Core;

/// <summary>
/// One XP world-standings snapshot for the receipt: where the player ranks
/// in the world, how many players are on the board, and the movement since
/// the last observed rank. Data only — the sentences live in the resx.
/// </summary>
public sealed record RivalsResult(
    int CurrentRank,
    int TotalCompetitors,
    int RankChange,
    int PreviousRank,
    bool IsFirstTime);
