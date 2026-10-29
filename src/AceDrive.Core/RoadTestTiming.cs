// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 49. The standing readout Unity's per-car
// board shows after an accel or brake test.
namespace AceDrive.Core;

/// <summary>
/// The 0-60 reading. The one 10 Hz poll only sees the speed before and after
/// the crossing, so the crossing itself is interpolated between the two
/// samples: without it every board time would be quantised to a tenth of a
/// second — which would make the three-decimal readout a lie and turn ties
/// into a coin toss between two honest runs. Pure so the rule is tested
/// without a telemetry pipe.
/// </summary>
public static class RoadTestTiming
{
    /// <summary>
    /// The elapsed seconds at which the car crossed <paramref name="targetKmh"/>.
    /// <paramref name="previousSeconds"/> is the previous poll's elapsed time,
    /// <paramref name="previousSpeedKmh"/> its speed and <paramref name="nowSeconds"/>
    /// the current poll's elapsed time. A sample that did not actually cross
    /// (the previous one was already at the target, or the speed fell) returns
    /// the current reading rather than inventing a time between them.
    /// </summary>
    public static double CrossingSeconds(
        double previousSeconds, double previousSpeedKmh, double nowSeconds,
        double speedKmh, double targetKmh = RoadTestRules.TargetSpeedKmh)
    {
        double previous = double.IsFinite(previousSeconds) && previousSeconds > 0.0
            ? previousSeconds
            : 0.0;
        double now = double.IsFinite(nowSeconds) && nowSeconds >= previous
            ? nowSeconds
            : previous;
        if (!double.IsFinite(previousSpeedKmh) || !double.IsFinite(speedKmh)
            || !double.IsFinite(targetKmh)
            || previousSpeedKmh >= targetKmh
            || speedKmh <= previousSpeedKmh)
        {
            return now;
        }

        double fraction = (targetKmh - previousSpeedKmh) / (speedKmh - previousSpeedKmh);
        double crossing = previous + Math.Clamp(fraction, 0.0, 1.0) * (now - previous);
        // The pull-away can begin before the green light (the player launched
        // during the counter); the clock can only measure from the green light.
        return crossing > 0.0 ? crossing : 0.0;
    }
}
