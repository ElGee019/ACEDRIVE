using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The assign-a-job page the employees page's NOT ON JOB button opens: the three
/// work-family buttons assign the employee and return, BACK returns unchanged,
/// the finished-job teal flags read through <see cref="EmployeeJobBoard"/>, and
/// a car-sharing pick hands the day's batch to the row as a timed job — or
/// leaves it waiting when the town booked no ride the employee can drive.
/// </summary>
public sealed class AssignJobScreenViewModelTests
{
    private static readonly DateTime GameStart = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);

    public AssignJobScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static (AssignJobScreenViewModel Vm, EmployeeAssignments Assignments, FakeClock Clock,
        GameClock Game, VoiceBeat Beat) Create(
        string employeeName = "Anna Berg",
        EmployeeAssignments? assignments = null,
        EmployeeJobBoard? jobs = null,
        IReadOnlyList<CarShareRequest>? requests = null,
        Action? back = null)
    {
        var assigned = assignments ?? new EmployeeAssignments(new FakeEmployeeJobStore());
        var clock = new FakeClock();
        var game = new GameClock(clock, GameStart);
        var beat = new VoiceBeat(clock);
        var zone = new OperatingZone(
            null, seed: 0, AddressMap.Default,
            initialKm: AddressBook.MapLengthKm(AddressMap.Default));
        var vm = new AssignJobScreenViewModel(
            employeeName,
            new EmployeeRides(assigned, game, zone, new Random(7)),
            new EmployeeDeliveries(assigned, game, () => [Store], new Random(7)),
            new CarShareJobs(assigned, game),
            jobs ?? new EmployeeJobBoard(),
            requests ?? [],
            new RelayCommand(back ?? (() => { })),
            beat);
        return (vm, assigned, clock, game, beat);
    }

    // A store with room on the shelves: an employee refill has something to add.
    private static DeliveryStore Store =>
        new("store_a", "Test Store", "Owner", StockedUnits: 5, UnitCapacity: 20, PricePerUnit: 1.0);

    [Fact]
    public void EachJobButton_AssignsThatFamilyAndReturns()
    {
        var (vm, assignments, _, _, _) = Create(requests: [Ride()]);
        Assert.False(assignments.IsOnJob("Anna Berg"));

        vm.CarSharingCommand.Execute(null);
        Assert.Equal(EmployeeJobKind.CarSharing, assignments.JobOf("Anna Berg"));

        vm.TaxiCommand.Execute(null);
        Assert.Equal(EmployeeJobKind.TaxiJobs, assignments.JobOf("Anna Berg"));

        vm.DeliveryCommand.Execute(null);
        Assert.Equal(EmployeeJobKind.DeliveryJobs, assignments.JobOf("Anna Berg"));
    }

    [Fact]
    public void TaxiPick_TimesARideByItsDrivePlusHandlingAndPaysItsDistance()
    {
        var (vm, assignments, _, game, _) = Create();

        vm.TaxiCommand.Execute(null);

        EmployeeJobAssignment taxi = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        // The ride adds the two-hour handling block on top of the drive, so it
        // can never land inside the block however short the drawn distance is.
        Assert.True(taxi.DueGameMinute >= game.GameMinute + EmployeeJobRules.HandlingMinutes);
        Assert.True(taxi.Cr > 0.0);
        Assert.Equal(0.0, taxi.Xp);
    }

    [Fact]
    public void DeliveryPick_AssignsA24HourRefillOfOneStore()
    {
        var (vm, assignments, _, game, _) = Create();

        vm.DeliveryCommand.Execute(null);

        EmployeeJobAssignment delivery = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        // Unity's delivery always lands exactly one day after the click...
        Assert.Equal(game.GameMinute + 1440, delivery.DueGameMinute);
        // ...and its payout is only knowable once the store is refilled.
        Assert.Equal(0.0, delivery.Cr);
        Assert.Equal("store_a", delivery.StoreKey);
    }

    [Fact]
    public void AJobButton_ReturnsToTheEmployeesPage()
    {
        int backed = 0;
        var (vm, _, _, _, _) = Create(back: () => backed++);

        vm.TaxiCommand.Execute(null);

        Assert.Equal(1, backed);
    }

    [Fact]
    public void Back_ReturnsWithoutAssigning()
    {
        int backed = 0;
        var (vm, assignments, _, _, _) = Create(back: () => backed++);

        vm.BackCommand.Execute(null);

        Assert.Equal(1, backed);
        Assert.False(assignments.IsOnJob("Anna Berg"));
    }

    [Fact]
    public void FinishedJob_TurnsOnlyThatFamilysFlagTeal()
    {
        var jobs = new EmployeeJobBoard();
        jobs.MarkFinished(EmployeeJobKind.TaxiJobs);

        var (vm, _, _, _, _) = Create(jobs: jobs);

        Assert.False(vm.CarSharingFinished);
        Assert.True(vm.TaxiJobsFinished);
        Assert.False(vm.DeliveryJobsFinished);
    }

    [Fact]
    public void Back_VoiceTapGlowsThenReturns()
    {
        bool backed = false;
        var (vm, _, clock, _, beat) = Create(back: () => backed = true);

        vm.VoiceActions[Strings.Get("AssignJobBack")].Invoke();

        Assert.True(vm.BackFlash);
        Assert.False(backed);
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.True(backed);
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void CarSharePick_HandsTheBatchToTheRowWithItsLastArrival()
    {
        var (vm, assignments, _, game, _) = Create(requests:
        [
            Ride(leaveMinute: 500, xp: 0.125),
            Ride(leaveMinute: 600, xp: 0.250),
        ]);

        vm.CarSharingCommand.Execute(null);

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        Assert.Equal(EmployeeJobKind.CarSharing, job.Kind);
        // The second ride leaves at 10:00 and drives ten minutes; the page was
        // opened at 08:00, so the row arrives 130 game minutes from now.
        Assert.Equal(game.GameMinute + 130, job.DueGameMinute);
        Assert.Equal(0.375, job.Xp, precision: 10);
    }

    [Fact]
    public void QuietDay_LeavesTheEmployeeWaitingForRealRides()
    {
        int backed = 0;
        var (vm, assignments, _, _, _) = Create(back: () => backed++);

        vm.CarSharingCommand.Execute(null);

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        Assert.Equal(EmployeeJobKind.CarSharing, job.Kind);
        // No rides were booked, so there is no finish minute to arrive at: the
        // row stays ON JOB until the nightly schedule supplies a real batch.
        Assert.Null(job.DueGameMinute);
        Assert.Equal(0.0, job.Xp);
        Assert.True(assignments.IsOnJob("Anna Berg"));
        Assert.Equal(1, backed);
    }

    [Fact]
    public void ABatchThatAlreadyLeft_AlsoLeavesTheEmployeeWaiting()
    {
        var (vm, assignments, _, _, _) = Create(requests: [Ride(leaveMinute: 400)]);

        vm.CarSharingCommand.Execute(null);

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        // The rides left at 06:40, before the 08:00 tap: no work to arrive for.
        Assert.Null(job.DueGameMinute);
        Assert.Equal(0.0, job.Xp);
    }

    [Fact]
    public void CarShareRequests_AreFormattedWithTheResxShapes()
    {
        var request = new CarShareRequest(
            "Lena Vogt", Door("Hafenstrasse", 12), Door("Ringstrasse", 4),
            4.2, (9 * 60) + 40, 0.34);

        var (vm, _, _, _, _) = Create(requests: [request]);

        Assert.True(vm.HasCarShareRequests);
        CarShareRequestRow row = Assert.Single(vm.CarShareRequests);
        Assert.Equal("09:40", row.Time);
        Assert.Equal("Lena Vogt", row.Customer);
        Assert.Equal("Hafenstrasse · No. 12 → Ringstrasse · No. 4", row.Route);
        Assert.Equal("4.2 km", row.Distance);
    }

    [Fact]
    public void NoCarShareRequests_ReadsAsAQuietDay()
    {
        var (vm, _, _, _, _) = Create();

        Assert.False(vm.HasCarShareRequests);
        Assert.Empty(vm.CarShareRequests);
    }

    private static CarShareRequest Ride(int leaveMinute = 500, double xp = 0.34) =>
        new("Lena Vogt", Door("Hafenstrasse", 12), Door("Ringstrasse", 4),
            4.2, leaveMinute, xp);

    private static AddressLocation Door(string street, int number) =>
        new(street, number, HouseNumberPlan.SideOf(number));
}
