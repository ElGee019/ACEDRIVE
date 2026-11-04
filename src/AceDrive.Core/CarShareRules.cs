// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate this
// file instead of the UI. Spec source: PORTPLAN.md item 22, port of Unity
// CarShareService.ReservationCountFor / CalculateReservationXp.
namespace AceDrive.Core;

/// <summary>
/// The balance numbers behind a day of car sharing: how many rides the town's
/// population produces, how long a ride takes at the shared car's assumed
/// speed, the daily window the rides fit in, and the XP one completed request
/// pays. All values are Unity's CarShareService constants; the planner supplies
/// the seeded stream so a rebuilt page replays the same day.
/// </summary>
public static class CarShareRules
{
    /// <summary>Unity's assumed average speed: a 10 km run is a 20 minute ride.</summary>
    public const double AverageDriveSpeedKmH = 30.0;

    /// <summary>A booking never takes the shared car out for less than this.</summary>
    public const int MinRideMinutes = 10;

    /// <summary>First leave time of the day (06:00).</summary>
    public const int EarliestLeaveMinute = 6 * 60;

    /// <summary>Latest minute a ride may still be on the road (22:00).</summary>
    public const int LatestEndMinute = 22 * 60;

    /// <summary>Unity's XP divisor: distance × class × friendship over this.</summary>
    private const double XpDivisor = 500.0;

    /// <summary>A customer the driver has never met counts as half a friend.</summary>
    private const double StrangerFriendshipFactor = 0.5;

    /// <summary>Unity's citizen band width: one step per thousand inhabitants.</summary>
    private const int CitizensPerBand = 1000;

    /// <summary>
    /// How many rides one shared car gets today. Unity's curve: below 1,000
    /// citizens 0-2 rides, then the floor rises by one per band while the
    /// ceiling grows by 1, 2, 3, … (1,000-1,999 → 1-3, 2,000-2,999 → 2-5,
    /// 3,000-3,999 → 3-8). The caller's seeded stream keeps the day stable.
    /// </summary>
    public static int RidesFor(Random rng, int totalCitizens)
    {
        ArgumentNullException.ThrowIfNull(rng);

        int band = Math.Max(0, totalCitizens) / CitizensPerBand;
        int min = band;
        int max = 2 + (band * (band + 1) / 2);
        return rng.Next(min, max + 1);
    }

    /// <summary>
    /// How long one ride keeps the shared car: the distance at the assumed
    /// speed, never below <see cref="MinRideMinutes"/>. The planner books the
    /// leave time from it and the page reads the arrival back from it, so both
    /// ends of a request agree without a second formula.
    /// </summary>
    public static int RideMinutes(double distanceKm)
    {
        if (!double.IsFinite(distanceKm) || distanceKm <= 0.0)
        {
            return MinRideMinutes;
        }

        return Math.Max(
            MinRideMinutes,
            (int)Math.Round(distanceKm / AverageDriveSpeedKmH * 60.0));
    }

    /// <summary>
    /// XP one completed request pays before the diminishing-returns multiplier:
    /// (distance km × car class × friendship level) / 500, with a stranger
    /// (level 0) counting half. Zero for a car the garage cannot name
    /// (class 0), which is Unity's own reading for an unknown car.
    /// </summary>
    public static double Xp(double distanceKm, int carClass, int friendshipLevel)
    {
        double friendship = friendshipLevel > 0
            ? friendshipLevel
            : StrangerFriendshipFactor;
        return Math.Max(0.0, distanceKm) * Math.Max(0, carClass) * friendship / XpDivisor;
    }

    /// <summary>
    /// What a whole day's batch pays an employee: the sum of every request's
    /// XP, zero when the town booked nothing.
    /// </summary>
    public static double BatchXp(IReadOnlyList<CarShareRequest> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        double total = 0.0;
        foreach (CarShareRequest request in batch)
        {
            total += request.Xp;
        }

        return total;
    }

    /// <summary>
    /// The car-sharing job a day's batch hands an employee: timed to the last
    /// ride's arrival and paying the batch total, or a job with no finish while
    /// the town booked nothing or the rides already left. An employee can never
    /// arrive for work nobody did: the row keeps reading ON JOB until the next
    /// nightly schedule supplies a batch that is still ahead.
    /// </summary>
    public static EmployeeJobAssignment AssignmentFor(
        IReadOnlyList<CarShareRequest> batch, long gameMinute, int gameMinuteOfDay)
    {
        ArgumentNullException.ThrowIfNull(batch);

        int lastArrival = LastArrival(batch);
        // WHY the day's zero point: the batch is authored in minutes of the day,
        // so the arrival becomes an absolute game minute the row can compare
        // against the clock, whether the shift is taken at dawn or resumed at
        // night.
        long dayStart = gameMinute - gameMinuteOfDay;
        return lastArrival > gameMinuteOfDay
            ? new EmployeeJobAssignment(
                EmployeeJobKind.CarSharing, dayStart + lastArrival, BatchXp(batch))
            : new EmployeeJobAssignment(EmployeeJobKind.CarSharing);
    }

    private static int LastArrival(IReadOnlyList<CarShareRequest> batch)
    {
        int last = -1;
        foreach (CarShareRequest request in batch)
        {
            if (request.ArrivalMinute > last)
            {
                last = request.ArrivalMinute;
            }
        }

        return last;
    }

    /// <summary>
    /// The day's draw seed. WHY the day: the schedule is one batch per game day,
    /// so a page the player reopens must replay the same rides instead of
    /// reshuffling the town; the multiplier only scopes the stream away from
    /// the dealer's week draw.
    /// </summary>
    public static int SeedFor(int gameDay) => gameDay * 131;
}
