// The taxi job's distance rule (PORTPLAN item 11): the driver's approach from
// where the job started to the pickup, plus the pickup-to-drop-off leg, both
// measured on the closed lap instead of the odometer. Pure geometry so the
// ViewModel only supplies the start position and the two doors.
namespace AceDrive.Core;

/// <summary>
/// A taxi job's chargeable distance: how far the car has to go from its
/// position when the job starts to the pickup, plus the ride itself. The
/// driven distance never enters the sum, so a detour cannot inflate the fare
/// and a shortcut cannot shrink it. The route math is shared with the delivery
/// run through <see cref="LapRouteDistance"/> so the two jobs cannot drift.
/// </summary>
public static class TaxiJobDistance
{
    /// <summary>
    /// The job's calculated distance in km. A missing start position (EVO not
    /// attached yet) drops the approach only; unresolved map addresses answer 0
    /// for the whole job.
    /// </summary>
    public static double Kilometers(
        AddressMap map, double? startNormalizedPosition,
        AddressLocation? pickup, AddressLocation? dropoff, double trackLengthMeters)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (pickup is not { } from || dropoff is not { } to)
        {
            return 0.0;
        }

        return LapRouteDistance.Kilometers(
            map, startNormalizedPosition, [from, to], trackLengthMeters);
    }
}
