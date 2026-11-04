// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate this
// file instead of the UI. Spec source: PORTPLAN.md item 22, port of Unity
// CarShareService.GenerateDailyReservations / CreateReservation.
namespace AceDrive.Core;

/// <summary>
/// Draws one game day of car-sharing requests: how many rides the town's
/// population produces, then for each ride a resident who asked, two doors on
/// the operating zone's map, the ride length at the shared car's assumed speed
/// and a leave time that never overlaps the previous ride. This is Unity's
/// morning batch ported to the assign page: the same resident table and the
/// same zone-scoped address pool the taxi proposal draws from.
///
/// The plan is a pure function of the day, the town's facts and the seeded
/// stream, so a rebuilt page replays the identical batch. It is deliberately
/// re-derived on every page open instead of cached: the loop is a handful of
/// draws, and a batch frozen at dawn would ignore residents the player met
/// since — the day seed already keeps one unchanged town's batch stable.
/// </summary>
public sealed class CarShareRequestPlanner
{
    private readonly OperatingZone _zone;
    private readonly ResidentBook _residents;
    private readonly Func<string, int> _friendshipLevel;

    public CarShareRequestPlanner(
        OperatingZone zone, ResidentBook residents, Func<string, int> friendshipLevel)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(residents);
        ArgumentNullException.ThrowIfNull(friendshipLevel);

        _zone = zone;
        _residents = residents;
        _friendshipLevel = friendshipLevel;
    }

    /// <summary>
    /// Today's batch, in leave-time order. Empty while the town has no known
    /// resident to ask or the zone cannot supply two doors; a population below
    /// 1,000 can also roll zero rides, which is Unity's own quiet day.
    /// <paramref name="carClass"/> and <paramref name="currentXp"/> price each
    /// ride the way completion will: the car being driven supplies the class
    /// term and the driver's XP supplies the diminishing multiplier.
    /// </summary>
    public IReadOnlyList<CarShareRequest> ForDay(
        int gameDay, int totalCitizens, int carClass, double currentXp)
    {
        IReadOnlyList<Resident> citizens = _residents.Residents;
        if (citizens.Count == 0)
        {
            return [];
        }

        var rng = new Random(CarShareRules.SeedFor(gameDay));
        int count = CarShareRules.RidesFor(rng, totalCitizens);
        var requests = new List<CarShareRequest>(count);
        int previousEndMinute = -1;
        for (int i = 0; i < count; i++)
        {
            // A zone that cannot offer two different doors ends the batch: a
            // ride to the door the customer already stands at is not a ride.
            if (!TaxiAddressPlan.TryDraw(
                    _zone.ZoneMap, rng, home: null, radiusKm: 0,
                    out AddressLocation pickup, out AddressLocation dropoff))
            {
                break;
            }

            double distanceKm = AddressBook.DistanceMeters(_zone.ZoneMap, pickup, dropoff) / 1000.0;
            if (!double.IsFinite(distanceKm) || distanceKm <= 0.0)
            {
                break;
            }

            int driveMinutes = CarShareRules.RideMinutes(distanceKm);
            int leaveMinute = NextLeaveMinute(rng, previousEndMinute, driveMinutes, out int endMinute);
            if (leaveMinute < 0)
            {
                break;
            }

            previousEndMinute = endMinute;

            // Unity draws the customer from the known residents, not from the
            // pickup door: a resident asks for a ride between two town
            // addresses rather than always being collected at home.
            Resident customer = citizens[rng.Next(citizens.Count)];
            double xp = XpEarning.Diminish(
                CarShareRules.Xp(
                    distanceKm, carClass, _friendshipLevel(customer.Name.BaseName)),
                currentXp);
            requests.Add(new CarShareRequest(
                customer.Name.BaseName, pickup, dropoff, distanceKm, leaveMinute, xp));
        }

        return requests;
    }

    /// <summary>
    /// The next ride's leave minute, chained after the previous ride's arrival
    /// so the shared car is never double-booked. A day with no room left before
    /// 22:00 answers -1: the caller stops the batch. Unity's clamping order is
    /// kept: a late random start slides the whole ride back inside the window.
    /// </summary>
    private static int NextLeaveMinute(
        Random rng, int previousEndMinute, int driveMinutes, out int endMinute)
    {
        int start = rng.Next(CarShareRules.EarliestLeaveMinute, CarShareRules.LatestEndMinute);
        if (previousEndMinute >= 0 && start < previousEndMinute)
        {
            start = previousEndMinute;
        }

        endMinute = start + driveMinutes;
        if (endMinute <= CarShareRules.LatestEndMinute)
        {
            return start;
        }

        endMinute = CarShareRules.LatestEndMinute;
        start = endMinute - driveMinutes;
        int earliestStart = previousEndMinute >= 0
            ? previousEndMinute
            : CarShareRules.EarliestLeaveMinute;
        return start < earliestStart ? -1 : start;
    }
}
