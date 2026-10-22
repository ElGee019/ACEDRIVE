// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 12 and LATESTDRIVEBS2 ShiftStandingsService.
namespace AceDrive.Core;

/// <summary>
/// One other player's best single-shift earnings. The first-earned stamp is
/// the tie-break: of two identical earnings, whoever reached the value first
/// ranks higher (Unity's board rule).
/// </summary>
public readonly record struct ShiftBoardEntry(double Earnings, long FirstEarnedTicks);
