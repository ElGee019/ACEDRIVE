using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The odometer feed and engine inspections (PORTPLAN items 31/32,
/// Unity DrivingManager's speed integration + RunEngineInspections): every live
/// tick banks the current car's distance, the garage bank is throttled, and the
/// weekly/annual inspections ride the drive past the intro gate.</summary>
public sealed class ConditionFlowTests
{
    private static OnboardingState Done => OnboardingState.Fresh with { BirthdayDone = true };

    [Fact]
    public void Poll_BanksTheDrivenDistanceIntoTheCurrentCar()
    {
        var garage = new Garage(TestRoadCars.Pool);
        OwnedCar car = garage.Add(TestRoadCars.Pool[0], week: 1);
        var store = new FakeGarageStore();
        var (vm, clock, _, _) = MainViewModelTests.Create(
            speedKmh: 90f, garage: garage, garageStore: store);

        clock.Advance(TimeSpan.FromSeconds(0.5));
        vm.Poll();

        // 90 km/h × 0.5 s = 0.0125 km.
        Assert.Equal(0.0125, car.Odometer, 6);
    }

    [Fact]
    public void Poll_SkipsStalledAndStationaryTicks()
    {
        var garage = new Garage(TestRoadCars.Pool);
        OwnedCar car = garage.Add(TestRoadCars.Pool[0], week: 1);
        var (vm, clock, _, _) = MainViewModelTests.Create(speedKmh: 0f, garage: garage);

        clock.Advance(TimeSpan.FromSeconds(5)); // stalled tick
        vm.Poll();
        clock.Advance(TimeSpan.FromSeconds(0.5)); // stationary
        vm.Poll();

        Assert.Equal(0.0, car.Odometer);
    }

    [Fact]
    public void Poll_ClampsAGlitchedSpeedSample()
    {
        var garage = new Garage(TestRoadCars.Pool);
        OwnedCar car = garage.Add(TestRoadCars.Pool[0], week: 1);
        var (vm, clock, tel, _) = MainViewModelTests.Create(speedKmh: 90f, garage: garage);
        tel.Next = tel.Next with { SpeedKmh = 100000f }; // a one-frame teleport read

        clock.Advance(TimeSpan.FromSeconds(0.5));
        vm.Poll();

        // Clamped to 500 km/h before it reaches the ledger.
        Assert.Equal(500.0 / 3600.0 * 0.5, car.Odometer, 6);
    }

    [Fact]
    public void Poll_ThrottlesTheGarageBank()
    {
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var store = new FakeGarageStore();
        var (vm, clock, _, _) = MainViewModelTests.Create(
            speedKmh: 90f, garage: garage, garageStore: store);

        clock.Advance(TimeSpan.FromSeconds(0.5));
        vm.Poll();
        Assert.Equal(0, store.Saves);

        clock.Advance(TimeSpan.FromSeconds(61));
        vm.Poll();
        Assert.Equal(1, store.Saves);

        clock.Advance(TimeSpan.FromSeconds(1));
        vm.Poll();
        Assert.Equal(1, store.Saves); // restamped throttle: still one bank
    }

    [Fact]
    public void Poll_SchedulesTheEngineOnTheFirstDrive()
    {
        var garage = new Garage(TestRoadCars.Pool);
        OwnedCar car = garage.Add(TestRoadCars.Pool[0], week: 1);
        var (vm, _, _, _) = MainViewModelTests.Create(
            speedKmh: 90f, garage: garage, progress: Done);

        vm.Poll();

        Assert.True(car.Engine.IsScheduled);
    }

    [Fact]
    public void Poll_RunsTheWeeklyInspectionOnceTheWeekPassed()
    {
        var garage = new Garage(TestRoadCars.Pool);
        OwnedCar car = garage.Add(TestRoadCars.Pool[0], week: 1);
        var (vm, clock, _, _) = MainViewModelTests.Create(
            speedKmh: 90f, garage: garage, progress: Done);

        vm.Poll(); // first drive: schedules at the current game moment
        DateTime first = car.Engine.WeeklyNext!.Value;

        // Eight game days (53 real minutes each): past the seven-day window.
        clock.Advance(TimeSpan.FromMinutes(8 * 53));
        vm.Poll();

        Assert.True(car.Engine.WeeklyNext!.Value > first);
        // The schedule rolls from the due moment: start 2026-10-12 + ~8 days,
        // then +7 days again.
        Assert.Equal(new DateTime(2026, 10, 27), car.Engine.WeeklyNext!.Value.Date);
        Assert.False(car.Engine.Broken);
    }

    [Fact]
    public void Poll_AnnualInspectionCanStrandTheCar()
    {
        // A car with a full red-line odometer against a fresh base: the annual
        // roll always fails (100% broken), so the engine strands.
        var pool = TestRoadCars.Pool;
        var garage = new Garage(pool, new GarageState(pool[0].Key, [
            new OwnedCarState(pool[0].Key, 1, 500000.0, false, 0, null, new EngineConditionState(0.0)),
        ]));
        var (vm, clock, _, _) = MainViewModelTests.Create(
            speedKmh: 90f, garage: garage, progress: Done, cars: pool);

        vm.Poll(); // schedules the annual check
        clock.Advance(TimeSpan.FromMinutes(373 * 53)); // past the 364-371 day window
        vm.Poll();

        Assert.True(garage.Current!.Engine.Broken);
    }

    [Fact]
    public void Poll_BeforeOnboarding_KeepsTheEngineDormant()
    {
        var garage = new Garage(TestRoadCars.Pool);
        OwnedCar car = garage.Add(TestRoadCars.Pool[0], week: 1);
        var (vm, clock, _, _) = MainViewModelTests.Create(speedKmh: 90f, garage: garage);

        clock.Advance(TimeSpan.FromMinutes(8 * 53));
        vm.Poll();

        Assert.False(car.Engine.IsScheduled);
    }

    [Fact]
    public void TaxiDrive_BanksTheDistanceIntoTheCurrentCar()
    {
        // The odometer feed banks the passenger leg's live distance into the
        // current car, so the settle must not add it a second time.
        var garage = new Garage(TestRoadCars.Pool);
        OwnedCar car = garage.Add(TestRoadCars.Pool[0], week: 1);
        var (vm, clock, tel, _) = MainViewModelTests.Create(
            speedKmh: 0f, garage: garage, garageStore: new FakeGarageStore(),
            progress: Done);

        TaxiFlow.AcceptAndDrive(vm, clock, tel);

        Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(8.2, car.Odometer, 6);
    }

    [Fact]
    public void DeliveryRun_BanksTheDrivenDistanceIntoTheCurrentCar()
    {
        // The delivery twin: the live odometer integration already banks the
        // store leg, so the settle must not add a second placeholder distance.
        var catalog = new DeliveryCatalog(
            [new DeliveryStore(
                "adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries",
                new AddressLocation("Street 2", 5, HouseSide.Left))],
            0.7, 100);
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])],
            CredPRamp.UnityDefault, ["Elena Marchetti"]);
        var garage = new Garage(TestRoadCars.Pool);
        OwnedCar car = garage.Add(TestRoadCars.Pool[0], week: 1);
        var hud = new FakeTelemetryHud();
        var session = MainViewModelTests.Create(
            catalog: catalog, hubs: hubs, speedKmh: 0f, garage: garage,
            garageStore: new FakeGarageStore(), hud: hud);
        var (vm, clock, tel, _) = session;
        session.Story.FinishDialogue();
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen).JobsCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen).DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);
        board.ToggleOrderCommand.Execute(board.Rows[0]);
        board.MainButtonCommand.Execute(null);

        hud.RaiseJobEnter(); // load the freight at the hub
        TaxiFlow.Drive(vm, clock, tel, TaxiFlow.DefaultDistanceKm);
        hud.RaiseJobEnter(); // deliver the store

        Assert.IsType<DeliveryReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(8.2, car.Odometer, 6);
    }
}
