// Closed-lap geometry for the job guidance (PORTPLAN item 11 driving slice).
// The track length comes from EVO's static block at runtime, so the same
// placeholder address map follows a different layout without moving any view
// code. Pure and guarded: invalid telemetry answers NaN/Infinity/false instead
// of throwing on the 10 Hz poll.
namespace AceDrive.Core;

/// <summary>
/// Lap-offset arithmetic: where a normalised position sits on the circuit and
/// how far it is from a door, taking the shorter way around the closed lap.
/// </summary>
public static class LapGeometry
{
    /// <summary>
    /// Metres from the start/finish line, or NaN when the position or the
    /// track length is unusable.
    /// </summary>
    public static double OffsetMeters(double normalizedPosition, double trackLengthMeters)
    {
        if (!double.IsFinite(normalizedPosition)
            || normalizedPosition < 0.0
            || normalizedPosition > 1.0
            || !double.IsFinite(trackLengthMeters)
            || trackLengthMeters <= 0.0)
        {
            return double.NaN;
        }

        return normalizedPosition * trackLengthMeters;
    }

    /// <summary>
    /// Closed-lap distance between two offsets: the shorter way around, so a
    /// door just behind the start line is next to a car just before it.
    /// Infinity when any input is unusable, which keeps the value out of a
    /// radius comparison.
    /// </summary>
    public static double DistanceMeters(
        double offsetA, double offsetB, double trackLengthMeters)
    {
        if (!double.IsFinite(offsetA)
            || !double.IsFinite(offsetB)
            || !double.IsFinite(trackLengthMeters)
            || trackLengthMeters <= 0.0)
        {
            return double.PositiveInfinity;
        }

        double direct = Math.Abs(offsetA - offsetB) % trackLengthMeters;
        return Math.Min(direct, trackLengthMeters - direct);
    }

    /// <summary>
    /// True when the live position is inside <paramref name="radiusMeters"/>
    /// of the target offset. A negative or non-finite radius never matches.
    /// </summary>
    public static bool IsArrived(
        double normalizedPosition, double targetOffsetMeters,
        double trackLengthMeters, double radiusMeters)
    {
        if (!double.IsFinite(radiusMeters) || radiusMeters < 0.0)
        {
            return false;
        }

        double offset = OffsetMeters(normalizedPosition, trackLengthMeters);
        return double.IsFinite(offset)
            && DistanceMeters(offset, targetOffsetMeters, trackLengthMeters) <= radiusMeters;
    }
}
