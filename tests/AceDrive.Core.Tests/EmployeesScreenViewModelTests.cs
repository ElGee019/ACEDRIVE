using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The employees page behind the menu's EMPLOYEES tile (PORTPLAN 23): the hired
/// crew only, each row's NOT ON JOB button, ON JOB text or ARRIVED button, the
/// collect that banks the job's pay — XP for car sharing, CR for the shifts —
/// and shows the earned line for three seconds, and the way back to the menu.
/// </summary>
public sealed class EmployeesScreenViewModelTests
{
    private static readonly DateTime GameStart = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);

    private static readonly string[] Crew = ["Anna Berg", "Bram de Wit"];

    public EmployeesScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static (EmployeesScreenViewModel Vm, EmployeeAssignments Assignments, FakeClock Clock,
        GameClock Game, List<double> Banked, List<double> BankedCr, VoiceBeat Beat) Create(
        IReadOnlyList<string>? employees = null,
        EmployeeAssignments? assignments = null,
        Action<string?>? notOnJob = null,
        Action? back = null,
        IReadOnlyList<CarShareRequest>? carShareBatch = null,
        Garage? garage = null,
        bool enforceUnusedCars = true,
        EmployeeDeliveryPayout? deliveryPayout = null)
    {
        var store = new FakeEmployeeStore
        {
            State = new EmployeeState(
                Employees: employees, RosterWeek: 1),
        };
        var roster = new EmployeeRoster(store, new Random(7));
        var assigned = assignments ?? new EmployeeAssignments(new FakeEmployeeJobStore());
        var clock = new FakeClock();
        var game = new GameClock(clock, GameStart);
        var beat = new VoiceBeat(clock);
        var banked = new List<double>();
        var bankedCr = new List<double>();
        // Tests that are not about the garage rule get the player's car plus a
        // car per employee, so a row's NOT ON JOB button is never hidden by an
        // unrelated car shortage.
        Garage owned = garage ?? GarageWith(Crew.Length + 1);
        var vm = new EmployeesScreenViewModel(
            roster, owned, assigned, new CarShareJobs(assigned, game),
            deliveryPayout ?? EmptyDeliveryPayout(),
            () => carShareBatch ?? [],
            game, clock, banked.Add, bankedCr.Add,
            new ParameterizedCommand<string>(notOnJob ?? (_ => { })),
            new RelayCommand(back ?? (() => { })),
            beat,
            enforceUnusedCars);
        return (vm, assigned, clock, game, banked, bankedCr, beat);
    }

    // A payout over an empty board: tests that do not collect a delivery never
    // reach it, and none of them should need a store to be built.
    private static EmployeeDeliveryPayout EmptyDeliveryPayout() =>
        new(new CityEconomy([], CityRules.Default),
            () => DeliveryCatalog.Default,
            new Glovebox(RewardTable.Default, new DriverWallet()),
            new Trunk());

    // A garage holding the first cars of the shared pool: the test needs a
    // count, not the variants themselves.
    private static Garage GarageWith(int carCount)
    {
        var garage = new Garage(TestRoadCars.Pool);
        for (int i = 0; i < carCount; i++)
        {
            garage.Add(TestRoadCars.Pool[i], 1);
        }

        return garage;
    }

    [Fact]
    public void ShowsEveryHiredEmployee_NotOnJob()
    {
        var (vm, _, _, _, _, _, _) = Create(Crew);

        Assert.Equal(2, vm.Employees.Count);
        Assert.Equal("Anna Berg", vm.Employees[0].Name);
        Assert.Equal("Bram de Wit", vm.Employees[1].Name);
        Assert.All(vm.Employees, row => Assert.True(row.ShowNotOnJob));
        Assert.All(vm.Employees, row => Assert.False(row.OnJob));
        Assert.True(vm.HasEmployees);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public void NoUnusedCars_HidesEveryNotOnJobButtonAndShowsTheLine()
    {
        var (vm, _, _, _, _, _, _) = Create(Crew, garage: GarageWith(0));

        Assert.True(vm.ShowNoUnusedCars);
        Assert.Equal(Strings.Get("EmployeesNoUnusedCars"), vm.NoUnusedCarsLine);
        Assert.All(vm.Employees, row => Assert.False(row.ShowNotOnJob));
        Assert.All(vm.Employees, row => Assert.False(row.OnJob));
    }

    [Fact]
    public void AnEmptyCrew_KeepsTheNoUnusedCarsLineHidden()
    {
        // The empty-page hint owns that state: nobody to assign, so the car
        // shortage is not what the player needs to read.
        var (vm, _, _, _, _, _, _) = Create([], garage: GarageWith(0));

        Assert.False(vm.ShowNoUnusedCars);
    }

    [Fact]
    public void ThePlayersOnlyCar_HidesEveryNotOnJobButtonAndShowsTheLine()
    {
        // The player is driving the garage's one car, so there is nothing for
        // the crew to take even before any job is assigned.
        var (vm, _, _, _, _, _, _) = Create(Crew, garage: GarageWith(1));

        Assert.True(vm.ShowNoUnusedCars);
        Assert.All(vm.Employees, row => Assert.False(row.ShowNotOnJob));
    }

    [Fact]
    public void GateOffForTesting_StillOffersTheButtonsOnAOneCarGarage()
    {
        // TEMP: covers the testing switch the composition site passes; remove
        // with the switch once the car gate is restored.
        var (vm, _, _, _, _, _, _) = Create(
            Crew, garage: GarageWith(1), enforceUnusedCars: false);

        Assert.False(vm.ShowNoUnusedCars);
        Assert.All(vm.Employees, row => Assert.True(row.ShowNotOnJob));
    }

    [Fact]
    public void OneUnusedCarWithTwoEmployees_AssigningOneHidesTheOthersButton()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        // Two cars: the player's own plus the one unused car.
        var (vm, _, _, _, _, _, _) = Create(Crew, assignments, garage: GarageWith(2));
        Assert.False(vm.ShowNoUnusedCars);
        Assert.All(vm.Employees, row => Assert.True(row.ShowNotOnJob));

        // The job takes the garage's only unused car: the page comes back with
        // the line up and the idle employee's button gone.
        assignments.Assign("Anna Berg", EmployeeJobKind.TaxiJobs);
        vm.Refresh();

        Assert.True(vm.ShowNoUnusedCars);
        Assert.True(vm.Employees[0].OnJob);
        Assert.False(vm.Employees[1].ShowNotOnJob);
    }

    [Fact]
    public void AJobComingDue_ReturnsTheCarAndBringsTheIdleButtonBack()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        // Two cars: the player's own plus the one the job took.
        var (vm, _, clock, game, _, _, _) = Create(Crew, assignments, garage: GarageWith(2));
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing, game.GameMinute + 10, 0.34);
        vm.Refresh();
        Assert.True(vm.ShowNoUnusedCars);
        Assert.False(vm.Employees[1].ShowNotOnJob);

        clock.Advance(GameMinutes(10));
        vm.Tick(clock.UtcNow);

        Assert.False(vm.ShowNoUnusedCars);
        Assert.True(vm.Employees[0].Arrived);
        Assert.True(vm.Employees[1].ShowNotOnJob);
    }

    [Fact]
    public void NoEmployees_ShowsTheEmptyHint()
    {
        var (vm, _, _, _, _, _, _) = Create([]);

        Assert.Empty(vm.Employees);
        Assert.False(vm.HasEmployees);
        Assert.True(vm.IsEmpty);
        Assert.Equal(Strings.Get("EmployeesEmpty"), vm.EmptyLine);
    }

    [Fact]
    public void AnEmployeeOnJob_ShowsOnJobOnThatRowOnly()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        assignments.Assign("Bram de Wit", EmployeeJobKind.TaxiJobs);

        var (vm, _, _, _, _, _, _) = Create(Crew, assignments);

        Assert.False(vm.Employees[0].OnJob);
        Assert.True(vm.Employees[1].OnJob);
        Assert.False(vm.Employees[1].Arrived);
    }

    [Fact]
    public void ARestartedSession_ResumesTheRowsTheJobFileKept()
    {
        // The previous session assigned Anna a car-sharing job and banked it;
        // this page is built over the same store the next launch would load.
        var store = new FakeEmployeeJobStore();
        var previous = new GameClock(new FakeClock(), GameStart);
        new EmployeeAssignments(store).Assign(
            "Anna Berg", EmployeeJobKind.CarSharing, previous.GameMinute + 10, 0.34);

        var (vm, _, _, _, _, _, _) = Create(Crew, new EmployeeAssignments(store));

        Assert.True(vm.Employees[0].OnJob);
        Assert.False(vm.Employees[0].Arrived);
        Assert.True(vm.Employees[1].ShowNotOnJob);
    }

    [Fact]
    public void AJobPastItsDueMinute_ReadsArrivedOnThatRowOnly()
    {
        var (vm, assignments, _, game, _, _, _) = Create(Crew);
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing, game.GameMinute - 1, 0.34);
        vm.Refresh();

        Assert.True(vm.Employees[0].Arrived);
        Assert.False(vm.Employees[0].OnJob);
        Assert.False(vm.Employees[0].ShowNotOnJob);
        Assert.False(vm.Employees[1].Arrived);
    }

    [Fact]
    public void AJobComingDueWhileThePageIsOpen_FlippedOnTheLoop()
    {
        var (vm, assignments, clock, game, _, _, _) = Create(Crew);
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing, game.GameMinute + 10, 0.34);
        vm.Refresh();
        Assert.True(vm.Employees[0].OnJob);

        clock.Advance(GameMinutes(10));
        vm.Tick(clock.UtcNow);

        Assert.True(vm.Employees[0].Arrived);
        Assert.False(vm.Employees[0].OnJob);
    }

    [Fact]
    public void ArrivedButton_BanksTheXpAndShowsTheEarnedLine()
    {
        var (vm, assignments, _, game, banked, _, _) = Create(Crew);
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing, game.GameMinute - 1, 0.34);
        vm.Refresh();

        vm.ArrivedCommand.Execute("Anna Berg");

        Assert.Equal([0.34], banked);
        EmployeeRow row = vm.Employees[0];
        Assert.Equal("Anna Berg earned 0.340 XP for you.", row.DisplayName);
        Assert.True(row.IsEarning);
        Assert.False(row.Arrived);
        Assert.False(row.ShowNotOnJob);
        Assert.False(assignments.IsOnJob("Anna Berg"));
    }

    [Fact]
    public void ArrivedTaxiRide_BanksTheCrAndShowsTheEarnedLine()
    {
        var (vm, assignments, _, game, banked, bankedCr, _) = Create(Crew);
        assignments.Assign("Anna Berg", new EmployeeJobAssignment(
            EmployeeJobKind.TaxiJobs, game.GameMinute - 1, Cr: 42.5));
        vm.Refresh();

        vm.ArrivedCommand.Execute("Anna Berg");

        // A ride returns CR only: the XP bank stays untouched and the row
        // prints the distance pay the assignment carried.
        Assert.Equal([42.5], bankedCr);
        Assert.Empty(banked);
        EmployeeRow row = vm.Employees[0];
        Assert.Equal("Anna Berg earned 42.50 CR for you.", row.DisplayName);
        Assert.True(row.IsEarning);
        Assert.False(assignments.IsOnJob("Anna Berg"));
    }

    [Fact]
    public void ArrivedDelivery_RefillsTheStoreAndBanksOneCrPerUnitAdded()
    {
        var store = new DeliveryStore(
            "store_a", "Test Store", "Owner",
            StockedUnits: 5, UnitCapacity: 20, PricePerUnit: 1.0);
        var economy = new CityEconomy([store], CityRules.Default);
        var payout = new EmployeeDeliveryPayout(
            economy,
            () => new DeliveryCatalog([store], WholesaleRate: 0.7, StartCredP: 100),
            new Glovebox(RewardTable.Default, new DriverWallet()),
            new Trunk());
        var (vm, assignments, _, game, _, bankedCr, _) = Create(
            Crew, deliveryPayout: payout);
        assignments.Assign("Anna Berg", new EmployeeJobAssignment(
            EmployeeJobKind.DeliveryJobs, game.GameMinute - 1, StoreKey: "store_a"));
        vm.Refresh();

        vm.ArrivedCommand.Execute("Anna Berg");

        // The 24-hour run tops the shelves up to capacity and the collect pays
        // one CR for each unit that landed (20 − 5).
        Assert.Equal([15.0], bankedCr);
        Assert.Equal(store.UnitCapacity, economy.Live(store).StockedUnits);
        Assert.Equal("Anna Berg earned 15.00 CR for you.", vm.Employees[0].DisplayName);
    }

    [Fact]
    public void TheEarnedLine_ReturnsToTheNameAndNotOnJobAfterThreeSeconds()
    {
        var (vm, assignments, clock, game, _, _, _) = Create(Crew);
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing, game.GameMinute - 1, 0.34);
        vm.Refresh();
        vm.ArrivedCommand.Execute("Anna Berg");

        clock.Advance(EmployeeRow.RewardDuration + TimeSpan.FromMilliseconds(1));
        vm.Tick(clock.UtcNow);

        EmployeeRow row = vm.Employees[0];
        Assert.Equal("Anna Berg", row.DisplayName);
        Assert.False(row.IsEarning);
        Assert.True(row.ShowNotOnJob);
        Assert.False(row.OnJob);
        Assert.False(row.Arrived);
    }

    [Fact]
    public void ArrivedButton_BeforeTheDueMinute_BanksNothing()
    {
        var (vm, assignments, _, game, banked, _, _) = Create(Crew);
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing, game.GameMinute + 100, 0.34);
        vm.Refresh();

        vm.ArrivedCommand.Execute("Anna Berg");

        Assert.Empty(banked);
        Assert.True(vm.Employees[0].OnJob);
        Assert.True(assignments.IsOnJob("Anna Berg"));
    }

    [Fact]
    public void ArrivedButton_CollectedTwice_PaysOnce()
    {
        var (vm, assignments, _, game, banked, _, _) = Create(Crew);
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing, game.GameMinute - 1, 0.34);
        vm.Refresh();

        vm.ArrivedCommand.Execute("Anna Berg");
        vm.ArrivedCommand.Execute("Anna Berg");

        Assert.Equal([0.34], banked);
    }

    [Fact]
    public void Refresh_ReadsAnAssignmentThatLandedWhileThePageWasOpen()
    {
        var (vm, assignments, _, _, _, _, _) = Create(Crew);
        Assert.False(vm.Employees[0].OnJob);

        assignments.Assign("Anna Berg", EmployeeJobKind.DeliveryJobs);
        vm.Refresh();

        Assert.True(vm.Employees[0].OnJob);
    }

    [Fact]
    public void NotOnJobButton_RunsTheInjectedCommandWithTheEmployeeName()
    {
        string? carried = null;
        var (vm, _, _, _, _, _, _) = Create(Crew, notOnJob: name => carried = name);

        vm.NotOnJobCommand.Execute("Anna Berg");

        Assert.Equal("Anna Berg", carried);
    }

    [Fact]
    public void Back_GlowsThenRunsTheHostsCommand()
    {
        bool backed = false;
        var (vm, _, clock, _, _, _, beat) = Create(Crew, back: () => backed = true);

        vm.VoiceActions[Strings.Get("EmployeesBack")].Invoke();

        Assert.True(vm.BackFlash);
        Assert.False(backed);
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.True(backed);
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void AWaitingCarShareRow_PicksUpTheDaysBatchWhenThePageOpens()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        // Assigned on a quiet day: on the family with no finish to arrive at.
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing);

        var (vm, _, _, game, _, _, _) = Create(Crew, assignments,
            carShareBatch: [Ride(leaveMinute: 600)]);

        Assert.True(vm.Employees[0].OnJob);
        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        // The ride leaves at 10:00 and drives ten minutes: 08:00 + 130.
        Assert.Equal(game.GameMinute + 130, job.DueGameMinute);
    }

    [Fact]
    public void AWaitingCarShareRow_KeepsWaitingWhileTheTownBooksNothing()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing);

        var (vm, _, _, _, _, _, _) = Create(Crew, assignments);

        Assert.True(vm.Employees[0].OnJob);
        Assert.Null(assignments.AssignmentOf("Anna Berg")?.DueGameMinute);
    }

    private static CarShareRequest Ride(int leaveMinute, double xp = 0.125) =>
        new("Lena Vogt", new AddressLocation("Hafenstrasse", 12, HouseNumberPlan.SideOf(12)),
            new AddressLocation("Ringstrasse", 4, HouseNumberPlan.SideOf(4)),
            4.2, leaveMinute, xp);

    private static TimeSpan GameMinutes(int minutes) =>
        TimeSpan.FromSeconds(minutes * GameClock.RealSecondsPerGameMinute);
}
