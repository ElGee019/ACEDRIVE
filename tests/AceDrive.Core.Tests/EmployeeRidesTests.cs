using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The crew's taxi work, ported from LATESTDRIVEBS2 EmployeeService.AssignRide:
/// the ride is drawn between two town doors and timed by its drive plus the
/// handling block, paying its distance in CR.
/// </summary>
public sealed class EmployeeRidesTests
{
    private static readonly DateTime GameStart = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);

    private static (EmployeeRides Rides, EmployeeAssignments Assignments, GameClock Game,
        OperatingZone Zone) Create()
    {
        var clock = new FakeClock();
        var game = new GameClock(clock, GameStart);
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        OperatingZone zone = new(
            null, seed: 0, AddressMap.Default,
            initialKm: AddressBook.MapLengthKm(AddressMap.Default));
        return (new EmployeeRides(assignments, game, zone, new Random(3)), assignments, game, zone);
    }

    [Fact]
    public void Assign_TimesTheRideByItsDrivePlusHandlingAndPaysItsDistance()
    {
        var (rides, assignments, game, zone) = Create();

        bool assigned = rides.Assign("Anna Berg");

        Assert.True(assigned);
        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        Assert.Equal(EmployeeJobKind.TaxiJobs, job.Kind);
        Assert.Equal(0.0, job.Xp);

        // Replay the same draw to know the doors the service drove between.
        var replay = new Random(3);
        Assert.True(TaxiAddressPlan.TryDraw(
            zone.ZoneMap, replay, home: null, radiusKm: 0,
            out AddressLocation pickup, out AddressLocation dropoff));
        double distanceKm = AddressBook.DistanceMeters(zone.ZoneMap, pickup, dropoff) / 1000.0;

        Assert.Equal(
            game.GameMinute + EmployeeJobRules.RideMinutes(distanceKm),
            job.DueGameMinute);
        Assert.Equal(
            Math.Round(EmployeeJobRules.RidePay(distanceKm), 2, MidpointRounding.AwayFromZero),
            job.Cr, precision: 10);
        Assert.True(job.Cr > 0.0);
    }

    [Fact]
    public void Assign_PutsTheEmployeeOnTheRide()
    {
        var (rides, assignments, _, _) = Create();

        rides.Assign("Anna Berg");

        Assert.True(assignments.IsOnJob("Anna Berg"));
    }
}
