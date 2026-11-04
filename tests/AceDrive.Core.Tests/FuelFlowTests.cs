using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The fuel receipt session (PORTPLAN item 37): EVO's own pump moves the
/// tank, so the poll prices the rise, banks it with the attendant's friendship
/// and opens the page — never billing a session change or the introduction.</summary>
public sealed class FuelFlowTests
{
    private static OnboardingState Done => OnboardingState.Fresh with { BirthdayDone = true };

    private static FuelRules Rules => FuelRules.Default;

    private static DriverWallet Wallet(double cash) => new(new DriverState(cash, 0.0));

    // One fill through the live loop: prime, a rising sample, then a still one
    // far enough past the settle window to close it.
    private static void Fill(MainViewModel vm, FakeTelemetry tel, FakeClock clock, float from, float to)
    {
        tel.Next = tel.Next with { FuelLitres = from, PacketId = 1 };
        vm.Poll();
        clock.Advance(TimeSpan.FromSeconds(2));
        tel.Next = tel.Next with { FuelLitres = to, PacketId = 2 };
        vm.Poll();
        clock.Advance(TimeSpan.FromSeconds(2));
        vm.Poll();
    }

    [Fact]
    public void ARefuel_ChargesThePrice_AndRaisesTheAttendantsFriendship()
    {
        var wallet = Wallet(100.0);
        var friends = new FakeFriendshipStore();
        var prices = new FakeFuelPriceStore();
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet, friendships: friends,
            fuelRules: Rules, fuelPrices: prices);
        double before = wallet.Cash; // the session-start login bonus lands first

        Fill(session.Vm, session.Tel, session.Clock, from: 20f, to: 55f);

        var page = Assert.IsType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(35.0, page.Receipt.Litres, 6);
        Assert.InRange(
            page.Receipt.PricePerLitre, Rules.MinPricePerLitre, Rules.MaxPricePerLitre);
        Assert.Equal(before - page.Receipt.Total, wallet.Cash, 6);
        Assert.Equal(1, friends.GetLevel(Rules.OwnerName));
        Assert.Equal(1, page.Receipt.Friendship!.Level);
    }

    [Fact]
    public void ContinuingTheReceipt_HandsBackThePageItInterrupted()
    {
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: Wallet(100.0), fuelRules: Rules);

        Fill(session.Vm, session.Tel, session.Clock, from: 20f, to: 55f);
        var page = Assert.IsType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);

        page.ContinueCommand.Execute(null);

        Assert.IsType<MenuScreenViewModel>(session.Vm.CurrentScreen);
    }

    [Fact]
    public void AShortWallet_LeavesTheFullBillAsDebt_TheFuelIsAlreadyInTheTank()
    {
        var wallet = Wallet(5.0);
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet, fuelRules: Rules);
        double before = wallet.Cash;

        Fill(session.Vm, session.Tel, session.Clock, from: 5f, to: 45f);

        var page = Assert.IsType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(40.0, page.Receipt.Litres, 6);
        Assert.Equal(before - page.Receipt.Total, wallet.Cash, 6);
        Assert.True(wallet.Cash < 0.0);
    }

    [Fact]
    public void AFreeTankRefillCard_CoversTheBill_AndIsSpent()
    {
        var trunk = new Trunk([new TrunkItem("FREE TANK REFILL", 0.0) { Quantity = 1 }]);
        var wallet = Wallet(100.0);
        var friends = new FakeFriendshipStore();
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet, friendships: friends,
            trunk: trunk, fuelRules: Rules);
        double before = wallet.Cash;

        Fill(session.Vm, session.Tel, session.Clock, from: 20f, to: 55f);

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
        var wallet = Wallet(100.0);
        var session = MainViewModelTests.Create(speedKmh: 0f, wallet: wallet, fuelRules: Rules);

        Fill(session.Vm, session.Tel, session.Clock, from: 20f, to: 55f);

        Assert.IsNotType<FuelReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(100.0, wallet.Cash, 6);
    }

    [Fact]
    public void ANewSessionThatStartsFull_IsNotBilled()
    {
        var wallet = Wallet(100.0);
        var session = MainViewModelTests.Create(
            speedKmh: 0f, progress: Done, wallet: wallet, fuelRules: Rules);
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
