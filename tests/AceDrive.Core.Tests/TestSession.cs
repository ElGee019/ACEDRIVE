using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// One headless session, composed the way <c>App.OnStartup</c> composes the
/// real one: the services, the four flows, the navigator, the panel presenter,
/// the introduction flow and the voice router are all named here and built
/// from fakes. The coordinator only ever receives finished collaborators, so
/// the flow suites share this graph instead of each keeping its own copy.
/// </summary>
internal static class TestSession
{
    // The smallest content that still has no walk pages in it; the story flow
    // degrades to the control page when nothing is authored.
    internal static readonly OnboardingContent EmptyContent = new(
        [], new Dictionary<string, string[]>(), [], [], [], [], []);
    // The birthday walk's minimum: one line, so the dialogue can be finished
    // without depending on Data/dialogue.json content. Max's post-intro
    // delivery offer rides along for the career hand-over tests.
    internal static readonly OnboardingContent OneLineDialogue = new(
        [], new Dictionary<string, string[]>(), [], [new DialogueLine("GRANDMA", "Go.")],
        [], [], [new DialogueLine("MAX", "Easy job, fast cash.")]);

    // The wake word a fresh session listens for until the watcher announces a
    // car; production primes it in MainViewModel.Start, which the tests skip.
    private const string FallbackBrand = "mazda";

    public static TestSessionResult Create(TestSessionOptions options)
    {
        // Deterministic locale: resources resolve to en, numbers format with '.'.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = options.Clock ?? new FakeClock();
        var tel = options.Physics ?? new FakeTelemetry();
        tel.Next = tel.Next with { SpeedKmh = options.SpeedKmh };
        var store = new FakeStore();
        var onboardingStore = options.Onboarding ?? new FakeOnboardingStore();
        if (options.Progress is not null)
        {
            onboardingStore.Save(options.Progress);
        }
        var board = options.Catalog ?? DeliveryCatalog.Default;
        var deliveryCareer = options.Career
            ?? new DeliveryCareer(options.Hubs ?? HubCatalog.Default, board.StartCredP);
        var deliveryBoard = new DeliveryBoard(board);
        CarIdentity? announced =
            options.CarKsId is null ? null : new CarIdentity(options.CarKsId, options.CarPreset);
        var brands = options.CarBrands ?? new FakeCarBrandSource { CurrentIdentity = announced };
        if (options.CarKsId is not null)
        {
            brands.CurrentIdentity = announced;
        }
        // The session's wake word is primed before anything reads it (the
        // production Start() does this one call later).
        _ = brands.Prime(FallbackBrand);
        var profiles = options.ProfileStore ?? new FakeProfileStore(options.Profile);
        AddressMap map = options.AddressMap ?? AddressMap.Default;
        var beat = new VoiceBeat(clock);
        var walletInstance = options.Wallet ?? new DriverWallet(options.SavedDriver);
        var pool = options.Cars ?? TestRoadCars.Pool;
        var garageInstance = options.Garage ?? new Garage(pool);
        var friendsInstance = options.Friendships ?? new FakeFriendshipStore();
        var trunkInstance = options.Trunk ?? new Trunk(options.SavedDriver?.Trunk);
        var storeCatalogInstance = options.StoreCatalog ?? StoreCatalog.Default;
        var storeNetwork = new StoreNetwork(storeCatalogInstance, PoiCatalog.Default);
        IReadOnlyList<IPoiArrivalHandler> handlers = options.PoiHandlers ??
        [
            new StorePoiHandler(
                storeNetwork, trunkInstance, walletInstance, garageInstance,
                friendsInstance, beat),
        ];
        PoiDirectory directory = options.PoiDirectory ?? new PoiDirectory();
        IPoiDirectoryStore directoryStore = options.PoiStore ?? new FakePoiDirectoryStore();
        if (options.PoiDirectory is null)
        {
            directory = new PoiDirectory(directoryStore.Load());
            // The real career opens these through the street/job thresholds;
            // the flow tests need the doors up front, so the test directory
            // carries one fixed door per generated family.
            SeedTestBusinesses(directory, map);
        }
        // One instance per service graph: the saver and the fleet must see the
        // same stores and state the VM does, so the inline `??` literals below
        // are hoisted and shared instead of being built twice.
        var game = new GameClock(clock, options.GameStart ?? clock.UtcNow);
        var shiftInstance = options.Shift ?? new TaxiShift();
        var shiftStoreInstance = options.ShiftStore ?? new FakeShiftStore();
        var contentSource = new FakeContentSource(options.Content ?? EmptyContent);
        var logger = options.Log ?? new NullLogger();
        var cityInstance = options.City ?? new CityEconomy(board.Stores, CityRules.Default);
        var cityStoreInstance = options.CityStore ?? new FakeCityStore();
        var roadsInstance = options.Roads ?? TestRoadCars.Ledger();
        var roadTestStoreInstance = options.RoadTestStore ?? new FakeRoadTestStore();
        var clockStoreInstance = options.Clocks ?? new FakeGameClockStore();
        var driverStoreInstance = options.DriverStore ?? new FakeDriverStore();
        var streakInstance = new DailyStreak(clock, options.SavedDriver);
        // Production seeds the interim zone length from the placeholder map
        // (App.OnStartup), so the starter bonus decays the same way.
        var zoneInstance = new OperatingZone(
            options.SavedDriver, seed: 0, AddressBook.MapLengthKm(map));
        var shiftsInstance = options.Shifts ?? new ShiftRecord(options.SavedDriver);
        var hungerInstance = options.Hunger ?? new Hunger(HungerRules.Default);
        var hungerStoreInstance = options.HungerStore ?? new FakeHungerStore();
        var garageStoreInstance = options.GarageStore ?? new FakeGarageStore();
        var jobsInstance = options.Jobs ?? new JobLedger(options.SavedDriver);
        var pinsInstance = options.Pins ?? new DestinationPins(options.SavedDriver);
        var visitsInstance = options.Visits ?? new VisitLedger(options.SavedDriver);
        var todoStoreInstance = options.TodoStore ?? new FakeTodoProgressStore();
        var saver = new GameStateSaver(
            clock, game, clockStoreInstance, driverStoreInstance, garageStoreInstance,
            cityStoreInstance, hungerStoreInstance, roadTestStoreInstance, shiftStoreInstance,
            walletInstance, streakInstance, zoneInstance, trunkInstance, garageInstance,
            cityInstance, hungerInstance, roadsInstance, shiftInstance, shiftsInstance,
            jobsInstance, pinsInstance, visitsInstance);
        var fleet = new FleetService(
            garageInstance, pool, game, walletInstance, InsuranceRules.Default, logger,
            brands, contentSource);
        var faresInstance = FareTable.Default;
        var taxiRulesInstance = options.TaxiRules ?? TaxiRules.Default;
        var deliveryFaresInstance = options.DeliveryFares ?? DeliveryFareTable.Default;
        var rarityInstance = options.Rarity ?? new FakeCarRarity();
        var rivalsInstance = options.Rivals ?? new FakeRivals();
        var brandMileageInstance = options.BrandMileage ?? new FakeBrandMileageStore();
        var telemetryInstance = options.Telemetry ?? new FakePlayerTelemetry();
        var meter = new TaxiDriveMeter(taxiRulesInstance.MaxMeterSpeedKmh);
        var breakdownsInstance = new TripBreakdowns(
            options.Breakdowns ?? new BreakdownStrategy(MaintenanceRules.Default, seed: 0),
            garageInstance, onboardingStore, walletInstance, rivalsInstance, saver, beat, logger);
        var settlement = new JobSettlement(
            walletInstance, garageInstance, shiftInstance, zoneInstance, jobsInstance,
            streakInstance, clock, fleet, rarityInstance, friendsInstance,
            brandMileageInstance, telemetryInstance, rivalsInstance, deliveryBoard,
            deliveryCareer, cityInstance, deliveryFaresInstance, meter,
            faresInstance, taxiRulesInstance, beat, options.Hud, logger);
        var shops = new ShopInteriors(
            walletInstance, garageInstance, game, InsuranceRules.Default, pool, saver,
            visitsInstance, beat, logger);
        var spawnerInstance = options.PoiSpawner ?? new PoiSpawner(
            map, PoiCatalog.Default,
            new ProceduralNameGenerator(NamePoolTable.Default, seed: 0), seed: 0);
        // The session calendar and the readout formatter: the week the pages
        // stamp, the shift timeout that opens its page and the painted strings.
        // One board instance for the cycle and the assertions, like the stores.
        IShiftStandings shiftBoard = options.ShiftStandings ?? new FakeShiftStandings();
        var dayCycle = new DayCycle(
            game, shiftInstance, shiftsInstance, shiftBoard, telemetryInstance,
            profiles, rivalsInstance, saver, beat, options.StartWeek, logger);
        var readouts = new TelemetryReadouts();
        // The session-start catch-up: the starter car and today's login bonus,
        // wired to the same stores and wallet the assertions read. Production
        // seeds the streak's skill rolls here, so a test can pin them.
        var catchUp = new DriverCatchUp(
            onboardingStore, garageInstance, streakInstance, walletInstance,
            options.Rng ?? new Random(0), saver, rivalsInstance, logger);
        // The zone growth the settle hook triggers: the same zone instance the
        // saver, the settle and the POI world hold.
        var zoneGrowth = new ZoneGrowth(
            zoneInstance, map, onboardingStore, profiles, telemetryInstance, logger);
        // The game-clock world: the same instances the saver and the fleet
        // hold, so an insurance charge lands in the driver file the tests read.
        var world = new WorldTicks(
            game, cityInstance, hungerInstance, fleet, deliveryBoard, deliveryCareer,
            saver, rivalsInstance, logger);
        var poiWorld = new PoiWorld(
            spawnerInstance, directory, directoryStore, deliveryBoard, deliveryCareer,
            cityInstance, storeCatalogInstance, zoneInstance, jobsInstance,
            onboardingStore, shops, dayCycle, logger);
        var poiArrivals = new PoiArrivalRouter(poiWorld.Handlers(handlers));
        // The ToDo list: its own ledger + file, synced on the cold edges the
        // tests drive (settle, POI entry, pin, purchase, boot).
        var todoBoard = new TodoBoard(
            options.Todos ?? TodoTable.Default, todoStoreInstance, directory,
            onboardingStore, walletInstance, jobsInstance, zoneInstance, garageInstance,
            pinsInstance, visitsInstance, deliveryCareer, logger);
        var proposalsInstance = TaxiTestData.Proposer(map: map, seed: 0);
        var taxi = new TaxiJobFlow(
            proposalsInstance, map, brands, pool, options.Requests,
            options.RequestTable ?? CustomerRequestTable.Empty, options.Police,
            taxiRulesInstance, faresInstance, garageInstance, walletInstance,
            friendsInstance, fleet, game, clock, beat, meter, saver, rivalsInstance,
            breakdownsInstance, options.Hud, logger);
        var delivery = new DeliveryFlow(
            deliveryBoard, deliveryCareer, cityInstance, friendsInstance, meter,
            breakdownsInstance, beat, logger);
        // The haul settle: the same watcher, pool and wallet the assertions
        // read, and the calendar whose week its checkpoint banks.
        var haul = new CarHaulFlow(
            brands, pool, walletInstance, dayCycle, saver, beat, logger);
        // The page graph shares the one shell the VM binds: the tests assert
        // vm.CurrentScreen, so both must report the same panel.
        var shell = new OverlayShell();
        var navigation = new Navigation(
            shell, beat, game, clock, shiftInstance, todoBoard, roadsInstance,
            saver, walletInstance, garageInstance, rivalsInstance, trunkInstance,
            hungerInstance, storeCatalogInstance, directory, options.Hud, deliveryBoard,
            deliveryCareer, cityInstance, taxi, delivery, haul, poiWorld, dayCycle,
            zoneInstance, [], () => brands.Brand, logger);
        // The panel policy and the two hosts the coordinator used to assemble:
        // the production root builds them in this same order.
        var panel = new PanelPresenter(shell, navigation, taxi, delivery, clock, logger);
        var story = new OnboardingFlow(
            onboardingStore, contentSource, profiles,
            options.PlayerNames ?? new OfflinePlayerNameRegistry(), map,
            options.DetectedName, options.Pois ?? new PoiGenerator(map, PoiCatalog.Default, seed: 0),
            directory, directoryStore, poiArrivals, trunkInstance,
            deliveryCareer, board.StartCredP,
            options.PreferencesStore ?? new FakePreferencesStore(),
            options.Preferences ?? PlayerPreferences.Default,
            options.Culture ?? new FakeCultureService(), beat, options.Hud, garageInstance,
            () => dayCycle.DisplayWeek, saver.SaveGarage, () => brands.Brand,
            logger, new StoryHost(panel, navigation, saver, rivalsInstance, todoBoard));
        var voiceRouter = new VoiceRouter(
            panel, brands, contentSource, clock, logger, options.Hud);
        var vm = new MainViewModel(
            tel, clock, world, shiftInstance, store, logger,
            options.Voice ?? new FakeVoiceListener(available: false), brands, map,
            taxiRulesInstance,
            beat, walletInstance,
            rivalsInstance, garageInstance,
            catchUp, zoneGrowth,
            saver, fleet, settlement, shell, panel, story, voiceRouter,
            taxi, delivery, poiWorld, navigation, readouts, dayCycle,
            todoBoard, pinsInstance,
            options.Hud, options.SaveResets, options.Restarter);
        if (options.CarKsId is not null)
        {
            // The watcher thread lands via the dispatcher, which runs inline
            // headless: the VM now follows the announced car like live.
            brands.Fire("test", announced);
        }
        return new TestSessionResult(vm, clock, tel, store, story, todoBoard);
    }

    // The real career opens these through the street/job thresholds; the flow
    // tests need the doors up front, so the test directory carries one fixed
    // door per generated family (same display names the Unity rows use).
    private static void SeedTestBusinesses(PoiDirectory directory, AddressMap map)
    {
        int house = 1;
        foreach (PoiDefinition definition in PoiCatalog.Default)
        {
            if (definition.Spawn is null)
            {
                continue;
            }

            int number = Math.Min(house++, HouseNumberPlan.Count(map.Streets[0], map));
            directory.Add(new PointOfInterest(
                definition.Id, definition.Name,
                new AddressLocation(
                    map.Streets[0].Name, number, HouseNumberPlan.SideOf(number)),
                definition.Type));
        }
    }
}
