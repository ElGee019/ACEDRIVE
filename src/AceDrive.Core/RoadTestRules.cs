// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 49 and LATESTDRIVEBS2 RoadTestScreen's accel
// leg (start counter, random reaction hold, then the 0-60 stopwatch).
namespace AceDrive.Core;

/// <summary>
/// The 0-60 leg's numbers: the target speed, the start counter and the random
/// hold it inserts before the clock starts so the pull-away is a reaction, not
/// a known beat.
/// </summary>
public static class RoadTestRules
{
    /// <summary>
    /// The speed the accel test times. WHY 60 km/h and not the player's unit:
    /// the game's own spec column (cars.json <c>Acceleration</c>) is seconds to
    /// 60 km/h, so the timed run has to be comparable with it — a miles player
    /// only reads the same target converted (37 mph), never a faster one.
    /// </summary>
    public const double TargetSpeedKmh = 60.0;

    /// <summary>
    /// The speed the brake leg is taken from: the player brings the car up to
    /// 100 km/h and then brakes to a halt, and the distance covered is the run.
    /// The game's brake column (cars.json <c>BrakeDistance</c>) is metres from
    /// 100, so the leg has to use the same start or the two are not comparable.
    /// </summary>
    public const double BrakeStartSpeedKmh = 100.0;

    /// <summary>
    /// Speed at or below which the car counts as halted. WHY 1 km/h: EVO
    /// reports a little jitter around a standstill, and the HUD's parked
    /// reading uses the same ceiling — a stricter zero would keep asking for a
    /// halt on a car that is already parked.
    /// </summary>
    public const double StandstillSpeedKmh = 1.0;

    /// <summary>
    /// The start counter: five, four, three, two, one, one real second each.
    /// WHY five and not Unity's 3-2-1: the port gives the player a longer,
    /// calmer count, and the counter restarts from this digit whenever a
    /// rolling car has to be halted before the run.
    /// </summary>
    public const int CountdownSteps = 5;

    /// <summary>Unity's Random.Range(0.3f, 3f) hold after the "1".</summary>
    public const double HoldMinSeconds = 0.3;

    /// <inheritdoc cref="HoldMinSeconds"/>
    public const double HoldMaxSeconds = 3.0;

    /// <summary>
    /// The target as the player reads it: 60 in kilometres, 37 in miles — the
    /// same 60 km/h rounded the way the speedometer rounds it, so the task text
    /// never claims a target the clock does not measure.
    /// </summary>
    public static int DisplayTarget(MileageUnit unit) => DisplaySpeed(TargetSpeedKmh, unit);

    /// <summary>
    /// Any speed as the player reads it: the same conversion and rounding as
    /// the speedometer, so the brake leg's 100 km/h and the accel leg's 60 are
    /// shown in the player's own unit and never as a speed the game does not
    /// measure.
    /// </summary>
    public static int DisplaySpeed(double speedKmh, MileageUnit unit) =>
        (int)Math.Round(
            Mileage.FromKilometres(speedKmh, unit), MidpointRounding.AwayFromZero);

    /// <summary>
    /// True once a live sample reaches the target. Abs because EVO reports a
    /// creep in either direction around a standstill.
    /// </summary>
    public static bool ReachedTarget(double speedKmh) =>
        double.IsFinite(speedKmh) && Math.Abs(speedKmh) >= TargetSpeedKmh;

    /// <summary>
    /// True once a live sample reaches the brake leg's 100 km/h start.
    /// </summary>
    public static bool ReachedBrakeStart(double speedKmh) =>
        double.IsFinite(speedKmh) && Math.Abs(speedKmh) >= BrakeStartSpeedKmh;

    /// <summary>
    /// True while the car is halted, within the jitter band a live reading
    /// carries around a standstill. A lost or torn reading does not count as
    /// halted, so the caller never promises a standing start it cannot see.
    /// </summary>
    public static bool IsStandstill(double speedKmh) =>
        double.IsFinite(speedKmh) && Math.Abs(speedKmh) <= StandstillSpeedKmh;

    /// <summary>
    /// True when the announced car is the week's pick, or when the game has not
    /// announced one yet. WHY unknown counts as a match: the log watcher catches
    /// the spawn a moment after a fresh session, and the HUD's wrong-car warning
    /// treats the same silence as "no mismatch" — refusing a test on a guess
    /// would lock the road test out of every cold start.
    /// </summary>
    public static bool DrivesThePick(CarIdentity? live, CarSpec pick)
    {
        ArgumentNullException.ThrowIfNull(pick);
        return live is null || live.Matches(pick.Key);
    }

    /// <summary>
    /// One reaction hold inside Unity's band. Clamped rather than trusted: the
    /// caller's RNG is a seeded instance, but a hand-written constant must not
    /// be able to turn the countdown into a two-minute wait.
    /// </summary>
    public static double ClampHold(double seconds) =>
        double.IsFinite(seconds)
            ? Math.Clamp(seconds, HoldMinSeconds, HoldMaxSeconds)
            : HoldMinSeconds;
}
