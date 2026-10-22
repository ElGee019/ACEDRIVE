// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 12 and LATESTDRIVEBS2 ShiftStatsArgs.
namespace AceDrive.Core;

/// <summary>
/// One finished taxi shift as the shift-ended page reports it: the jobs
/// completed, what they paid and how long the driver was on the road.
/// Data only — the sentences live in the resx.
/// </summary>
public sealed record ShiftSummary(int Jobs, double Earnings, int DurationMinutes);
