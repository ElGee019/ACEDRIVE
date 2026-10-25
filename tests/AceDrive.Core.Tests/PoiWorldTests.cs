using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The POI world on its own: the business growth behind the introduction gate,
/// the shop doors the board and the town adopt, the hub pickup doors, the
/// delivery stop lookup and the arrival handlers. Every shell effect is
/// asserted through the fake host.
/// </summary>
public sealed class PoiWorldTests
{
    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16, Price: 12000.0, EngineCc: 2000);

    private static readonly DeliveryStore Post = new(
        "adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries",
        new AddressLocation("Street 2", 5, HouseSide.Left));

    private static readonly DeliveryCatalog BoardCatalog =
        new([Post], 0.7, 100);

    private static HubCatalog Hubs => new(
        [new HubTier(1, true, [
            new HubCareer("groceries", "GROCERIES",
                [new HubLevel("Grocers and Sons", 100)])])],
        CredPRamp.UnityDefault, ["Elena Marchetti"]);

    private static OnboardingState Done =>
        OnboardingState.Fresh with { BirthdayDone = true };

    private sealed record Fixture(
        PoiWorld World, FakePoiWorldHost Host, PoiDirectory Directory,
        FakePoiDirectoryStore Store, DeliveryBoard Board, CityEconomy City,
        DriverWallet Wallet, Garage Garage, IReadOnlyList<CarSpec> Cars);

    private static Fixture Build(
        OnboardingState? progress = null,
        DeliveryCareer? career = null,
        PoiDirectory? directory = null,
        DriverState? saved = null,
        StoreCatalog? stores = null,
        int week = 1)
    {
        var clock = new FakeClock();
        var wallet = new DriverWallet();
        IReadOnlyList<CarSpec> cars = [Car];
        var garage = new Garage(cars);
        var beat = new VoiceBeat(clock);
        var logger = new NullLogger();
        var saver = TestSaveState.Saver(clock, wallet, garage);
        var shops = new ShopInteriors(
            wallet, garage, new GameClock(clock, clock.UtcNow), InsuranceRules.Default,
            cars, saver, new VisitLedger(), beat, logger);
        var onboarding = new FakeOnboardingStore();
        if (progress is not null)
        {
            onboarding.Save(progress);
        }
        var zone = new OperatingZone(saved, seed: 0);
        var jobs = new JobLedger(saved);
        var board = new DeliveryBoard(BoardCatalog);
        var deliveryCareer = career ?? new DeliveryCareer(Hubs, BoardCatalog.StartCredP);
        var city = new CityEconomy(BoardCatalog.Stores, CityRules.Default);
        PoiDirectory doors = directory ?? new PoiDirectory();
        var store = new FakePoiDirectoryStore();
        var day = new DayCycle(
            new GameClock(clock, clock.UtcNow), new TaxiShift(), new ShiftRecord(null),
            new FakeShiftStandings(), new FakePlayerTelemetry(),
            new FakeProfileStore(), new FakeRivals(), saver, beat, week, logger);
        var world = new PoiWorld(
            new PoiSpawner(
                AddressMap.Default, PoiCatalog.Default,
                new ProceduralNameGenerator(NamePoolTable.Default, seed: 0), seed: 0),
            doors, store, board, deliveryCareer, city, stores ?? StoreCatalog.Default,
            zone, jobs, onboarding, shops, day, logger);
        var host = new FakePoiWorldHost();
        world.Attach(host);
        return new Fixture(world, host, doors, store, board, city, wallet, garage, cars);
    }

    [Fact]
    public void Grow_BeforeTheIntroduction_OpensNothing()
    {
        // Unity's firstSceneComplete gate: a fresh career has story doors only,
        // even when the saved counters would already unlock a business.
        var saved = new DriverState(
            0, 0, StreetIds: ["home", "s1", "s2", "s3", "s4", "s5"], JobsCompleted: 10);
        var fixture = Build(saved: saved);

        fixture.World.Grow();

        Assert.Empty(fixture.Directory.All);
        Assert.Equal(0, fixture.Store.Saves);
        Assert.Equal(0, fixture.Host.Refreshes);
    }

    [Fact]
    public void Grow_AfterTheIntroduction_OpensTheUnlockedBusinesses()
    {
        var saved = new DriverState(
            0, 0, StreetIds: ["home", "s1", "s2", "s3", "s4", "s5"], JobsCompleted: 10);
        var fixture = Build(progress: Done, saved: saved);

        fixture.World.Grow();

        Assert.Contains(fixture.Directory.All, poi => poi.Type == PoiTypes.CarWash);
        Assert.Contains(fixture.Directory.All, poi => poi.Type == PoiTypes.Dealer);
        Assert.NotEmpty(fixture.Store.Saved);
        // The board page is refreshed once even with no shop door: a new
        // business door is still news for the cached addresses/board reads.
        Assert.Equal(1, fixture.Host.Refreshes);
    }

    [Fact]
    public void Grow_IsIdempotent_WhenTheDoorsAreBanked()
    {
        var saved = new DriverState(
            0, 0, StreetIds: ["home", "s1", "s2", "s3", "s4", "s5"], JobsCompleted: 10);
        var fixture = Build(progress: Done, saved: saved);

        fixture.World.Grow();
        int count = fixture.Directory.All.Count;
        fixture.World.Grow();

        Assert.Equal(count, fixture.Directory.All.Count);
    }

    [Fact]
    public void AdoptDoors_ConnectsARealShopToTheBoardAndTheTown()
    {
        var max = new PointOfInterest(
            PoiCatalog.MaxGroceryId, "Max's Grocery Store",
            new AddressLocation("Street 1", 9, HouseSide.Left),
            PoiTypes.MaxsGrocery, "Max Silvestri", "groceries", ["MAPLE SYRUP"]);
        var fixture = Build(progress: Done, directory: new PoiDirectory([max]));

        fixture.World.Grow();

        DeliveryStore row = Assert.IsType<DeliveryStore>(fixture.Board.Catalog.Find(max.Id));
        Assert.Equal(max.Id, row.Key);
        Assert.Equal(max.Address, row.Address);
        Assert.True(row.UnitCapacity > 0);
        Assert.Equal(row.UnitCapacity, fixture.City.Live(row).StockedUnits);
        Assert.Equal(1, fixture.Host.Refreshes);
    }

    [Fact]
    public void PlaceHubDoor_BanksThePickupDoorOnce()
    {
        var career = new DeliveryCareer(Hubs, startCredP: 100);
        Assert.True(career.TryOpenCareer(1, 0, 0));
        var fixture = Build(career: career);
        OwnedHub hub = Assert.Single(career.Owned);

        fixture.World.PlaceHubDoor(hub);
        fixture.World.PlaceHubDoor(hub);

        PointOfInterest door = Assert.Single(
            fixture.Directory.All, poi => poi.Type == PoiTypes.Hub);
        Assert.Equal(hub.Key, door.Id);
        Assert.Equal(hub.Name, door.Name);
        Assert.Equal(hub.OwnerName, door.OwnerName);
        Assert.Contains(door, fixture.Store.Saved);
        Assert.Equal(1, fixture.Store.Saves);
    }

    [Fact]
    public void StopDoor_ResolvesTheHubAndStoreDoors_AndNullWhenNeitherExists()
    {
        var career = new DeliveryCareer(Hubs, startCredP: 100);
        Assert.True(career.TryOpenCareer(1, 0, 0));
        var fixture = Build(career: career);
        fixture.World.PlaceHubDoor(career.Owned[0]);

        AddressLocation hubDoor = Assert.IsType<AddressLocation>(
            fixture.World.StopDoor(new DeliveryStop(DeliveryStopKind.Hub, career.Owned[0].Key, "Hub")));
        Assert.Contains(AddressMap.Default.Streets, street => street.Name == hubDoor.StreetName);
        AddressLocation storeDoor = Assert.IsType<AddressLocation>(
            fixture.World.StopDoor(new DeliveryStop(DeliveryStopKind.Store, Post.Key, Post.Name)));
        Assert.Equal(Post.Address, storeDoor);
        Assert.Null(fixture.World.StopDoor(
            new DeliveryStop(DeliveryStopKind.Store, "not-on-the-board", "Unknown")));
    }

    [Fact]
    public void Handlers_KeepTheSuppliedStoreHandler_AndBuildEveryInterior()
    {
        var supplied = new FakePoiArrivalHandler(PoiTypes.Grocery, "store-screen");
        var fixture = Build();
        var router = new PoiArrivalRouter(fixture.World.Handlers([supplied]));
        var poi = new PointOfInterest(
            "bank_0", "Test Bank", new AddressLocation("Street 1", 1, HouseSide.Left),
            PoiTypes.Bank, "Owner", "");

        Assert.Equal(
            "store-screen",
            router.Open(poi with { Type = PoiTypes.Grocery }, new RelayCommand(() => { }), () => { }));
        Assert.IsType<BankScreenViewModel>(
            router.Open(poi, new RelayCommand(() => { }), () => { }));
        Assert.IsType<CarWashScreenViewModel>(
            router.Open(poi with { Type = PoiTypes.CarWash }, new RelayCommand(() => { }), () => { }));
        Assert.IsType<MechanicScreenViewModel>(
            router.Open(poi with { Type = PoiTypes.Mechanic }, new RelayCommand(() => { }), () => { }));
        Assert.IsType<InsuranceScreenViewModel>(
            router.Open(poi with { Type = PoiTypes.Insurance }, new RelayCommand(() => { }), () => { }));
    }

    [Fact]
    public void DealerHandler_UsesTheDayCyclesWeekAndReportsThePurchase()
    {
        // The floor is drawn from the calendar week the day cycle owns and the
        // bought car is the shell's follow-up page, so the dealer handler can be
        // built before the coordinator exists.
        var fixture = Build(week: 3);
        var router = new PoiArrivalRouter(fixture.World.Handlers());
        var poi = new PointOfInterest(
            "dealer_0", "Test Dealer", new AddressLocation("Street 1", 2, HouseSide.Left),
            PoiTypes.Dealer, "Owner", "");

        var page = Assert.IsType<DealerScreenViewModel>(
            router.Open(poi, new RelayCommand(() => { }), () => { }));

        IReadOnlyList<CarSpec> floor = DealerStock.Build(
            fixture.Cars, fixture.Garage.Keys, 3);
        Assert.Equal(floor.Count, page.Rows.Count);
        Assert.Equal(floor[0].Key, page.Rows[0].Key);
    }
}
