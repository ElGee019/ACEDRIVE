// The Cento Miglia reputation ticker's input side (PORTPLAN item 49): Unity
// ran the reputation multiplication on a 0.1 s coroutine, so the port banks
// the distance each poll covers and charges one multiplication per 0.01 km
// (CentoReputation.TickKilometres). Standing still, a paused game or a stalled
// poll adds nothing, which is what stops the race from building a club
// reputation for a car that never moves. Pure, like the fare meter it mirrors.
namespace AceDrive.Core;

/// <summary>
/// Accumulates the race's driven distance and reports the live club
/// reputation it is worth. One <see cref="Add"/> per telemetry poll; the same
/// 0..1 s dt guard and speed clamp as the odometer and the taxi fare meter keep
/// a stalled, torn or teleported read out of the race.
/// </summary>
public sealed class CentoReputationTicker
{
    // WHY a clamp: a one-frame telemetry glitch must not buy a kilometre of
    // reputation. This is a sanity bound, not balance, so it stays out of the
    // data files (the fleet's odometer feed clamps the same way).
    private const double MaxSpeedKmh = 500.0;

    private readonly double _startReputation;
    private readonly int _carClass;
    private readonly int _ownedCars;

    public CentoReputationTicker(double startReputation, int carClass, int ownedCars)
    {
        _startReputation = startReputation;
        _carClass = carClass;
        _ownedCars = ownedCars;
        Value = CentoReputation.AfterDistance(startReputation, carClass, ownedCars, 0.0);
    }

    /// <summary>Kilometres banked on the race leg.</summary>
    public double DistanceKm { get; private set; }

    /// <summary>The reputation the race has grown so far.</summary>
    public double Value { get; private set; }

    /// <summary>
    /// Adds one poll's distance. <paramref name="seconds"/> is the poll's dt;
    /// a poll outside 0..1 s (a stalled or lagged frame), a torn speed read or
    /// a car at a standstill banks nothing.
    /// </summary>
    public void Add(float speedKmh, double seconds)
    {
        if (!float.IsFinite(speedKmh)
            || !double.IsFinite(seconds)
            || seconds is <= 0.0 or >= 1.0)
        {
            return;
        }

        double km = Math.Min(Math.Abs(speedKmh), MaxSpeedKmh) / 3600.0 * seconds;
        if (!double.IsFinite(km) || km <= 0.0)
        {
            return;
        }

        DistanceKm += km;
        Value = CentoReputation.AfterDistance(
            _startReputation, _carClass, _ownedCars, DistanceKm);
    }
}
