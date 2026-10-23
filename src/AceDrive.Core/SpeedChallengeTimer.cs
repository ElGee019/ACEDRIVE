// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 14 (speed-challenge gamble).
namespace AceDrive.Core;

/// <summary>
/// Turns a route distance and the driven car into a delivery deadline. Cheap
/// by design: the address map already knows the closed-lap distance between
/// the two doors, so no routing service is needed. The result is rounded up
/// to five seconds so the HUD never shows a value that immediately ticks down.
/// </summary>
public static class SpeedChallengeTimer
{
    private const double SecondsPerHour = 3600.0;
    private const double RoundingSeconds = 5.0;

    /// <summary>
    /// How long the player gets for <paramref name="routeMeters"/>. Null when
    /// the route or the rules are unusable, so the caller can leave the
    /// challenge untimed instead of inventing an impossible deadline.
    /// </summary>
    public static TimeSpan? Estimate(
        double routeMeters, int topSpeedKmh, SpeedChallengeRules rules)
    {
        if (rules is null || !rules.IsValid
            || !double.IsFinite(routeMeters) || routeMeters <= 0.0)
        {
            return null;
        }

        double averageKmh = topSpeedKmh > 0
            ? Math.Clamp(
                topSpeedKmh * rules.TopSpeedFraction,
                rules.MinAverageKmh,
                rules.MaxAverageKmh)
            : rules.MinAverageKmh;

        double seconds = routeMeters / 1000.0 / averageKmh
            * SecondsPerHour * rules.SlackFactor + rules.BufferSeconds;
        if (!double.IsFinite(seconds) || seconds <= 0.0)
        {
            return null;
        }

        double rounded = Math.Ceiling(seconds / RoundingSeconds) * RoundingSeconds;
        return rounded > TimeSpan.MaxValue.TotalSeconds
            ? null
            : TimeSpan.FromSeconds(rounded);
    }
}
