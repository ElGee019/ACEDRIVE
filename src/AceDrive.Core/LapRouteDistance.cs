// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The calculated-route rule the priced jobs share (PORTPLAN items 11 and 19):
// the approach and the planned legs are measured on the closed lap, never on
// the odometer, so a detour cannot inflate the pay and a shortcut cannot shrink
// it.
namespace AceDrive.Core;

/// <summary>
/// A job's chargeable route on the address map: the optional approach from the
/// car's position to the first door, then every door-to-door leg in the order
/// the job will drive them. The driven distance never enters the sum.
/// </summary>
public static class LapRouteDistance
{
    /// <summary>
    /// The route's calculated length in km. A missing start position drops the
    /// approach only (the legs still count); a door the map cannot resolve is
    /// skipped, so one broken address cannot take the whole route to zero; an
    /// unusable track length answers 0, exactly like the taxi rule's guard.
    /// </summary>
    public static double Kilometers(
        AddressMap map, double? startNormalizedPosition,
        IReadOnlyList<AddressLocation?> doors, double trackLengthMeters)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(doors);
        if (!double.IsFinite(trackLengthMeters) || trackLengthMeters <= 0.0)
        {
            return 0.0;
        }

        double meters = 0.0;
        AddressLocation? previous = null;
        foreach (AddressLocation? door in doors)
        {
            if (door is not { } next)
            {
                continue;
            }

            if (previous is { } from)
            {
                double leg = AddressBook.DistanceMeters(map, from, next, trackLengthMeters);
                if (double.IsFinite(leg))
                {
                    meters += leg;
                }
            }
            else if (startNormalizedPosition is { } position
                && double.IsFinite(position)
                && position >= 0.0
                && position <= 1.0)
            {
                double approach = LapGeometry.DistanceMeters(
                    LapGeometry.OffsetMeters(position, trackLengthMeters),
                    AddressBook.LapOffsetMeters(map, next, trackLengthMeters),
                    trackLengthMeters);
                if (double.IsFinite(approach))
                {
                    meters += approach;
                }
            }

            previous = next;
        }

        return double.IsFinite(meters) ? meters / 1000.0 : 0.0;
    }
}
