// The taxi fare meter's distance integration (PORTPLAN item 11 driving
// slice): only the passenger-aboard leg counts. Pure so the Core tests and a
// future Lua port share the arithmetic.
namespace AceDrive.Core;

/// <summary>
/// Accumulates the fare distance while the passenger is aboard. A tick is
/// ignored when the poll stalled (dt outside 0..1 s), the speed read is not
/// finite, or the car is stationary; the speed clamp keeps a one-frame
/// telemetry glitch from inflating the ride.
/// </summary>
public sealed class TaxiDriveMeter
{
    private readonly double _maxSpeedKmh;

    public TaxiDriveMeter(double maxSpeedKmh)
    {
        _maxSpeedKmh = double.IsFinite(maxSpeedKmh) && maxSpeedKmh > 0.0
            ? maxSpeedKmh
            : TaxiRules.Default.MaxMeterSpeedKmh;
    }

    /// <summary>Distance banked on the passenger leg, in kilometres.</summary>
    public double DistanceKm { get; private set; }

    public void Reset() => DistanceKm = 0.0;

    /// <summary>
    /// Adds one poll's distance. <paramref name="seconds"/> is the poll's dt;
    /// the same 0..1 s guard as the odometer keeps a stalled or lagged frame
    /// out of the fare (PORTPLAN items 11/31).
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
