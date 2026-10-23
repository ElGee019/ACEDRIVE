// Driving-slice balance (PORTPLAN item 11): the door radius, the fare meter's
// speed clamp, the HUD distance step, a transient notice duration and the
// customer request's read-in delay. Shipped values are the test-shell
// fallback; Data/taxi_rules.json is the source of truth, loaded by
// JsonTaxiRulesStore at startup.
namespace AceDrive.Core;

/// <summary>
/// Job-guidance rules the poll, the HUD and the meter share. All distances in
/// metres; times in seconds.
/// </summary>
public sealed record TaxiRules(
    double ArrivalRadiusMeters,
    double MaxMeterSpeedKmh,
    double DisplayDistanceStepMeters,
    double NoticeSeconds)
{
    /// <summary>
    /// The speed challenge's deadline knobs. Kept as an init property so a
    /// data file written before the challenge existed still loads the four
    /// positional rules and gets the defaults here.
    /// </summary>
    public SpeedChallengeRules SpeedChallenge { get; init; } =
        SpeedChallengeRules.Default;

    /// <summary>
    /// How long the drop-off task text stays up before a rolled customer
    /// request replaces it, so the player reads where to drive first. Init
    /// property for the same reason as <see cref="SpeedChallenge"/>: a file
    /// written before the request page existed still loads.
    /// </summary>
    public double RequestDelaySeconds { get; init; } = 5.0;

    /// <summary>Fallback used when the data file is missing or invalid.</summary>
    public static readonly TaxiRules Default = new(
        ArrivalRadiusMeters: 25.0,
        MaxMeterSpeedKmh: 400.0,
        DisplayDistanceStepMeters: 10.0,
        NoticeSeconds: 3.0);
}
