using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The crew's car-sharing work: an employee is only ever given a shift the town
/// actually booked, so a quiet day (or one whose rides already left) leaves the
/// row waiting until the schedule supplies a batch that is still ahead.
/// </summary>
public sealed class CarShareJobsTests
{
    private static readonly DateTime GameStart = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);

    private static (CarShareJobs Jobs, EmployeeAssignments Assignments, GameClock Game) Create(
        EmployeeAssignments? assignments = null)
    {
        var clock = new FakeClock();
        var game = new GameClock(clock, GameStart);
        var assigned = assignments ?? new EmployeeAssignments(new FakeEmployeeJobStore());
        return (new CarShareJobs(assigned, game), assigned, game);
    }

    [Fact]
    public void Assign_OnADayWithRides_EndsAtTheLastArrivalAndPaysTheBatch()
    {
        var (jobs, assignments, game) = Create();

        jobs.Assign("Anna Berg", [Ride(leaveMinute: 500), Ride(leaveMinute: 600)]);

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        Assert.Equal(EmployeeJobKind.CarSharing, job.Kind);
        // The last ride leaves at 10:00 and drives ten minutes: 08:00 + 130.
        Assert.Equal(game.GameMinute + 130, job.DueGameMinute);
    }

    [Fact]
    public void Assign_OnAQuietDay_LeavesTheEmployeeWaiting()
    {
        var (jobs, assignments, _) = Create();

        jobs.Assign("Anna Berg", []);

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        Assert.Equal(EmployeeJobKind.CarSharing, job.Kind);
        Assert.Null(job.DueGameMinute);
        Assert.Equal(0.0, job.Xp);
        Assert.True(assignments.IsOnJob("Anna Berg"));
    }

    [Fact]
    public void Assign_AfterTheRidesLeft_LeavesTheEmployeeWaiting()
    {
        var (jobs, assignments, _) = Create();

        // 06:40 + ten minutes is behind the 08:00 tap: nobody can drive it now.
        jobs.Assign("Anna Berg", [Ride(leaveMinute: 400)]);

        Assert.Null(assignments.AssignmentOf("Anna Berg")?.DueGameMinute);
    }

    [Fact]
    public void ArmWaiting_GivesAWaitingEmployeeTheBatchThatRuns()
    {
        var (jobs, assignments, game) = Create();
        jobs.Assign("Anna Berg", []);

        jobs.ArmWaiting(["Anna Berg"], [Ride(leaveMinute: 600, xp: 0.25)]);

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        Assert.Equal(game.GameMinute + 130, job.DueGameMinute);
        Assert.Equal(0.25, job.Xp, precision: 10);
    }

    [Fact]
    public void ArmWaiting_LeavesTheRowWaitingWhileTheTownIsQuiet()
    {
        var (jobs, assignments, _) = Create();
        jobs.Assign("Anna Berg", []);

        jobs.ArmWaiting(["Anna Berg"], []);

        Assert.Null(assignments.AssignmentOf("Anna Berg")?.DueGameMinute);
        Assert.True(assignments.IsOnJob("Anna Berg"));
    }

    [Fact]
    public void ArmWaiting_LeavesTheRowWaitingAfterTheBatchLeft()
    {
        var (jobs, assignments, _) = Create();
        jobs.Assign("Anna Berg", []);

        jobs.ArmWaiting(["Anna Berg"], [Ride(leaveMinute: 400)]);

        Assert.Null(assignments.AssignmentOf("Anna Berg")?.DueGameMinute);
    }

    [Fact]
    public void ArmWaiting_DoesNotTouchAnotherFamily()
    {
        var (jobs, assignments, _) = Create();
        assignments.Assign("Bram de Wit", EmployeeJobKind.TaxiJobs);

        jobs.ArmWaiting(["Bram de Wit"], [Ride(leaveMinute: 600)]);

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Bram de Wit"));
        Assert.Equal(EmployeeJobKind.TaxiJobs, job.Kind);
        Assert.Null(job.DueGameMinute);
    }

    [Fact]
    public void ArmWaiting_LeavesAnAlreadyTimedRowOnItsOwnBatch()
    {
        var (jobs, assignments, game) = Create();
        jobs.Assign("Anna Berg", [Ride(leaveMinute: 500)]);
        long? firstDue = assignments.AssignmentOf("Anna Berg")?.DueGameMinute;

        jobs.ArmWaiting(["Anna Berg"], [Ride(leaveMinute: 600, xp: 0.9)]);

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        Assert.Equal(firstDue, job.DueGameMinute);
        // The 08:20 arrival the row was first given, not the 10:10 one.
        Assert.Equal(game.GameMinute + 30, job.DueGameMinute);
    }

    private static CarShareRequest Ride(int leaveMinute, double xp = 0.125) =>
        new("Lena Vogt", Door("Hafenstrasse", 12), Door("Ringstrasse", 4),
            4.2, leaveMinute, xp);

    private static AddressLocation Door(string street, int number) =>
        new(street, number, HouseNumberPlan.SideOf(number));
}
