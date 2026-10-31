// Pure gameplay data. No WPF / Windows / telemetry / backend references.
namespace AceDrive.Core;

/// <summary>
/// One collision the telemetry witnessed: the speed the car carried into it
/// (the sample taken before the damage jump, so a hit that stops the car dead
/// still reports the speed it hit at) and the damage units EVO added.
/// Co-located with <see cref="AccidentDetector"/>, the only producer.
/// </summary>
public sealed record AccidentImpact(
    double ImpactSpeedKmh,
    double DamageDelta);
