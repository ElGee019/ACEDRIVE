using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The dry-tank flow (PORTPLAN 38, Unity's OnFuelOutDetected +
/// RepairScreen + FailJobOnFuelOut): a live run-dry opens the repair page, its
/// bill is taken in full, the interrupted ride or delivery is failed, and the
/// paid litres come off the next fill for the same car. A tank that is already
/// dry when the session opens gets Unity's road-service button instead.</summary>
public sealed class FuelOutFlowTests
{
    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16,
        Price: 12000.0, TankLitres: 50.0, FuelPer100Km: 8.0);

    private static OnboardingState Done => OnboardingState.Fresh with { BirthdayDone = true };

    private sealed record Session(
        TestSessionResult Result, FakeFuelPriceStore Prices, Garage Garage,
        DriverWallet Wallet, FakeFriendshipStore Friends);

    private static Session Build(
        float startFuel, double cash = 1000.0, FakeFriendshipStore? friends = null,
        Trunk? trunk = null)
    {
        var garage = new Garage([Car]);
        garage.Add(Car, week: 1);
        var wallet = new DriverWallet(new DriverState(cash, 0.0));
        var prices = new FakeFuelPriceStore();
        FakeFriendshipStore friendsStore = friends ?? new FakeFriendshipStore();
        var tel = new FakeTelemetry
        {
            Next = new TelemetrySample(
                1, 0f, 0f, 4, 5000, 0f, 0f, FuelLitres: startFuel),
        };
        TestSessionResult result = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet, garage: garage,
            cars: [Car], physics: tel, fuelPrices: prices, friendships: friendsStore,
            trunk: trunk);
        return new Session(result, prices, garage, wallet, friendsStore);
    }

    private static void Fuel(Session session, float litres, int packet, bool inPit = false) =>
        session.Result.Tel.Next = session.Result.Tel.Next with
        {
            FuelLitres = litres,
            PacketId = packet,
            InPit = inPit,
        };

    [Fact]
    public void RunningDry_OpensTheRepairPage_AndChargesTheRoadsideBill()
    {
        Session session = Build(startFuel: 20f);
        MainViewModel vm = session.Result.Vm;
        vm.Poll(); // the live reading is above zero
        double before = session.Wallet.Cash;

        Fuel(session, litres: 0f, packet: 2);
        vm.Poll();

        var page = Assert.IsType<FuelOutScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Your car ran out of fuel.", page.Message);
        Assert.Equal(50.0, page.Outage.LitresNeeded, 6);
        Assert.Equal(50.0 * 1.65 + 100.0, page.Outage.RepairCost, 6);
        Assert.Contains("Cash before: CR", page.CashLine);

        page.RepairCommand.Execute(null);

        Assert.Equal(before - (50.0 * 1.65 + 100.0), session.Wallet.Cash, 6);
        Assert.Equal(50.0, session.Prices.Saved!.PrepaidLitres, 6);
        Assert.Equal(Car.Key, session.Prices.Saved.PrepaidCarKey);
        // REPAIR resumes the page the interruption took (the menu here).
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void Repairing_A_PaidTank_MakesTheNextFillFreeForThatCar()
    {
        Session session = Build(startFuel: 20f);
        MainViewModel vm = session.Result.Vm;
        vm.Poll();
        double before = session.Wallet.Cash;

        Fuel(session, litres: 0f, packet: 2);
        vm.Poll();
        Assert.IsType<FuelOutScreenViewModel>(vm.CurrentScreen).RepairCommand.Execute(null);

        // The player drives to a forecourt (past the 100 m anti-top-up gate)
        // and EVO pumps the tank full. The repair already paid for it.
        session.Garage.Current!.Drive(0.2);
        session.Result.Clock.Advance(TimeSpan.FromSeconds(2));
        Fuel(session, litres: 0f, packet: 3);
        vm.Poll();
        session.Result.Clock.Advance(TimeSpan.FromSeconds(2));
        Fuel(session, litres: 50f, packet: 4);
        vm.Poll();
        session.Result.Clock.Advance(TimeSpan.FromSeconds(2));
        vm.Poll();

        var receipt = Assert.IsType<FuelReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.True(receipt.Receipt.HasPrepaid);
        Assert.Equal(50.0, receipt.Receipt.PrepaidLitres, 6);
        Assert.Equal(0.0, receipt.Receipt.Total, 6);
        Assert.False(receipt.Receipt.Free);
        // Nothing was billed a second time, and the credit is spent.
        Assert.Equal(before - (50.0 * 1.65 + 100.0), session.Wallet.Cash, 6);
        Assert.Equal(0.0, session.Prices.Saved!.PrepaidLitres, 6);
    }

    [Fact]
    public void RunningDry_DuringATaxiRide_FailsTheRideAndTheCustomerFriendship()
    {
        var friends = new FakeFriendshipStore();
        Session session = Build(startFuel: 20f, friends: friends);
        MainViewModel vm = session.Result.Vm;
        vm.Poll();

        TaxiFlow.Propose(vm);
        vm.PickupCommand.Execute(null);

        Fuel(session, litres: 0f, packet: 2);
        vm.Poll();

        Assert.IsType<FuelOutScreenViewModel>(vm.CurrentScreen).RepairCommand.Execute(null);

        Assert.Equal(-1, Assert.Single(friends.Deltas).Delta);
        Assert.IsNotType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void ASessionThatOpensOnAnEmptyTank_OffersRoadServiceInsteadOfTheRepairPage()
    {
        Session session = Build(startFuel: 0f);
        MainViewModel vm = session.Result.Vm;
        vm.Poll();

        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        menu.JobsCommand.Execute(null);
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.True(control.ShowRoadService);
        Assert.Equal("CALL ROAD SERVICE (300 CR)", control.RoadServiceLabel);
        double before = session.Wallet.Cash;

        control.RoadServiceCommand.Execute(null);

        Assert.Equal(before - 300.0, session.Wallet.Cash, 6);
        Assert.Equal(50.0, session.Prices.Saved!.PrepaidLitres, 6);
        Assert.False(control.ShowRoadService);
    }

    [Fact]
    public void AFreeCarRepairCard_CoversTheRoadsideBill()
    {
        RewardCard card = RewardTable.Default.Find(RewardCardEffect.FreeCarRepair)!;
        var trunk = new Trunk([new TrunkItem(card.Title, 0.0) { Quantity = 1 }]);
        Session session = Build(startFuel: 20f, trunk: trunk);
        MainViewModel vm = session.Result.Vm;
        vm.Poll();
        double before = session.Wallet.Cash;

        Fuel(session, litres: 0f, packet: 2);
        vm.Poll();
        var page = Assert.IsType<FuelOutScreenViewModel>(vm.CurrentScreen);
        Assert.True(page.FreeRepair);
        Assert.Equal(0.0, page.EffectiveCost, 6);
        Assert.Contains("free", page.RepairCostLine);

        page.RepairCommand.Execute(null);

        Assert.Equal(before, session.Wallet.Cash, 6);
        Assert.Equal(50.0, session.Prices.Saved!.PrepaidLitres, 6);
        Assert.DoesNotContain(trunk.Items, item => item.Name == card.Title);
    }

    [Fact]
    public void AShortWallet_PrintsTheRoadServiceRefusal_AndTheCommandCannotRun()
    {
        Session session = Build(startFuel: 0f, cash: 10.0);
        MainViewModel vm = session.Result.Vm;
        vm.Poll();
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen).JobsCommand.Execute(null);
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        Assert.True(control.ShowRoadService);
        Assert.Equal("NEED 300 CR FOR ROAD SERVICE", control.RoadServiceLabel);
        Assert.False(control.RoadServiceCommand.CanExecute(null));
    }

    [Fact]
    public void StoppingAtAPumpOnEmpty_DoesNotOpenTheRepairPage()
    {
        Session session = Build(startFuel: 20f);
        MainViewModel vm = session.Result.Vm;
        vm.Poll();

        Fuel(session, litres: 0f, packet: 2, inPit: true);
        vm.Poll();

        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void BeforeTheIntroduction_NoDryTankFlow()
    {
        var garage = new Garage([Car]);
        garage.Add(Car, week: 1);
        var tel = new FakeTelemetry
        {
            Next = new TelemetrySample(1, 0f, 0f, 4, 5000, 0f, 0f, FuelLitres: 0f),
        };
        TestSessionResult result = MainViewModelTests.Create(
            speedKmh: 0f, garage: garage, cars: [Car], physics: tel);

        result.Vm.Poll();

        Assert.IsNotType<FuelOutScreenViewModel>(result.Vm.CurrentScreen);
    }
}
