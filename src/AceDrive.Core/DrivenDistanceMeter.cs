// The metered-leg distance integration (PORTPLAN items 11/31): only the leg
// the player is charged for counts — a delivery run from its first store, a
// Touristenfahrten corner. The taxi job deliberately does not use it: its
// fare is the calculated lap distance (TaxiJobDistance). Pure so the Core
// tests and a future Lua port share the arithmetic.
namespace AceDrive.Core;

/// <summary>
/// Accumulates the charged leg's distance. A tick is ignored when the poll
/// stalled (dt outside 0..1 s), the speed read is not finite, or the car is
/// stationary; the speed clamp keeps a one-frame telemetry glitch from
/// inflating the leg.
/// </summary>
public sealed class DrivenDistanceMeter
{
    private readonly double _maxSpeedKmh;

    public DrivenDistanceMeter(double maxSpeedKmh)
    {
        _maxSpeedKmh = double.IsFinite(maxSpeedKmh) && maxSpeedKmh > 0.0
            ? maxSpeedKmh
            : TaxiRules.Default.MaxMeterSpeedKmh;
    }

    /// <summary>Distance banked on the charged leg, in kilometres.</summary>
    public double DistanceKm { get; private set; }

    public void Reset() => DistanceKm = 0.0;

    /// <summary>
    /// Adds one poll's distance. <paramref name="seconds"/> is the poll's dt;
    /// the same 0..1 s guard as the odometer keeps a stalled or lagged frame
    /// out of the leg (PORTPLAN items 11/31).
    /// </summary>
    public void Add(float speedKmh, double seconds)
    {
        if (!float.IsFinite(speedKmh)
            || !double.IsFinite(seconds)
            || seconds is <= 0.0 or >= 1.0)
        {
            return;
        }

        double speed = Math.Min(Math.Abs(speedKmh), _maxSpeedKmh);
        double km = speed / 3600.0 * seconds;
        if (double.IsFinite(km) && km > 0.0)
        {
            DistanceKm += km;
        }
    }
}
