// Pure gameplay data. No WPF / Windows / telemetry / backend references.
namespace AceDrive.Core;

/// <summary>
/// The bill one collision leaves behind: the impact speed, this crash's share
/// of the car's value, the CR that share actually added to the outstanding
/// bill (0 once the car is written off), and the car's total outstanding body
/// damage after the crash (0..1, capped at a write-off). The crashes of one
/// ride accumulate, but <see cref="RepairCost"/> sits on the share that was
/// banked, so the bills never add up to more than the car's own price.
/// </summary>
public sealed record AccidentDamage(
    double ImpactSpeedKmh,
    double RepairFraction,
    double RepairCost,
    double TotalDamageFraction);
