using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The fuel receipt session (PORTPLAN item 37): EVO's own pump moves the
/// tank, so the poll prices the rise, banks it with the attendant's friendship
/// and opens the page — never billing the top-ups EVO makes on load, restart or
/// a pit stop, and never billing the introduction.</summary>
public sealed class FuelFlowTests
{
    private static OnboardingState Done => OnboardingState.Fresh with { BirthdayDone = true };

    private static FuelRules Rules => FuelRules.Default;

    private static DriverWallet Wallet(double cash) => new(new DriverState(cash, 0.0));

    // A garage with a car on the road: the fuel flow prices against its
    // odometer, so the drive-away-from-spawn gate needs a real car.
    private static Garage GarageWithCar()
    {
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        return garage;
    }

    // One fill through the live loop: prime (baseline), drive away from the
    // spawn, let the tracker re-prime at the level the car drove in with, then
    // a rising sample and a still one past the settle window. The extra prime
    // tick is what production does too: the first sample after the drive gate
    // opens is the baseline, never a fill.
    private static void Fill(
        MainViewModel vm, FakeTelemetry tel, FakeClock clock, Garage garage,
        float from, float to, bool inPit = false)
    {
        tel.Next = tel.Next with { FuelLitres = from, InPit = inPit, PacketId = 1 };
        vm.Poll();
        garage.Current!.Drive(0.2); // 200 m: past the 100 m drive gate
        clock.Advance(TimeSpan.FromSeconds(2));
        vm.Poll();                 // primes at `from`
        clock.Advance(TimeSpan.FromSeconds(2));
        tel.Next = tel.Next with { FuelLitres = to, InPit = inPit, PacketId = 2 };
        vm.Poll();
        clock.Advance(TimeSpan.FromSeconds(2));
        vm.Poll();
    }

    [Fact]
    public void ARefuel_ChargesThePrice_AndRaisesTheAttendantsFriendship()
    {
        Garage garage = GarageWithCar();
        var wallet = Wallet(100.0);
        var friends = new FakeFriendshipStore();
        var prices = new FakeFuelPriceStore();
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet, friendships: friends,
            garage: garage, fuelRules: Rules, fuelPrices: prices);
        double before = wallet.Cash; // the session-start login bonus lands first

        Fill(session.Vm, session.Tel, session.Clock, garage, from: 20f, to: 55f);

        var page = Assert.IsType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(35.0, page.Receipt.Litres, 6);
        Assert.InRange(
            page.Receipt.PricePerLitre, Rules.MinPricePerLitre, Rules.MaxPricePerLitre);
        Assert.Equal(before - page.Receipt.Total, wallet.Cash, 6);
        Assert.Equal(1, friends.GetLevel(Rules.OwnerName));
        Assert.Equal(1, page.Receipt.Friendship!.Level);
    }

    [Fact]
    public void ARefuel_StampsTheOneTimeFuelStationVisit()
    {
        Garage garage = GarageWithCar();
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: Wallet(100.0),
            garage: garage, fuelRules: Rules);

        Fill(session.Vm, session.Tel, session.Clock, garage, from: 20f, to: 55f);

        // The fill is the visit (PORTPLAN 46): there is no forecourt door, so
        // the receipt edge syncs the list and the completed block names the tip.
        Assert.Contains(
            session.Todos.CompletedLines,
            line => line.CopyKey == "TodoVisitFuelStation");
    }

    [Fact]
    public void ContinuingTheReceipt_HandsBackThePageItInterrupted()
    {
        Garage garage = GarageWithCar();
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: Wallet(100.0),
            garage: garage, fuelRules: Rules);

        Fill(session.Vm, session.Tel, session.Clock, garage, from: 20f, to: 55f);
        var page = Assert.IsType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);

        page.ContinueCommand.Execute(null);

        Assert.IsType<MenuScreenViewModel>(session.Vm.CurrentScreen);
    }

    [Fact]
    public void AShortWallet_LeavesTheFullBillAsDebt_TheFuelIsAlreadyInTheTank()
    {
        Garage garage = GarageWithCar();
        var wallet = Wallet(5.0);
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet,
            garage: garage, fuelRules: Rules);
        double before = wallet.Cash;

        Fill(session.Vm, session.Tel, session.Clock, garage, from: 5f, to: 45f);

        var page = Assert.IsType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(40.0, page.Receipt.Litres, 6);
        Assert.Equal(before - page.Receipt.Total, wallet.Cash, 6);
        Assert.True(wallet.Cash < 0.0);
    }

    [Fact]
    public void AFreeTankRefillCard_CoversTheBill_AndIsSpent()
    {
        Garage garage = GarageWithCar();
        var trunk = new Trunk([new TrunkItem("FREE TANK REFILL", 0.0) { Quantity = 1 }]);
        var wallet = Wallet(100.0);
        var friends = new FakeFriendshipStore();
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet, friendships: friends,
            trunk: trunk, garage: garage, fuelRules: Rules);
        double before = wallet.Cash;

        Fill(session.Vm, session.Tel, session.Clock, garage, from: 20f, to: 55f);

        var page = Assert.IsType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.True(page.Receipt.Free);
        Assert.Equal(0.0, page.Receipt.Total, 6);
        Assert.Equal(before, wallet.Cash, 6);
        Assert.False(session.Glovebox.Carries(RewardCardEffect.FreeTankRefill, session.Trunk));
        // Unity still raises the familiarity on a free fill.
        Assert.Equal(1, friends.GetLevel(Rules.OwnerName));
    }

    [Fact]
    public void BeforeOnboarding_AFillIsNotPriced()
    {
        Garage garage = GarageWithCar();
        var wallet = Wallet(100.0);
        var session = MainViewModelTests.Create(
            speedKmh: 0f, wallet: wallet, garage: garage, fuelRules: Rules);

        Fill(session.Vm, session.Tel, session.Clock, garage, from: 20f, to: 55f);

        Assert.IsNotType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(100.0, wallet.Cash, 6);
    }

    [Fact]
    public void ALoadTopUpBeforeDriving_IsNotBilled_AndTheNextRealFillIs()
    {
        Garage garage = GarageWithCar();
        var wallet = Wallet(100.0);
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet,
            garage: garage, fuelRules: Rules);
        double before = wallet.Cash;

        // EVO fills the tank on load/restart while the car still sits at the
        // spawn: no distance driven, so the rise is a re-prime, not a purchase.
        session.Tel.Next = session.Tel.Next with { FuelLitres = 10f, PacketId = 1 };
        session.Vm.Poll();
        session.Clock.Advance(TimeSpan.FromSeconds(2));
        session.Tel.Next = session.Tel.Next with { FuelLitres = 50f, PacketId = 2 };
        session.Vm.Poll();
        session.Clock.Advance(TimeSpan.FromSeconds(2));
        session.Vm.Poll();

        Assert.IsNotType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(before, wallet.Cash, 6);

        // A fill after actually driving away is priced as usual.
        Fill(session.Vm, session.Tel, session.Clock, garage, from: 50f, to: 60f);
        Assert.IsType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
    }

    [Fact]
    public void APitStopRefuel_IsFree()
    {
        Garage garage = GarageWithCar();
        var wallet = Wallet(100.0);
        var friends = new FakeFriendshipStore();
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet, friendships: friends,
            garage: garage, fuelRules: Rules);
        double before = wallet.Cash;

        Fill(session.Vm, session.Tel, session.Clock, garage, from: 20f, to: 55f, inPit: true);

        Assert.IsNotType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(before, wallet.Cash, 6);
        Assert.Equal(0, friends.GetLevel(Rules.OwnerName));
    }

    [Fact]
    public void APitRefuelInProgress_IsDroppedWhenTheFlagClears()
    {
        Garage garage = GarageWithCar();
        var wallet = Wallet(100.0);
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet,
            garage: garage, fuelRules: Rules);
        double before = wallet.Cash;

        // Drive to the pit and start taking fuel on the road side.
        session.Tel.Next = session.Tel.Next with { FuelLitres = 20f, PacketId = 1 };
        session.Vm.Poll();
        garage.Current!.Drive(0.2);
        session.Clock.Advance(TimeSpan.FromSeconds(2));
        session.Vm.Poll(); // primes at 20 L
        session.Clock.Advance(TimeSpan.FromSeconds(2));
        session.Tel.Next = session.Tel.Next with { FuelLitres = 30f, PacketId = 2 };
        session.Vm.Poll();

        // The box flag arrives mid-fill: the accumulated litres are dropped.
        session.Clock.Advance(TimeSpan.FromSeconds(2));
        session.Tel.Next = session.Tel.Next with { FuelLitres = 40f, InPit = true, PacketId = 3 };
        session.Vm.Poll();

        // Driving out leaves the tank full and the flag clear: still nothing
        // to bill, because the top-up happened in the box.
        session.Clock.Advance(TimeSpan.FromSeconds(4));
        session.Tel.Next = session.Tel.Next with { FuelLitres = 40f, InPit = false, PacketId = 4 };
        session.Vm.Poll();

        Assert.IsNotType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(before, wallet.Cash, 6);
    }

    [Fact]
    public void ANewSessionThatStartsFull_IsNotBilled()
    {
        Garage garage = GarageWithCar();
        var wallet = Wallet(100.0);
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet,
            garage: garage, fuelRules: Rules);
        double before = wallet.Cash;

        session.Tel.Next = session.Tel.Next with { FuelLitres = 20f, PacketId = 10 };
        session.Vm.Poll();
        session.Clock.Advance(TimeSpan.FromSeconds(2));
        // A reload rewinds the physics packet counter and spawns with a full
        // tank: the jump is a baseline, not a purchase.
        session.Tel.Next = session.Tel.Next with { FuelLitres = 60f, PacketId = 1 };
        session.Vm.Poll();
        session.Clock.Advance(TimeSpan.FromSeconds(2));
        session.Vm.Poll();

        Assert.IsNotType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(before, wallet.Cash, 6);
    }

    [Fact]
    public void ThePumpPriceDrift_IsBankedOnTheDayFlip()
    {
        var prices = new FakeFuelPriceStore();
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, fuelRules: Rules, fuelPrices: prices);

        // A little over one game day: the market rolls and banks the new price.
        session.Clock.Advance(TimeSpan.FromSeconds(3200));
        session.Vm.Poll();

        Assert.NotNull(prices.Saved);
        Assert.InRange(
            prices.Saved!.PricePerLitre, Rules.MinPricePerLitre, Rules.MaxPricePerLitre);
        Assert.True(prices.Saves >= 1);
    }
}
