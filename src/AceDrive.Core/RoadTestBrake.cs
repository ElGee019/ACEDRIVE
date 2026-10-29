// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 49 and LATESTDRIVEBS2 RoadTestScreen's decel
// leg: the distance covered from 100 km/h to a halt is the run.
namespace AceDrive.Core;

/// <summary>
/// The brake leg's distance integration. The 10 Hz poll only has two speed
/// readings per tick, so the distance between them is the trapezoid under the
/// pair — the same arithmetic a future Lua port transliterates. A stalled tick
/// is dropped rather than integrated: a tab-out while the car reads 100 km/h
/// must not bank a kilometre the car never drove.
/// </summary>
public static class RoadTestBrake
{
    /// <summary>
    /// Adds one poll's distance, in metres. <paramref name="seconds"/> is the
    /// tick's dt; the same 0..1 s guard the taxi meter and the odometer use
    /// keeps a stalled or lagged frame out of the run.
    /// </summary>
    public static double AddDistance(
        double meters, double previousSpeedKmh, double speedKmh, double seconds)
    {
        if (!double.IsFinite(meters)
            || !double.IsFinite(previousSpeedKmh)
            || !double.IsFinite(speedKmh)
            || !double.IsFinite(seconds)
            || seconds is <= 0.0 or >= 1.0)
        {
            return meters;
        }

        double averageKmh = (Math.Abs(previousSpeedKmh) + Math.Abs(speedKmh)) * 0.5;
        double added = averageKmh / 3600.0 * seconds * 1000.0;
        double next = meters + added;
        return double.IsFinite(next) && next >= meters ? next : meters;
    }
}
