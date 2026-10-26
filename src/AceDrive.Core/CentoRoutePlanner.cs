// Cento Miglia route selection (PORTPLAN item 49): the longest route the
// player's operating zone allows. Port of Unity MilleMigliaService.
// CalculateMilleMigliaRouteAsync, which ranks the discovered street pairs by
// distance, takes the furthest house pair and asks the router for that route.
// The placeholder map has no coordinates and no routing service, so the
// closed-lap distance between two doors IS the drive: the planner answers the
// door pair the lap separates furthest. Once the freeroam map lands, the same
// seam asks the router instead and this file keeps its "furthest door pair"
// rule as the candidate selection.
namespace AceDrive.Core;

/// <summary>
/// Picks the two doors of an event's route from the streets the player's
/// operating zone covers. Pure and guarded: an unusable map answers null
/// instead of throwing on the page's open.
/// </summary>
public static class CentoRoutePlanner
{
    /// <summary>
    /// The door pair furthest apart on the map's closed lap, among the given
    /// streets. Answers null when the streets hold fewer than two usable doors
    /// (a hand-edited map) so the caller can refuse to open the event instead
    /// of building a zero-length race. Equal distances keep the first pair in
    /// street-then-house order, so a fixed map always answers the same route.
    /// </summary>
    public static CentoRoutePlan? LongestRoute(
        AddressMap map, IReadOnlyList<AddressStreet> streets)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(streets);
        if (map.Streets.Count == 0
            || !double.IsFinite(map.TrackLengthMeters)
            || map.TrackLengthMeters <= 0.0
            || !double.IsFinite(map.HouseSpacingMeters)
            || map.HouseSpacingMeters <= 0.0)
        {
            return null;
        }

        List<AddressLocation> doors = Doors(map, streets);
        if (doors.Count < 2)
        {
            return null;
        }

        double bestMeters = -1.0;
        AddressLocation start = default;
        AddressLocation finish = default;
        for (int i = 0; i < doors.Count; i++)
        {
            double from = AddressBook.LapOffsetMeters(map, doors[i]);
            if (!double.IsFinite(from))
            {
                continue;
            }

            for (int j = i + 1; j < doors.Count; j++)
            {
                double to = AddressBook.LapOffsetMeters(map, doors[j]);
                if (!double.IsFinite(to))
                {
                    continue;
                }

                double meters = LapGeometry.DistanceMeters(
                    from, to, map.TrackLengthMeters);
                if (double.IsFinite(meters) && meters > bestMeters)
                {
                    bestMeters = meters;
                    start = doors[i];
                    finish = doors[j];
                }
            }
        }

        return bestMeters < 0.0
            ? null
            : new CentoRoutePlan(start, finish, bestMeters / 1000.0);
    }

    // Every house number on the given streets, in the streets' own order: the
    // pair search walks this list, so its order is the tie-break the caller
    // can rely on.
    private static List<AddressLocation> Doors(
        AddressMap map, IReadOnlyList<AddressStreet> streets)
    {
        var doors = new List<AddressLocation>();
        foreach (AddressStreet street in streets)
        {
            int houses = HouseNumberPlan.Count(street, map);
            for (int number = 1; number <= houses; number++)
            {
                doors.Add(new AddressLocation(
                    street.Name, number, HouseNumberPlan.SideOf(number)));
            }
        }
        return doors;
    }
}
