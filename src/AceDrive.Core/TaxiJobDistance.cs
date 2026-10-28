// The taxi job's distance rule (PORTPLAN item 11): the driver's approach from
// where the job started to the pickup, plus the pickup-to-drop-off leg, both
// measured on the closed lap instead of the odometer. Pure geometry so the
// ViewModel only supplies the start position and the two doors.
namespace AceDrive.Core;

/// <summary>
/// A taxi job's chargeable distance: how far the car has to go from its
/// position when the job starts to the pickup, plus the ride itself. The
/// driven distance never enters the sum, so a detour cannot inflate the fare
/// and a shortcut cannot shrink it.
/// </summary>
public static class TaxiJobDistance
{
    /// <summary>
    /// The job's calculated distance in km. A missing start position (EVO not
    /// attached yet) or missing door answers 0 for that part instead of
    /// throwing; unresolved map addresses answer 0 for the whole job.
    /// </summary>
    public static double Kilometers(
        AddressMap map, double? startNormalizedPosition,
        AddressLocation? pickup, AddressLocation? dropoff, double trackLengthMeters)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (pickup is not { } from
            || dropoff is not { } to
            || !double.IsFinite(trackLengthMeters)
            || trackLengthMeters <= 0.0)
        {
            return 0.0;
        }

        double meters = 0.0;
        if (startNormalizedPosition is { } position
            && double.IsFinite(position)
            && position >= 0.0
            && position <= 1.0)
        {
            double pickupOffset = AddressBook.LapOffsetMeters(
                map, from, trackLengthMeters);
            if (double.IsFinite(pickupOffset))
            {
                meters += LapGeometry.DistanceMeters(
                    LapGeometry.OffsetMeters(position, trackLengthMeters),
                    pickupOffset, trackLengthMeters);
            }
        }

        double trip = AddressBook.DistanceMeters(map, from, to, trackLengthMeters);
        if (double.IsFinite(trip))
        {
            meters += trip;
        }

        return double.IsFinite(meters) ? meters / 1000.0 : 0.0;
    }
}
