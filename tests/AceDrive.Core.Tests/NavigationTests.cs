using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The page graph runs headless: which pages are cached and which are rebuilt,
/// the road-test day's bank, the display week's report and the bought-car
/// choice. The coordinator here is a fake, so each test sees only the
/// navigator's own decisions.
/// </summary>
public sealed class NavigationTests
{
    // The tier table the navigator hands the Cento Miglia page: content at
    // runtime (Data/cento_miglia.json), two rows here so the wiring is what
    // these tests read.
    private static readonly CentoTier[] CentoTiers =
    [
        new("gentlemen_drivers", "Gentlemen Drivers Club", "Free for all.", 1),
        new("era_event", "{0}s Era Only", "Exclusive event.", 2),
    ];

    private static (Navigation Nav, OverlayShell Shell, FakeNavigationHost Host,
        FakeClock Clock, TaxiShift Shift, FakeShiftStore ShiftStore,
        RoadTestLedger Roads, FakeRoadTestStore RoadTests,
        Garage Garage, FakeGarageStore GarageStore, TestLog Log) Create(
        int startWeek = 1, IReadOnlyList<CarSpec>? cars = null, Garage? garage = null)
    {
        // Deterministic locale: resources resolve to en, numbers format with '.'.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var game = new GameClock(clock, clock.UtcNow);
        var beat = new VoiceBeat(clock);
        var shell = new OverlayShell();
        var log = new TestLog();
        var pool = cars ?? TestRoadCars.Pool;
        var wallet = new DriverWallet();
        var garageInstance = garage ?? new Garage(pool);
        var garageStore = new FakeGarageStore();
        var trunk = new Trunk();
        var hunger = new Hunger(HungerRules.Default);
        var city = new CityEconomy([], CityRules.Default);
        var roads = new RoadTestLedger(pool, seed: 0);
        var roadTests = new FakeRoadTestStore();
        var shift = new TaxiShift();
        var shiftStore = new FakeShiftStore();
        var rivals = new FakeRivals();
        var friends = new FakeFriendshipStore();
        var onboarding = new FakeOnboardingStore();
        // The zone the Cento Miglia gates read: no street discovered yet, so
        // the event opens with every club locked (Unity's fresh-driver state).
        // Its length is the placeholder map's, exactly as the root seeds it.
        var zoneInstance = new OperatingZone(
            null, seed: 0, AddressBook.MapLengthKm(AddressMap.Default));
        // One instance per service graph: the saver writes the same stores the
        // assertions read.
        var willyLedger = new WhereIsWillyLedger();
        var saver = new GameStateSaver(
            clock, game, new FakeGameClockStore(), new FakeDriverStore(), garageStore,
            new FakeCityStore(), new FakeHungerStore(), roadTests, shiftStore,
            wallet, new DailyStreak(clock, null), zoneInstance,
            trunk, garageInstance, city, hunger, roads, shift, new ShiftRecord(null),
            new JobLedger(), new DestinationPins(), new VisitLedger(),
            new ValuationLedger(), willyLedger);
        var board = new DeliveryBoard(DeliveryCatalog.Default);
        var career = new DeliveryCareer(HubCatalog.Default, 0);
        var directory = new PoiDirectory();
        IReadOnlyList<CarSpec> poolCars = pool;
        var day = new DayCycle(
            game, shift, new ShiftRecord(null), new FakeShiftStandings(),
            new FakePlayerTelemetry(), new FakeProfileStore(), rivals, saver, beat,
            startWeek, log);
        var world = new PoiWorld(
            new PoiSpawner(
                AddressMap.Default, PoiCatalog.Default,
                new ProceduralNameGenerator(NamePoolTable.Default, seed: 0), seed: 0),
            directory, new FakePoiDirectoryStore(), board, career, city,
            StoreCatalog.Default, new OperatingZone(null, seed: 0), new JobLedger(),
            onboarding,
            new ShopInteriors(
                wallet, garageInstance, game, InsuranceRules.Default, poolCars,
                saver, new VisitLedger(), beat, log),
            day, log);
        var meter = new TaxiDriveMeter(TaxiRules.Default.MaxMeterSpeedKmh);
        var breakdowns = new TripBreakdowns(
            new BreakdownStrategy(MaintenanceRules.Default, seed: 0),
            garageInstance, onboarding, wallet, rivals, saver, beat, log);
        var brands = new FakeCarBrandSource();
        var taxi = new TaxiJobFlow(
            TaxiTestData.Proposer(), AddressMap.Default, brands, poolCars, null,
            CustomerRequestTable.Empty, null, TaxiRules.Default, FareTable.Default,
            garageInstance, wallet, friends,
            new FleetService(
                garageInstance, poolCars, game, wallet, InsuranceRules.Default, log,
                brands, new FakeContentSource(TestSession.EmptyContent)),
            game, clock, beat, meter, saver, rivals, breakdowns, null, log);
        var delivery = new DeliveryFlow(board, career, city, friends, meter, breakdowns, beat, log);
        var cento = new CentoRaceFlow(wallet, garageInstance, beat, log);
        cento.Attach(new FakeCentoRaceHost());
        var haul = new CarHaulFlow(brands, poolCars, wallet, day, saver, beat, log);
        // The weekly market valuation's flow: the calendar's Monday tap starts
        // its drive leg, so the navigator needs the real flow (the list stays
        // empty offline, which is the page's own fallback path).
        var valuation = new ValuationFlow(
            AddressMap.Default, game, new ValuationLedger(), new OfflineValuationBoard(),
            garageInstance, poolCars, wallet, new FakeProfileStore(), null, saver,
            rivals, beat, new Random(0), log);
        // The ToDo list the jobs page reads: its own ledger over the same
        // state instances, with an in-memory file.
        var todoBoard = new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), directory, onboarding,
            wallet, new JobLedger(), new OperatingZone(null, seed: 0), garageInstance,
            new DestinationPins(), new VisitLedger(), career, log);
        // The weekly missing-person run: the calendar's Wednesday tap starts it,
        // so the navigator needs the real flow (its resident table is the one
        // the elder is drawn from).
        var willy = new WhereIsWillyFlow(
            AddressMap.Default, game,
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 0),
            directory,
            new ResidentBook(
                new FakeResidentStore(),
                new ProceduralNameGenerator(NamePoolTable.Default, seed: 0), log),
            garageInstance, poolCars, WhereIsWillyRules.Default, willyLedger, friends,
            trunk, wallet, null, saver, rivals, beat, () => day.DisplayWeek,
            new Random(0), log);
        var nav = new Navigation(
            shell, beat, game, clock, shift, todoBoard, roads, saver,
            wallet, garageInstance, rivals, trunk, hunger, StoreCatalog.Default,
            directory, null, board, career, city, taxi, delivery, haul, world, day,
            zoneInstance, CentoTiers, AddressMap.Default, () => (string?)null, cento,
            valuation, willy, willyLedger,
            () => "mazda", new Random(0), log);
        var host = new FakeNavigationHost();
        nav.Attach(host);
        // The proposal page asks the job's host for the player's door on every
        // draw; the page is the only part of the flow the navigator opens.
        taxi.Attach(new FakeTaxiJobHost
        {
            Home = new AddressLocation(
                AddressMap.Default.Streets[0].Name, 1,
                HouseNumberPlan.SideOf(1)),
        });
        return (nav, shell, host, clock, shift, shiftStore, roads, roadTests,
            garageInstance, garageStore, log);
    }

    [Fact]
    public void Menu_IsCachedButTheHomePageIsRebuiltOnEveryVisit()
    {
        var (nav, shell, _, _, _, _, _, _, _, _, _) = Create();

        nav.ShowMenuPage();
        var menu = Assert.IsType<MenuScreenViewModel>(shell.CurrentScreen);
        Assert.Same(menu, nav.MenuPage());

        menu.HomeCommand.Execute(null);
        var first = Assert.IsType<HomeScreenViewModel>(shell.CurrentScreen);
        first.BackCommand.Execute(null);
        Assert.Same(menu, shell.CurrentScreen);

        menu.HomeCommand.Execute(null);
        var second = Assert.IsType<HomeScreenViewModel>(shell.CurrentScreen);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void RoadTestDay_BanksThePickAndDropsTheCar()
    {
        var (nav, shell, _, clock, _, _, roads, roadTests, _, _, log) = Create();
        nav.ShowMenuPage();
        var menu = Assert.IsType<MenuScreenViewModel>(shell.CurrentScreen);
        menu.CalendarCommand.Execute(null);
        var calendar = Assert.IsType<CalendarScreenViewModel>(shell.CurrentScreen);
        // The strip opens on Monday; the road-test gate is Tuesday 8am-8pm, so
        // carry the game clock over the midnight and repaint like the poll does.
        clock.Advance(TimeSpan.FromMinutes(53));
        nav.Tick();
        int roadTestDay = ControlCalendar.Week
            .Single(day => day.EventId == ControlCalendar.RoadTest).DayOfWeek;
        Assert.Equal(3, roadTestDay);
        string first = RoadTestButton(calendar);

        calendar.DayCommand.Execute(roadTestDay);

        // The tested car is banked and the next pick takes its place on the
        // row (PORTPLAN 49: only a driven test may move the offer).
        RoadTestSnapshot saved = Assert.Single(roadTests.Saved);
        Assert.Equal(roads.Current?.Id, saved.CarId);
        Assert.NotEqual(first, RoadTestButton(calendar));
        Assert.Contains(log.Infos, line => line.StartsWith("road test done:", StringComparison.Ordinal));
    }

    [Fact]
    public void CentoDay_OpensTheEventPageAndBackReturnsToTheCalendar()
    {
        var (nav, shell, _, clock, _, _, _, _, _, _, log) = Create();
        nav.ShowMenuPage();
        var menu = Assert.IsType<MenuScreenViewModel>(shell.CurrentScreen);
        menu.CalendarCommand.Execute(null);
        var calendar = Assert.IsType<CalendarScreenViewModel>(shell.CurrentScreen);
        // The strip opens on Monday noon; advance six game days plus a minute in
        // the clock's own unit to reach Sunday noon — the event's day inside its
        // 8am-8pm window — then let the poll repaint the highlight.
        double gameMinutesToSunday = (6 * 24 * 60) + 1;
        clock.Advance(TimeSpan.FromSeconds(
            gameMinutesToSunday * GameClock.RealSecondsPerGameMinute));
        nav.Tick();
        int centoDay = ControlCalendar.Week
            .Single(day => day.EventId == ControlCalendar.CentoMiglia).DayOfWeek;
        Assert.Equal(1, centoDay);

        calendar.DayCommand.Execute(centoDay);

        // The tap opens the event page on the calendar's week; BACK lands on the
        // same cached calendar page it left (Unity's screen pops back to jobs).
        var page = Assert.IsType<CentoMigliaScreenViewModel>(shell.CurrentScreen);
        Assert.Equal("Week 1", page.WeekLine);
        Assert.Equal(CentoTiers.Length, page.Tiers.Count);
        page.BackCommand.Execute(null);
        Assert.Same(calendar, shell.CurrentScreen);
        Assert.Contains(
            log.Infos, line => line.StartsWith("cento miglia opened:", StringComparison.Ordinal));
    }

    [Fact]
    public void CentoEnter_BuildsTheEventAndReportsIt()
    {
        var (nav, shell, _, clock, _, _, _, _, _, _, log) = Create();
        nav.ShowMenuPage();
        Assert.IsType<MenuScreenViewModel>(shell.CurrentScreen).CalendarCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(
            ((6 * 24 * 60) + 1) * GameClock.RealSecondsPerGameMinute));
        nav.Tick();
        int centoDay = ControlCalendar.Week
            .Single(day => day.EventId == ControlCalendar.CentoMiglia).DayOfWeek;
        Assert.IsType<CalendarScreenViewModel>(shell.CurrentScreen)
            .DayCommand.Execute(centoDay);

        var page = Assert.IsType<CentoMigliaScreenViewModel>(shell.CurrentScreen);
        page.TierCommand.Execute(page.Tiers[0]);
        page.EnterCommand.Execute(null);
        Assert.True(page.IsBuilding);

        // The host's poll is what advances the build; the page itself only
        // formats the seconds it is handed.
        clock.Advance(TimeSpan.FromSeconds(CentoMiglia.BuildSeconds));
        page.Tick(clock.UtcNow);
        Assert.True(page.IsBuilt);
        // The built event now starts the race flow, which logs the entry it owns
        // (tier, registration and the zone's longest route on one street).
        Assert.Contains(
            log.Infos, line => line.StartsWith("cento miglia entry:", StringComparison.Ordinal)
                && line.Contains("registration #", StringComparison.Ordinal)
                && line.Contains("0.960 km", StringComparison.Ordinal));
    }

    [Fact]
    public void DisplayWeekChange_RepaintsTheBuiltPages()
    {
        var (nav, shell, _, _, _, _, _, _, _, _, _) = Create(startWeek: 1);
        nav.ShowMenuPage();
        var menu = Assert.IsType<MenuScreenViewModel>(shell.CurrentScreen);
        menu.CalendarCommand.Execute(null);
        var calendar = Assert.IsType<CalendarScreenViewModel>(shell.CurrentScreen);
        Assert.Contains("WEEK 1", calendar.DayWeekText, StringComparison.Ordinal);

        nav.DisplayWeekChanged(3);

        Assert.Contains("WEEK 3", calendar.DayWeekText, StringComparison.Ordinal);
        Assert.Contains("WEEK 3", menu.DayWeekText, StringComparison.Ordinal);
    }

    [Fact]
    public void Calendar_BuiltAfterARoll_StampsTheCurrentWeek()
    {
        // S5: the week lives in the day cycle, so a page built after a roll
        // reads it from the owner instead of a mirror the navigator kept.
        var (nav, shell, _, _, _, _, _, _, _, _, _) = Create(startWeek: 4);

        nav.ShowMenuPage();
        var menu = Assert.IsType<MenuScreenViewModel>(shell.CurrentScreen);
        menu.CalendarCommand.Execute(null);

        var calendar = Assert.IsType<CalendarScreenViewModel>(shell.CurrentScreen);
        Assert.Contains("WEEK 4", calendar.DayWeekText, StringComparison.Ordinal);
    }

    [Fact]
    public void TaxiShiftTap_StartsTheShiftBanksItAndOpensTheProposal()
    {
        var (nav, shell, host, _, shift, shiftStore, _, _, _, _, _) = Create();

        nav.TaxiShiftCommand.Execute(null);

        Assert.True(shift.IsStarted);
        Assert.Single(shiftStore.Saved);
        Assert.IsType<TaxiJobProposalViewModel>(shell.CurrentScreen);
        Assert.Equal(1, host.Readouts);
    }

    [Fact]
    public void TaxiShiftTap_OnATimedOutShift_HandsTheCloseToTheCoordinator()
    {
        var (nav, shell, host, clock, shift, _, _, _, _, _, _) = Create();
        nav.TaxiShiftCommand.Execute(null);
        clock.Advance(TimeSpan.FromMinutes(TaxiShift.TimeoutMinutes + 1));

        nav.TaxiShiftCommand.Execute(null);

        Assert.NotNull(host.Idle);
        Assert.True(host.Idle >= TimeSpan.FromMinutes(TaxiShift.TimeoutMinutes));
        Assert.False(shift.IsStarted);
        Assert.IsType<TaxiJobProposalViewModel>(shell.CurrentScreen);
    }

    [Fact]
    public void CarBought_SecondCar_OffersTheChoiceAndDrivesItOnRequest()
    {
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0].Key, week: 1);
        var (nav, shell, _, _, _, _, _, _, _, garageStore, _) = Create(garage: garage);
        CarSpec bought = TestRoadCars.Pool[1];
        garage.Add(bought.Key, week: 1);

        nav.CarBought(bought);

        var page = Assert.IsType<BoughtFirstCarScreenViewModel>(shell.CurrentScreen);
        Assert.NotEmpty(page.SummaryLine);
        Assert.Equal(1, garageStore.Saves);

        page.DriveCommand.Execute(null);

        Assert.Equal(bought.Key, garage.CurrentKey);
        Assert.IsType<MainControlScreenViewModel>(shell.CurrentScreen);
        Assert.Equal(2, garageStore.Saves);
    }

    [Fact]
    public void CarBought_FirstPurchase_HidesTheSecondCarSummary()
    {
        var garage = new Garage(TestRoadCars.Pool);
        var (nav, shell, _, _, _, _, _, _, _, _, _) = Create(garage: garage);
        CarSpec bought = TestRoadCars.Pool[0];
        garage.Add(bought.Key, week: 1);

        nav.CarBought(bought);

        var page = Assert.IsType<BoughtFirstCarScreenViewModel>(shell.CurrentScreen);
        Assert.Empty(page.SummaryLine);
    }

    [Fact]
    public void SettingsTile_TravelsThroughTheCoordinator()
    {
        // RESET GAME is the story flow's and the session's plumbing, so the
        // menu's tile is a report rather than a page the navigator builds.
        var (nav, shell, host, _, _, _, _, _, _, _, _) = Create();
        nav.ShowMenuPage();
        var menu = Assert.IsType<MenuScreenViewModel>(shell.CurrentScreen);

        menu.SettingsCommand.Execute(null);

        Assert.Equal(1, host.Settings);
    }

    [Fact]
    public void HaulDoor_SettlesTheFlowAndItsReceipt_WithoutTheCoordinator()
    {
        // The haul is a job type, not a report: the control page's tap prices
        // the placeholder run, banks it through the flow and puts the flow's
        // own receipt up. CONTINUE is the control page the tap came from.
        var (nav, shell, _, _, _, _, _, _, _, _, _) = Create();

        nav.ControlPage().HaulCommand.Execute(null);

        var receipt = Assert.IsType<CarHaulReceiptScreenViewModel>(shell.CurrentScreen);
        Assert.NotEmpty(receipt.CarLine);
        receipt.ContinueCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(shell.CurrentScreen);
    }

    private static string RoadTestButton(CalendarScreenViewModel calendar) =>
        Assert.Single(calendar.Days, day => day.DayOfWeek == 3).EventLabel;
}
