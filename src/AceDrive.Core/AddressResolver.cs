// Maps the live normalised lap position onto the placeholder address map
// (PORTPLAN Step 1). Pure geometry: no telemetry types, no UI strings.
namespace AceDrive.Core;

/// <summary>
/// Resolves the street and house number at a normalised lap position. The
/// street fractions and the house spacing are data; the runtime track length
/// comes from EVO's static block, so the same map can follow a different
/// layout without editing the code.
/// </summary>
public static class AddressResolver
{
    /// <summary>
    /// Null when the position is outside 0..1, the map has no usable street,
    /// or the geometry is invalid. Never throws.
    /// </summary>
    public static AddressLocation? Resolve(
        double normalizedPosition, double trackLengthMeters, AddressMap map)
    {
        if (map.Streets is not { Count: > 0 }
            || !double.IsFinite(normalizedPosition)
            || normalizedPosition < 0.0
            || normalizedPosition > 1.0)
        {
            return null;
        }

        // WHY the fallback: the static block can be missing or still zero
        // right after a session load; the data file's length is the honest
        // stand-in until the game publishes it.
        double length = double.IsFinite(trackLengthMeters) && trackLengthMeters > 0.0
            ? trackLengthMeters
            : map.TrackLengthMeters;
        if (!double.IsFinite(length) || length <= 0.0
            || !double.IsFinite(map.HouseSpacingMeters) || map.HouseSpacingMeters <= 0.0)
        {
            return null;
        }

        double lapMeters = normalizedPosition * length;
        for (int i = 0; i < map.Streets.Count; i++)
        {
            AddressStreet street = map.Streets[i];
            if (street is null
                || !double.IsFinite(street.FromFraction)
                || !double.IsFinite(street.ToFraction)
                || street.ToFraction <= street.FromFraction)
            {
                continue;
            }

            double from = street.FromFraction * length;
            double to = street.ToFraction * length;
            // The last street owns the exact end of the lap; npos can read 1.0
            // on the line, which must not fall through to "no address".
            bool inside = lapMeters >= from
                && (lapMeters < to || (i == map.Streets.Count - 1 && lapMeters <= to));
            if (!inside)
            {
                continue;
            }

            int count = HouseNumberPlan.CountAt(street, length, map.HouseSpacingMeters);
            if (count <= 0)
            {
                return null;
            }

            int number = (int)Math.Floor((lapMeters - from) / map.HouseSpacingMeters) + 1;
            int house = Math.Clamp(number, 1, count);
            return new AddressLocation(street.Name, house, HouseNumberPlan.SideOf(house));
        }

        return null;
    }
}
