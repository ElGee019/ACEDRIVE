// Driving-slice balance (PORTPLAN item 14): how the speed challenge turns a
// route distance and the driven car into a delivery deadline. Shipped values
// live in Data/taxi_rules.json; this default is the test-shell fallback and
// the deterministic fixture for tests.
namespace AceDrive.Core;

/// <summary>
/// The speed-challenge deadline knobs. The average speed is the car's top
/// speed scaled down to a road pace, clamped so a slow classic is not given an
/// impossible target and a supercar cannot turn every ride into a sprint. The
/// slack absorbs traffic, corners and the address map's closed-lap estimate;
/// the buffer covers the stop at the door and a very short hop that would
/// otherwise be decided by one traffic light. All times in seconds; speeds in
/// km/h.
/// </summary>
public sealed record SpeedChallengeRules(
    double TopSpeedFraction,
    double MinAverageKmh,
    double MaxAverageKmh,
    double SlackFactor,
    int BufferSeconds)
{
    /// <summary>Fallback used when the data file is missing or invalid.</summary>
    public static readonly SpeedChallengeRules Default = new(
        TopSpeedFraction: 0.55,
        MinAverageKmh: 40.0,
        MaxAverageKmh: 130.0,
        SlackFactor: 1.25,
        BufferSeconds: 20);

    public bool IsValid =>
        double.IsFinite(TopSpeedFraction) && TopSpeedFraction > 0.0 && TopSpeedFraction <= 1.0
        && double.IsFinite(MinAverageKmh) && MinAverageKmh > 0.0
        && double.IsFinite(MaxAverageKmh) && MaxAverageKmh >= MinAverageKmh
        && double.IsFinite(SlackFactor) && SlackFactor > 0.0
        && BufferSeconds >= 0;
}
