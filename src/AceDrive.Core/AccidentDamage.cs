// Pure gameplay data. No WPF / Windows / telemetry / backend references.
namespace AceDrive.Core;

/// <summary>
/// The bill one collision leaves behind: the impact speed, this crash's share
/// of the car's value, the CR that share is worth, and the car's total
/// outstanding body damage after the crash (0..1, capped at a write-off).
/// </summary>
public sealed record AccidentDamage(
    double ImpactSpeedKmh,
    double RepairFraction,
    double RepairCost,
    double TotalDamageFraction);
