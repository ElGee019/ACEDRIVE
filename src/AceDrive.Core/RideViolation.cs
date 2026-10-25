// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity's passive customer asks).
namespace AceDrive.Core;

/// <summary>
/// Which live rule an accepted passive ask broke. The enum is data for the
/// host: it picks the HUD notice and the log line, the monitor only decides
/// the kind.
/// </summary>
public enum RideViolation
{
    None,
    Brake,
    Throttle,
    Steering,
    RevsTooLow,
    RevsTooHigh,
    Speed,
}
