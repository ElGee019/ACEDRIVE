using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The weekly missing-person run end to end (ToWhereIsWilly.md §4/§5), driven
/// the way the root drives it: the Wednesday calendar row opens the quest on a
/// pinned game clock, a shop row pins the HUD, parking finds the elder (or
/// spends an attempt), the home row pins the elder's door, DROP OFF pays,
/// stamps and hands over the receipt, and CONTINUE returns to the control page.
/// The wrong-address, give-up, restart, completed and locked branches run on
/// the same session graph.
/// </summary>
public sealed class WhereIsWillyFlowTests
{
    // Wednesday 2026-10-14, 12:00 game time: inside the 8am-8pm window.
    private static readonly DateTime WednesdayNoon =
        new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private const int Seed = 11;

    private static readonly CarSpec TestCar = new(
        "ks_willy_car", 2020, "Test", "One", Passengers: 2, Class: 3,
        Price: 20000.0, Horsepower: 120, EngineCc: 1600, TopSpeedKmh: 190);

    [Fact]
    public void WednesdayTap_OpensTheQuestPageAndPinsNothing()
    {
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(hud);

        WhereIsWillyScreenViewModel page = Begin(session, hud);

        Assert.True(session.Willy.InFlight);
        Assert.True(session.Vm.IsShown);
        Assert.Equal(WhereIsWillyPhase.Searching, page.Phase);
        Assert.Equal(2, page.Shops.Count);
        Assert.All(page.Shops, row => Assert.NotEmpty(row.Address));
        Assert.False(page.CanGiveUp);
        Assert.Null(hud.Destination);
        Assert.Contains("has gone missing", page.IntroLine, StringComparison.Ordinal);
        Assert.Equal("WHERE IS WILLY", page.Title);
    }

    [Fact]
    public void ElderHome_StaysInsideTheOperatingZone()
    {
        // The zone has discovered Street 1 only, while the map carries the
        // fictional pool. The free-house fallback that places the elder must
        // not hide them on a street the zone has not reached (TOZONES §4.7).
        var map = new AddressMap(
            "Germany", "Brands", 1929.0, 30.0,
            [
                new AddressStreet("street-1", "Street 1", 0.0, 0.5),
                new AddressStreet("street-2", "Street 2", 0.5, 1.0),
                new AddressStreet("street-3", "Street 3", 0.0, 0.5, Fictional: true),
            ]);
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(hud, map: map);
        WhereIsWillyScreenViewModel search = Begin(session, hud);

        Pin(session, hud, HidingShop(search));
        Park(session, hud);

        var found = Assert.IsType<WhereIsWillyScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(WhereIsWillyPhase.Found, found.Phase);
        Assert.Contains("Street 1", found.FoundLine, StringComparison.Ordinal);
        Assert.DoesNotContain("Street 2", found.FoundLine, StringComparison.Ordinal);
        Assert.DoesNotContain("Street 3", found.FoundLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ShopRow_PinsTheCoffeeShopAndHidesThePanel()
    {
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(hud);
        WhereIsWillyScreenViewModel page = Begin(session, hud);
        WillyShopRow row = page.Shops[0];

        page.SelectShopCommand.Execute(row);

        Assert.False(session.Vm.IsShown);
        Assert.Equal(PoiTypes.CoffeeShop, hud.Destination?.Type);
        Assert.Equal(row.Poi.Id, hud.Destination?.Id);
        Assert.Equal("HudWillySearchDrive", hud.Copy?.DriveKey);
        Assert.True(session.Willy.InFlight);
    }

    [Fact]
    public void ParkingAtTheHidingShop_OpensTheFoundPage()
    {
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(hud);
        WhereIsWillyScreenViewModel search = Begin(session, hud);
        PointOfInterest hiding = HidingShop(search);

        Pin(session, hud, hiding);
        Park(session, hud);

        var found = Assert.IsType<WhereIsWillyScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(WhereIsWillyPhase.Found, found.Phase);
        Assert.True(session.Vm.IsShown);
        // The pinned shop is released, so a stray ENTER cannot open the shop.
        Assert.Null(hud.Destination);
        Assert.Contains("Missing person found", found.FoundLine, StringComparison.Ordinal);
        Assert.Contains("DRIVE", found.DriveHomeLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void ParkingAtAnotherShop_SpendsAnAttemptAndOffersGiveUp()
    {
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(hud);
        WhereIsWillyScreenViewModel search = Begin(session, hud);
        Assert.False(search.CanGiveUp);
        PointOfInterest wrong = WrongShop(search);

        Pin(session, hud, wrong);
        Park(session, hud);

        var page = Assert.IsType<WhereIsWillyScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(WhereIsWillyPhase.Searching, page.Phase);
        Assert.True(page.ShowNotHere);
        Assert.True(page.CanGiveUp);
        Assert.Null(hud.Destination);
    }

    [Fact]
    public void FindDriveHomeDropOff_PaysStampsAndLeavesTheCertificate()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet();
        var trunk = new Trunk();
        var friends = new FakeFriendshipStore();
        var drivers = new FakeDriverStore();
        TestSessionResult session = Create(
            hud, wallet, friends, trunk, drivers: drivers, garage: GarageWithTestCar());

        WhereIsWillyScreenViewModel search = Begin(session, hud);
        Pin(session, hud, HidingShop(search));
        Park(session, hud);
        var found = Assert.IsType<WhereIsWillyScreenViewModel>(session.Vm.CurrentScreen);
        double cashBefore = wallet.Cash;

        found.DriveHomeCommand.Execute(null);

        Assert.False(session.Vm.IsShown);
        Assert.Equal(PoiTypes.Willy, hud.Destination?.Type);
        Assert.Equal("HudWillyHomeDrive", hud.Copy?.DriveKey);

        hud.AtDestination = false;
        session.Vm.Poll();
        Assert.Equal(WhereIsWillyPhase.Found, session.Willy.Phase);
        Assert.Equal(PoiTypes.Willy, hud.Destination?.Type);

        Park(session, hud);

        // Home arrival: friendship +1 and the HUD's DROP OFF, no payment yet.
        Assert.Equal("HudWillyDropOff", hud.Copy?.ActionKey);
        Assert.Equal("HudWillyHomeArrived", hud.Copy?.ArrivedKey);
        string elder = Assert.Single(friends.Names);
        Assert.Equal(1, friends.GetLevel(elder));
        Assert.Equal(cashBefore, wallet.Cash, 6);

        hud.RaiseEnter();

        var receipt = Assert.IsType<WhereIsWillyReceiptScreenViewModel>(session.Vm.CurrentScreen);
        // 20,000 average * 0.04 = 800 base; two shops x1.00; first try x2.0.
        Assert.Equal(cashBefore + 1600.0, wallet.Cash, 6);
        Assert.Equal(1600.0, wallet.CreditEarned, 6);
        Assert.Contains("1600", receipt.TotalLine, StringComparison.Ordinal);
        Assert.Contains("Week 1 1924", receipt.CertificateLine, StringComparison.Ordinal);
        TrunkItem certificate = Assert.Single(trunk.Items);
        Assert.Equal(0.0, certificate.UnitVolume, 6);
        Assert.Equal(1, certificate.Quantity);
        Assert.Equal("2026-10-14", session.WillyLedger.LastCompletedDate);
        Assert.Contains(drivers.Saved, state => state.LastWillyDate == "2026-10-14");
        Assert.False(session.Willy.InFlight);
        Assert.Null(hud.Destination);

        receipt.ContinueCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(session.Vm.CurrentScreen);
    }

    [Fact]
    public void WrongAddress_PenalizesFriendshipAndClearsTheRunUnpaid()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet();
        var friends = new FakeFriendshipStore();
        var drivers = new FakeDriverStore();
        TestSessionResult session = Create(
            hud, wallet, friends, drivers: drivers, garage: GarageWithTestCar());
        WhereIsWillyScreenViewModel search = Begin(session, hud);
        Pin(session, hud, HidingShop(search));
        Park(session, hud);
        double cashBefore = wallet.Cash;

        // Leg 2 to another door: the player pinned a known address instead of
        // driving the elder home (adaptation A8).
        var wrongDoor = new PointOfInterest(
            "other-door", "Somebody Else",
            new AddressLocation("Street 2", 3, HouseNumberPlan.SideOf(3)),
            PoiTypes.Grocery);
        Pin(session, hud, wrongDoor);
        Park(session, hud);

        Assert.Equal("HudWillyContinue", hud.Copy?.ActionKey);
        Assert.Equal("HudWillyWrongAddress", hud.Copy?.ArrivedKey);
        string elder = Assert.Single(friends.Names);
        Assert.Equal(0, friends.GetLevel(elder));

        hud.RaiseEnter();

        Assert.False(session.Willy.InFlight);
        Assert.Equal(cashBefore, wallet.Cash, 6);
        Assert.Null(session.WillyLedger.LastCompletedDate);
        Assert.DoesNotContain(drivers.Saved, state => state.LastWillyDate is not null);
        Assert.Null(hud.Destination);
        Assert.False(session.Vm.IsShown);
    }

    [Fact]
    public void GiveUpAfterOneAttempt_LeavesTheRowOpenAndPaysNothing()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet();
        var drivers = new FakeDriverStore();
        TestSessionResult session = Create(hud, wallet, drivers: drivers);
        WhereIsWillyScreenViewModel search = Begin(session, hud);
        Pin(session, hud, WrongShop(search));
        Park(session, hud);
        var page = Assert.IsType<WhereIsWillyScreenViewModel>(session.Vm.CurrentScreen);
        double cashBefore = wallet.Cash;

        page.GiveUpCommand.Execute(null);

        Assert.False(session.Willy.InFlight);
        Assert.Equal(cashBefore, wallet.Cash, 6);
        Assert.Null(session.WillyLedger.LastCompletedDate);
        Assert.DoesNotContain(drivers.Saved, state => state.LastWillyDate is not null);
        Assert.Contains("still out there", hud.Notice, StringComparison.Ordinal);
        Assert.False(session.Vm.IsShown);

        // The next tap draws a fresh search instead of resuming the abandoned one.
        WhereIsWillyScreenViewModel again = Begin(session, hud);
        Assert.True(session.Willy.InFlight);
        Assert.Equal(WhereIsWillyPhase.Searching, again.Phase);
        Assert.False(again.CanGiveUp);
    }

    [Fact]
    public void StampedWednesday_ReadsCompletedRefusesAndReopensNextWeek()
    {
        var hud = new FakeTelemetryHud();
        var drivers = new FakeDriverStore();
        TestSessionResult session = Create(
            hud, drivers: drivers,
            saved: DriverState.Fresh with { LastWillyDate = "2026-10-14" });

        CalendarScreenViewModel calendar = OpenCalendar(session);
        CalendarRow willy = WillyRow(calendar);
        Assert.True(willy.Completed);
        Assert.False(willy.Locked);
        Assert.Equal("Where is Willy (Completed)", willy.EventLabel);

        calendar.DayCommand.Execute(4);
        session.Clock.Advance(TimeSpan.FromSeconds(0.51));
        session.Vm.Poll();
        Assert.Equal("SEE YOU NEXT WEDNESDAY", WillyRow(calendar).EventLabel);
        Assert.False(session.Willy.InFlight);
        Assert.Null(hud.Destination);

        // Even a direct call cannot reopen a stamped Wednesday.
        session.Willy.Begin();
        Assert.False(session.Willy.InFlight);

        // A week later the same game date no longer matches and the row opens.
        // One game minute past the weekly jump keeps the page's minute guard
        // from mistaking the new day for the old one.
        session.Clock.Advance(GameWeek() + GameMinute());
        session.Vm.Poll();
        Assert.Equal("Where is Willy", WillyRow(calendar).EventLabel);

        calendar.DayCommand.Execute(4);
        Assert.True(session.Willy.InFlight);
        Assert.IsType<WhereIsWillyScreenViewModel>(session.Vm.CurrentScreen);
    }

    [Fact]
    public void NoCoffeeShop_LocksTheRowAndDrawsNothing()
    {
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(hud, directory: new PoiDirectory());

        CalendarScreenViewModel calendar = OpenCalendar(session);
        CalendarRow willy = WillyRow(calendar);
        Assert.True(willy.Locked);
        Assert.False(willy.Completed);
        Assert.Equal("UNLOCK COFFEE BEANS CAREER FIRST", willy.EventLabel);

        calendar.DayCommand.Execute(4);
        session.Clock.Advance(TimeSpan.FromSeconds(0.51));
        session.Vm.Poll();

        Assert.Equal("UNLOCK COFFEE BEANS CAREER FIRST", WillyRow(calendar).EventLabel);
        Assert.False(session.Willy.InFlight);
        Assert.Null(hud.Destination);
    }

    [Fact]
    public void RestartMidQuest_ResumesNothingAndBanksNothing()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet();
        var drivers = new FakeDriverStore();
        TestSessionResult first = Create(hud, wallet, drivers: drivers);
        WhereIsWillyScreenViewModel search = Begin(first, hud);
        Pin(first, hud, HidingShop(search));
        Park(first, hud);
        Assert.True(first.Willy.InFlight);

        // Whatever the save holds now (nothing paid yet), a new session opens
        // with the row available and no quest behind it (Unity parity, §2.9).
        var secondWallet = new DriverWallet();
        var secondHud = new FakeTelemetryHud();
        TestSessionResult second = Create(
            secondHud, secondWallet,
            saved: drivers.Saved.Count > 0 ? drivers.Saved[^1] : DriverState.Fresh);

        Assert.False(second.Willy.InFlight);
        Assert.Null(second.WillyLedger.LastCompletedDate);
        CalendarScreenViewModel calendar = OpenCalendar(second);
        Assert.Equal("Where is Willy", WillyRow(calendar).EventLabel);
        Assert.Equal(DriverWallet.StartingCash, secondWallet.Cash, 6);
    }

    [Fact]
    public void QuestInFlight_BlocksTheTaxiShiftAndTheDeliveryCareer()
    {
        var hud = new FakeTelemetryHud();
        var shift = new TaxiShift();
        TestSessionResult session = Create(hud, shift: shift);
        WhereIsWillyScreenViewModel search = Begin(session, hud);

        session.Vm.TaxiShiftCommand.Execute(null);
        Assert.False(shift.IsStarted);
        Assert.Same(search, session.Vm.CurrentScreen);

        // The panel goes back to the menu/control page for the delivery tap;
        // the run stays in flight and the career refuses to open.
        search.BackCommand.Execute(null);
        OpenControl(session).DeliveryCommand.Execute(null);

        Assert.IsType<MainControlScreenViewModel>(session.Vm.CurrentScreen);
        Assert.True(session.Willy.InFlight);
    }

    [Fact]
    public void IgnoreEventGates_ReplaysAStampedWednesdayAtAnyHour()
    {
        // TEMPORARY (testing): the switch opens the Wednesday window and the
        // completion stamp together, so a stamped run starts again at 22:00 on
        // a Wednesday and the calendar row never reads completed.
        var hud = new FakeTelemetryHud();
        var drivers = new FakeDriverStore();
        TestSessionResult session = Create(
            hud, drivers: drivers,
            saved: DriverState.Fresh with { LastWillyDate = "2026-10-14" },
            gameStart: new DateTime(2026, 10, 14, 22, 0, 0, DateTimeKind.Utc),
            ignoreGates: true);

        CalendarScreenViewModel calendar = OpenCalendar(session);
        Assert.Equal("Where is Willy", WillyRow(calendar).EventLabel);

        session.Willy.Begin();

        Assert.True(session.Willy.InFlight);
        Assert.IsType<WhereIsWillyScreenViewModel>(session.Vm.CurrentScreen);
    }

    [Fact]
    public void DirectBegin_OutsideTheWednesdayWindow_Refuses()
    {
        TestSessionResult session = Create(
            new FakeTelemetryHud(),
            gameStart: new DateTime(2026, 10, 14, 20, 0, 0, DateTimeKind.Utc));
        session.Story.FinishDialogue();

        session.Willy.Begin();

        Assert.False(session.Willy.InFlight);
        Assert.IsNotType<WhereIsWillyScreenViewModel>(session.Vm.CurrentScreen);
    }

    [Fact]
    public void DirectBegin_WithoutACoffeeShop_Refuses()
    {
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(hud, directory: new PoiDirectory());
        session.Story.FinishDialogue();

        session.Willy.Begin();

        Assert.False(session.Willy.InFlight);
        Assert.Null(hud.Destination);
    }

    [Fact]
    public void DirectBegin_OnAStampedWednesday_Refuses()
    {
        TestSessionResult session = Create(
            new FakeTelemetryHud(),
            saved: DriverState.Fresh with { LastWillyDate = "2026-10-14" });
        session.Story.FinishDialogue();

        session.Willy.Begin();

        Assert.False(session.Willy.InFlight);
    }

    [Fact]
    public void InFlightWillyRow_ReopensTheSameSearchInsteadOfDrawing()
    {
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(hud);
        WhereIsWillyScreenViewModel search = Begin(session, hud);
        string[] before = [.. search.Shops.Select(s => s.Poi.Id)];

        search.BackCommand.Execute(null);
        CalendarScreenViewModel calendar = OpenCalendar(session);
        calendar.DayCommand.Execute(4);

        var reopened = Assert.IsType<WhereIsWillyScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(WhereIsWillyPhase.Searching, reopened.Phase);
        Assert.Equal(before, reopened.Shops.Select(s => s.Poi.Id));
    }

    // ---- helpers -----------------------------------------------------------

    private static TestSessionResult Create(
        FakeTelemetryHud? hud = null, DriverWallet? wallet = null,
        FakeFriendshipStore? friends = null, Trunk? trunk = null,
        FakeDriverStore? drivers = null, Garage? garage = null,
        PoiDirectory? directory = null, DriverState? saved = null,
        TaxiShift? shift = null, DateTime? gameStart = null,
        AddressMap? map = null, bool ignoreGates = false)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        return TestSession.Create(new TestSessionOptions
        {
            SpeedKmh = 0f,
            GameStart = gameStart ?? WednesdayNoon,
            Hud = hud,
            Wallet = wallet,
            Friendships = friends,
            Trunk = trunk,
            DriverStore = drivers,
            Garage = garage,
            Cars = [TestCar],
            PoiDirectory = directory ?? TwoCoffeeShops(),
            AddressMap = map,
            WillyRng = new Random(Seed),
            SavedDriver = saved,
            Shift = shift,
            IgnoreEventGates = ignoreGates,
        });
    }

    private static WhereIsWillyScreenViewModel Begin(
        TestSessionResult session, FakeTelemetryHud hud)
    {
        // The calendar is opened from the menu: the car is not parked on any
        // pinned door yet.
        hud.AtDestination = false;
        OpenCalendar(session).DayCommand.Execute(4);
        return Assert.IsType<WhereIsWillyScreenViewModel>(session.Vm.CurrentScreen);
    }

    private static CalendarScreenViewModel OpenCalendar(
        TestSessionResult session, bool finishIntro = true)
    {
        if (finishIntro)
        {
            session.Story.FinishDialogue();
            session.Vm.Show();
        }

        var menu = Assert.IsType<MenuScreenViewModel>(session.Vm.CurrentScreen);
        menu.CalendarCommand.Execute(null);
        return Assert.IsType<CalendarScreenViewModel>(session.Vm.CurrentScreen);
    }

    private static MainControlScreenViewModel OpenControl(TestSessionResult session)
    {
        session.Vm.Show();
        var menu = Assert.IsType<MenuScreenViewModel>(session.Vm.CurrentScreen);
        menu.JobsCommand.Execute(null);
        return Assert.IsType<MainControlScreenViewModel>(session.Vm.CurrentScreen);
    }

    // The hiding shop is the flow's first RNG step (one Next over the shop
    // list), so the seeded twin reproduces the draw without the flow exposing
    // its quest state.
    private static PointOfInterest HidingShop(WhereIsWillyScreenViewModel page) =>
        page.Shops[new Random(Seed).Next(page.Shops.Count)].Poi;

    private static PointOfInterest WrongShop(WhereIsWillyScreenViewModel page)
    {
        PointOfInterest hiding = HidingShop(page);
        return page.Shops.First(row => row.Poi.Id != hiding.Id).Poi;
    }

    private static void Pin(
        TestSessionResult session, FakeTelemetryHud hud, PointOfInterest door)
    {
        hud.SetDestination(door);
        hud.AtDestination = false;
        session.Vm.Poll();
    }

    private static void Park(TestSessionResult session, FakeTelemetryHud hud)
    {
        hud.AtDestination = true;
        session.Vm.Poll();
    }

    private static CalendarRow WillyRow(CalendarScreenViewModel calendar) =>
        Assert.Single(calendar.Days, d => d.DayOfWeek == WhereIsWillyRules.DayOfWeek);

    private static PoiDirectory TwoCoffeeShops() =>
        new(
        [
            new PointOfInterest(
                "coffee_1", "Café Central",
                new AddressLocation("Street 1", 5, HouseNumberPlan.SideOf(5)),
                PoiTypes.CoffeeShop),
            new PointOfInterest(
                "coffee_2", "Café Nord",
                new AddressLocation("Street 1", 9, HouseNumberPlan.SideOf(9)),
                PoiTypes.CoffeeShop),
        ]);

    private static Garage GarageWithTestCar()
    {
        var garage = new Garage([TestCar]);
        garage.Add(TestCar.Key, week: 1);
        return garage;
    }

    // Seven game days in real seconds at the session clock's pace.
    private static TimeSpan GameWeek() =>
        TimeSpan.FromSeconds(7 * 24 * 60 * GameClock.RealSecondsPerGameMinute);

    // One game minute in real seconds.
    private static TimeSpan GameMinute() =>
        TimeSpan.FromSeconds(GameClock.RealSecondsPerGameMinute);
}
