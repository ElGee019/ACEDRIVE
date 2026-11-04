// The employees' taxi work (PORTPLAN item 23), port of LATESTDRIVEBS2
// EmployeeService.AssignRide: a house-to-house ride between two different town
// doors, timed by the drive plus the handling block, paying its distance in CR.
namespace AceDrive.Core;

/// <summary>
/// Puts an employee on one house-to-house ride. The ride is drawn like the
/// taxi proposal's (two doors on the operating zone's map, never the same one,
/// never the player's own door), then timed by
/// <see cref="EmployeeJobRules.RideMinutes"/> and paid by
/// <see cref="EmployeeJobRules.RidePay"/>. Writes through
/// <see cref="EmployeeAssignments"/>, which stays the one owner of the on-job
/// marks; a town that cannot offer two doors leaves the employee unassigned
/// instead of booking a ride to nowhere.
/// </summary>
public sealed class EmployeeRides
{
    private readonly EmployeeAssignments _assignments;
    private readonly GameClock _game;
    private readonly OperatingZone _zone;
    private readonly Random _rng;

    public EmployeeRides(
        EmployeeAssignments assignments, GameClock game, OperatingZone zone, Random rng)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(rng);

        _assignments = assignments;
        _game = game;
        _zone = zone;
        _rng = rng;
    }

    /// <summary>
    /// Assigns the ride and answers true, or answers false (assigning nothing)
    /// when the town cannot offer two different doors or the drawn pair has no
    /// distance to drive.
    /// </summary>
    public bool Assign(string? employeeName)
    {
        if (!TaxiAddressPlan.TryDraw(
                _zone.ZoneMap, _rng, home: null, radiusKm: 0,
                out AddressLocation pickup, out AddressLocation dropoff))
        {
            return false;
        }

        double distanceKm = AddressBook.DistanceMeters(_zone.ZoneMap, pickup, dropoff) / 1000.0;
        if (!double.IsFinite(distanceKm) || distanceKm <= 0.0)
        {
            return false;
        }

        _assignments.Assign(employeeName, new EmployeeJobAssignment(
            EmployeeJobKind.TaxiJobs,
            // WHY the absolute minute: the due moment is stored, so a restart
            // resumes the same remaining ride the same way car sharing does.
            _game.GameMinute + EmployeeJobRules.RideMinutes(distanceKm),
            Cr: Round(EmployeeJobRules.RidePay(distanceKm))));
        return true;
    }

    // Money is banked in cents elsewhere; the assignment already carries the
    // amount the row will print, so it rounds once here instead of at collect.
    private static double Round(double amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
