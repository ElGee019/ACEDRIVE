// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 49. One painted row of the per-car road-test
// board (Unity RoadTestScreen.PopulateStandings + AddSeparator).
namespace AceDrive.Core;

/// <summary>
/// One standing as the page paints it: the position on the full board, whose
/// run it was and the value (seconds or metres, lower is better). IsOwn marks
/// the player's row, which the page highlights; Unity also repeats that row
/// under a separator when it fell outside the visible top slice.
/// </summary>
public readonly record struct RoadTestStanding(
    int Position,
    string PlayerName,
    double Value,
    bool IsOwn);
