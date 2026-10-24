using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The delivery run on its own (PORTPLAN 19): the trip-start roll, the cart
/// order the stops are pinned in, the first store leg's meter reset, the
/// unreachable-door skip and the settle after the last stop. Every effect the
/// shell owns (page, panel, HUD, receipt) is asserted through the fake host.
/// </summary>
public sealed class DeliveryFlowTests
{
    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16, Price: 12000.0, EngineCc: 2000);

    private static readonly DeliveryStore Post = new(
        "adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries",
        new AddressLocation("Street 2", 5, HouseSide.Left));

    private static readonly DeliveryStore Bakery = new(
        "adenau-bakery", "Adenau Bakery", "Fuchs", 8, 20, 2.20, "groceries",
        new AddressLocation("Street 3", 7, HouseSide.Right));

    private static DeliveryCatalog Catalog => new([Post, Bakery], 0.7, 100);

    private static HubCatalog Hubs => new(
        [new HubTier(1, true, [
            new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])],
        CredPRamp.UnityDefault, ["Elena Marchetti"]);

    private static DeliveryCareer OwnedGroceryCareer()
    {
        var career = new DeliveryCareer(Hubs, startCredP: 100);
        Assert.True(career.TryOpenCareer(1, 0, 0));
        return career;
    }

    private static (DeliveryFlow Flow, FakeDeliveryHost Host, CityEconomy City) Build(
        DeliveryCareer career, Garage? garage = null, DriverWallet? wallet = null,
        FakeFriendshipStore? friends = null, FakeOnboardingStore? onboarding = null)
    {
        DeliveryCatalog catalog = Catalog;
        var city = new CityEconomy(catalog.Stores, CityRules.Default);
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        Garage garageInstance = garage ?? new Garage([]);
        var walletInstance = wallet ?? new DriverWallet();
        var breakdowns = new TripBreakdowns(
            new BreakdownStrategy(MaintenanceRules.Default, seed: 0), garageInstance,
            onboarding ?? new FakeOnboardingStore(), walletInstance, new FakeRivals(),
            TestSaveState.Saver(clock, walletInstance, garageInstance), beat, new NullLogger());
        var flow = new DeliveryFlow(
            new DeliveryBoard(catalog), career, city, friends ?? new FakeFriendshipStore(),
            new TaxiDriveMeter(TaxiRules.Default.MaxMeterSpeedKmh), breakdowns, beat, new NullLogger());
        var host = new FakeDeliveryHost();
        flow.Attach(host);
        return (flow, host, city);
    }

    private static DeliveryRun Cart(params DeliveryStore[] stores) =>
        new([.. stores.Select(store => store.Key)], PaidForGoods: 28.0, ReceivedForGoods: 63.0);

    [Fact]
    public void Start_PinsTheHubPickupThenEveryStoreInCartOrder_AndSettlesTheLastStop()
    {
        var (flow, host, city) = Build(OwnedGroceryCareer());

        flow.Start(Cart(Post, Bakery));

        Assert.True(flow.InProgress);
        Assert.Equal(1, host.Hides); // the companion drives, the panel steps aside
        Assert.Equal(DeliveryStopKind.Hub, Assert.Single(host.Pinned).Kind);

        flow.Deliver(); // the freight is loaded; the first store leg starts
        Assert.Equal(Post.Key, host.Pinned[1].Key);
        flow.Deliver();
        Assert.Equal(Bakery.Key, host.Pinned[2].Key);
        flow.Deliver(); // last store: nothing left to pin

        DeliveryRun settled = Assert.Single(host.Settled);
        Assert.Equal([Post.Key, Bakery.Key], settled.StoreKeys);
        Assert.False(flow.InProgress);
        Assert.Equal(Post.UnitCapacity, city.Live(Post).StockedUnits);
    }

    [Fact]
    public void Deliver_StoreWithoutADoor_IsSkippedAndTheRunStillSettles()
    {
        var (flow, host, _) = Build(OwnedGroceryCareer());
        host.Doorless.Add(Bakery.Key);

        flow.Start(Cart(Post, Bakery));
        flow.Deliver(); // hub
        flow.Deliver(); // the post
        flow.Deliver(); // the bakery has no door: skipped, so the run settles here

        Assert.DoesNotContain(host.Pinned, leg => leg.Key == Bakery.Key);
        Assert.Single(host.Settled);
        Assert.False(flow.InProgress);
    }

    [Fact]
    public void Start_WhileARunIsLive_IsIgnored()
    {
        var (flow, host, _) = Build(OwnedGroceryCareer());

        flow.Start(Cart(Post));
        flow.Start(Cart(Post, Bakery));

        Assert.Equal(1, host.Hides);
        Assert.Single(host.Pinned);
        Assert.Empty(host.Settled);
    }

    [Fact]
    public void Start_WithATriggeredBreakdown_ChargesTheTowAndLosesTheOwnersFamiliarity()
    {
        // A worn car on a 100 % ceiling: the delivery trip start is cut before
        // the cargo moves (Unity DeliveryJobHandler's BeginTripSession), so no
        // stop is pinned and nothing settles.
        MaintenanceRules rules = MaintenanceRules.Default with
        {
            Parts = [new MaintenancePart("tires", 1000.0, 900.0)],
            MaxBreakdownChance = 1.0,
        };
        var garage = new Garage([Car], maintenanceRules: rules);
        garage.Add(Car, week: 1);
        garage.Current!.Drive(1000.0);
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        var friends = new FakeFriendshipStore();
        var onboarding = new FakeOnboardingStore();
        onboarding.Save(OnboardingState.Fresh with { BirthdayDone = true });
        var (flow, host, _) = Build(
            OwnedGroceryCareer(), garage, wallet, friends, onboarding);

        flow.Start(Cart(Post));

        Assert.False(flow.InProgress);
        Assert.Empty(host.Pinned);
        Assert.Empty(host.Settled);
        Assert.IsType<BreakdownScreenViewModel>(Assert.Single(host.Revealed));
        Assert.Equal(900.0, wallet.Cash, 6); // the mechanic's tow-in fee
        var (owner, delta) = Assert.Single(friends.Deltas);
        Assert.Equal("Elena Marchetti", owner);
        Assert.Equal(-1, delta);
    }

    [Fact]
    public void Stop_DropsTheRunAndTheCompanionLine_ButKeepsTheBoard()
    {
        var (flow, host, _) = Build(OwnedGroceryCareer());
        object board = flow.OpenBoard();
        flow.Start(Cart(Post));

        flow.Stop();

        Assert.False(flow.InProgress);
        Assert.Equal(1, host.Clears);
        Assert.Same(board, flow.Board); // CONTINUE still lands on the staged board
    }
}
