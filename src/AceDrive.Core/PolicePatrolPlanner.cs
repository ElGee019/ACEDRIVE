// Speed-challenge patrol planner (PORTPLAN item 11, Phase C). Seeded and pure
// so a replay draws the same patrols; the host plans once per accepted job.
namespace AceDrive.Core;

/// <summary>
/// Draws patrol doors from the map's real houses, keeping clear of the pickup
/// and drop-off so a challenge cannot fine the player while parked at either
/// stop. The runtime track length (EVO's static block) positions every patrol
/// on the same lap offsets the HUD and the job guidance use.
/// </summary>
public sealed class PolicePatrolPlanner(
    AddressMap map, PoliceRules rules, int seed)
{
    private readonly Random _rng = new(seed);

    /// <summary>The shape the host reads for the warning radius and count.</summary>
    public PoliceRules Rules => rules;

    /// <summary>
    /// Plans up to <paramref name="count"/> unique patrols. An invalid track
    /// length, a map without houses or a non-positive count answers an empty
    /// list, so the host simply runs a challenge without patrols instead of
    /// throwing on the poll path.
    /// </summary>
    public IReadOnlyList<PolicePatrol> Plan(
        AddressLocation? pickup, AddressLocation? dropoff,
        double trackLengthMeters, int count)
    {
        if (count <= 0
            || !double.IsFinite(trackLengthMeters)
            || trackLengthMeters <= 0.0)
        {
            return [];
        }

        double pickupOffset = OffsetOrNaN(pickup, trackLengthMeters);
        double dropoffOffset = OffsetOrNaN(dropoff, trackLengthMeters);

        List<(AddressLocation Address, double Offset)> candidates = [];
        foreach (AddressStreet street in map.Streets)
        {
            int houses = HouseNumberPlan.CountAt(
                street, trackLengthMeters, map.HouseSpacingMeters);
            for (int number = 1; number <= houses; number++)
            {
                var address = new AddressLocation(
                    street.Name, number, HouseNumberPlan.SideOf(number));
                double offset = AddressBook.LapOffsetMeters(map, address, trackLengthMeters);
                if (!double.IsFinite(offset)
                    || IsExcluded(offset, pickupOffset, trackLengthMeters)
                    || IsExcluded(offset, dropoffOffset, trackLengthMeters))
                {
                    continue;
                }
                candidates.Add((address, offset));
            }
        }

        int take = Math.Min(count, candidates.Count);
        PolicePatrol[] patrols = new PolicePatrol[take];
        for (int i = 0; i < take; i++)
        {
            // Partial Fisher-Yates: every house can appear at most once and
            // the draw stays O(count) instead of retrying duplicates.
            int pick = _rng.Next(i, candidates.Count);
            (candidates[i], candidates[pick]) = (candidates[pick], candidates[i]);
            (AddressLocation address, double offset) = candidates[i];
            patrols[i] = new PolicePatrol(
                i + 1, address, offset,
                rules.PatrolRadiusMeters, rules.SpeedLimitKmh);
        }
        return patrols;
    }

    private double OffsetOrNaN(AddressLocation? address, double trackLengthMeters) =>
        address is { } door
            ? AddressBook.LapOffsetMeters(map, door, trackLengthMeters)
            : double.NaN;

    // A patrol on a stop itself would fine a parked challenge, so the draw
    // skips the whole keep-out circle, not just the exact house.
    private bool IsExcluded(double offset, double stopOffset, double trackLengthMeters) =>
        double.IsFinite(stopOffset)
        && LapGeometry.DistanceMeters(offset, stopOffset, trackLengthMeters)
            <= rules.ExcludedStopRadiusMeters;
}
