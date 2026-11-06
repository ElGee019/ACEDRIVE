using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The delivery run on its own (PORTPLAN 19): the trip-start roll, the cart
/// order the stops are pinned in, the route priced at DELIVER, the
/// unreachable-door skip and the settle after the last stop. Every effect the
/// shell owns (page, panel, HUD, receipt) is asserted through the fake host.
/// </summary>
public sealed class DeliveryFlowTests
{
    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16, Price: 12000.0, EngineCc: 2000);

    // Ten-metre houses on a 1 km lap: hub 10 m, post 290 m, bakery 560 m, so
    // the priced route hub -> post -> bakery is 280 + 270 = 550 m.
    private static readonly AddressMap RouteMap = new(
        "Germany", "Brands", 1000.0, 10.0,
        [
            new AddressStreet("street-1", "Street 1", 0.0, 0.25),
            new AddressStreet("street-2", "Street 2", 0.25, 0.5),
            new AddressStreet("street-3", "Street 3", 0.5, 0.75),
        ]);

    private static readonly AddressLocation HubDoor =
        new("Street 1", 2, HouseSide.Left);

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
        CredPRamp.UnityDefault);

    private static DeliveryCareer OwnedGroceryCareer()
    {
        var career = new DeliveryCareer(Hubs, startCredP: 100, TestNames.For());
        Assert.True(career.TryOpenCareer(1, 0, 0));
        return career;
    }

    private static (DeliveryFlow Flow, FakeDeliveryHost Host, CityEconomy City) Build(
        DeliveryCareer career, Garage? garage = null, DriverWallet? wallet = null,
        FakeFriendshipStore? friends = null, FakeOnboardingStore? onboarding = null,
        Trunk? trunk = null)
    {
        DeliveryCatalog catalog = Catalog;
        var city = new CityEconomy(catalog.Stores, CityRules.Default);
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        Garage garageInstance = garage ?? new Garage([]);
        var walletInstance = wallet ?? new DriverWallet();
        var carrying = trunk ?? new Trunk();
        var breakdowns = new TripBreakdowns(
            new BreakdownStrategy(MaintenanceRules.Default, seed: 0), garageInstance,
            onboarding ?? new FakeOnboardingStore(), walletInstance, new FakeRivals(),
            TestSaveState.Saver(clock, walletInstance, garageInstance), beat, new NullLogger());
        var host = new FakeDeliveryHost();
        // The shell resolves a stop's door; a doorless stop answers null, so it
        // is neither pinned nor priced.
        AddressLocation? Door(DeliveryStop stop) =>
            stop.Kind == DeliveryStopKind.Hub
                ? HubDoor
                : host.Doorless.Contains(stop.Key)
                    ? null
                    : catalog.Find(stop.Key)?.Address;
        var flow = new DeliveryFlow(
            new DeliveryBoard(catalog), career, city, friends ?? new FakeFriendshipStore(),
            walletInstance, new Glovebox(RewardTable.Default, walletInstance), carrying,
            () => 1000.0, RouteMap, Door,
            new JobMileageMeter(() => garageInstance.Current, walletInstance, new Random(1)),
            breakdowns, clock, beat,
            new NullLogger());
        flow.Attach(host);
        return (flow, host, city);
    }

    private static DeliveryRun Cart(params DeliveryStore[] stores)
    {
        DeliveryCatalog catalog = Catalog;
        List<DeliveryOrder> orders = [];
        foreach (DeliveryStore store in stores)
        {
            orders.Add(catalog.Order(store, store.UnitsNeeded));
        }
        return new DeliveryRun(orders);
    }

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
        Assert.Equal([Post.Key, Bakery.Key], settled.Orders.Select(order => order.StoreKey));
        Assert.False(flow.InProgress);
        Assert.Equal(Post.UnitCapacity, city.Live(Post).StockedUnits);
        Assert.Equal(Bakery.UnitCapacity, city.Live(Bakery).StockedUnits);
    }

    [Fact]
    public void Start_PricesThePlannedRoute_SoTheDriveCannotMoveTheCharge()
    {
        var (flow, host, _) = Build(OwnedGroceryCareer());

        flow.Start(Cart(Post, Bakery));
        flow.Deliver(); // hub
        flow.Deliver(); // the post
        flow.Deliver(); // the bakery: the last stop settles

        // Hub door 10 m -> post 290 m -> bakery 560 m: the planned legs only,
        // the approach to the hub stays uncharged.
        Assert.Equal(0.55, Assert.Single(host.Settled).DistanceKm, 3);
    }

    [Fact]
    public void Start_WithADoorlessStop_PricesOnlyTheDoorsItCanResolve()
    {
        var (flow, host, _) = Build(OwnedGroceryCareer());
        host.Doorless.Add(Bakery.Key);

        flow.Start(Cart(Post, Bakery));
        flow.Deliver(); // hub
        flow.Deliver(); // the post
        flow.Deliver(); // the bakery is skipped and settles here

        // The bakery contributes no leg: hub 10 m -> post 290 m only.
        Assert.Equal(0.28, Assert.Single(host.Settled).DistanceKm, 3);
    }


    [Fact]
    public void Start_ChargesTheWholesaleSumBeforeTheTripRisk()
    {
        var wallet = new DriverWallet(new DriverState(100.0, 0.0));
        var (flow, _, _) = Build(OwnedGroceryCareer(), wallet: wallet);

        Assert.True(flow.Start(Cart(Post)));

        Assert.Equal(51.0, wallet.Cash, 6); // 100 − 2.50 × 0.7 × 28
    }

    [Fact]
    public void Start_WithCashBelowTheGoods_IsRefusedAndKeepsTheCash()
    {
        var wallet = new DriverWallet(new DriverState(10.0, 0.0));
        var (flow, host, _) = Build(OwnedGroceryCareer(), wallet: wallet);

        Assert.False(flow.Start(Cart(Post)));

        Assert.Equal(10.0, wallet.Cash, 6);
        Assert.False(flow.InProgress);
        Assert.Empty(host.Pinned);
        Assert.Empty(host.Settled);
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
        var career = OwnedGroceryCareer();
        var (flow, host, _) = Build(
            career, garage, wallet, friends, onboarding);

        flow.Start(Cart(Post));

        Assert.False(flow.InProgress);
        Assert.Empty(host.Pinned);
        Assert.Empty(host.Settled);
        Assert.IsType<BreakdownScreenViewModel>(Assert.Single(host.Revealed));
        // 1 000 CR − the 49.00 goods charge − the 100 CR tow fee. No shield
        // is carried, so the lost cargo stays paid for.
        Assert.Equal(851.0, wallet.Cash, 6);
        var (owner, delta) = Assert.Single(friends.Deltas);
        Assert.Equal(career.OwnedFor("groceries")!.OwnerName, owner);
        Assert.Equal(-1, delta);
    }

    [Fact]
    public void Start_WithATriggeredBreakdown_SpendsTheInsuranceShield_ButNotTheAccidentShield()
    {
        // A delivery cut at the trip start is a failed delivery, not an
        // accident: the invested goods come back (Delivery Insurance Shield)
        // while the friendship penalty and the carried Accident Loyalty Shield
        // both stay (Unity's BreakdownFlow consumes the insurance card only).
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
        var trunk = new Trunk();
        var glovebox = new Glovebox(RewardTable.Default, wallet);
        glovebox.Claim(RewardTable.Default.Find(RewardCardEffect.AccidentLoyalty)!, trunk);
        glovebox.Claim(RewardTable.Default.Find(RewardCardEffect.DeliveryInsurance)!, trunk);
        var career = OwnedGroceryCareer();
        var (flow, host, _) = Build(career, garage, wallet, friends, onboarding, trunk);

        flow.Start(Cart(Post));

        var (owner, delta) = Assert.Single(friends.Deltas);
        Assert.Equal(career.OwnedFor("groceries")!.OwnerName, owner);
        Assert.Equal(-1, delta);
        // 1 000 CR − the 49.00 goods charge − the 100 CR tow fee + the same
        // 49.00 back from the insurance card: the refund now refunds a real
        // debit instead of minting cash.
        Assert.Equal(900.0, wallet.Cash, 6);
        var page = Assert.IsType<BreakdownScreenViewModel>(Assert.Single(host.Revealed));
        Assert.True(page.NoteVisible);
        Assert.Contains("Delivery Insurance Shield", page.NoteLine, StringComparison.Ordinal);
        Assert.DoesNotContain("Accident Loyalty Shield", page.NoteLine, StringComparison.Ordinal);
        // The insurance card is spent; the accident shield is still carried.
        TrunkItem card = Assert.Single(trunk.Items);
        Assert.Equal("ACCIDENT LOYALTY SHIELD", card.Name);
    }

    [Fact]
    public void FailForAccident_DropsTheRunAndChargesTheHubOwner()
    {
        var friends = new FakeFriendshipStore();
        var career = OwnedGroceryCareer();
        var (flow, host, _) = Build(career, friends: friends);
        flow.Start(Cart(Post, Bakery));

        Assert.Equal(67.48, flow.FailForAccident());

        Assert.False(flow.InProgress);
        Assert.Equal(1, host.Clears);
        Assert.Empty(host.Settled); // the lost cargo is never priced
        var (owner, delta) = Assert.Single(friends.Deltas);
        Assert.Equal(career.OwnedFor("groceries")!.OwnerName, owner);
        Assert.Equal(-1, delta);
    }

    [Fact]
    public void FailForAccident_OutsideARun_DoesNothing()
    {
        var (flow, host, _) = Build(OwnedGroceryCareer());

        Assert.Null(flow.FailForAccident());

        Assert.Equal(0, host.Clears);
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
        Assert.Same(board, flow.Board); // the staged board survives the dropped run
    }
}
