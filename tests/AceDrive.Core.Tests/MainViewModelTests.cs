using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>ViewModel flows run headless against fakes (LSP in action).</summary>
public sealed class MainViewModelTests
{
    internal static readonly OnboardingContent EmptyContent = new([], new Dictionary<string, string[]>(), [], []);
    private static (MainViewModel Vm, FakeClock Clock, FakeTelemetry Tel, FakeStore Store)
        Create(
            float speedKmh = 90f, OnboardingState? progress = null,
            DeliveryCatalog? catalog = null, HubCatalog? hubs = null, ILog? log = null,
            RoadTestLedger? roads = null, IGameClockStore? clocks = null, int startWeek = 1,
            DateTime? gameStart = null, IRoadTestStore? roadTestStore = null,
            CityEconomy? city = null, ICityStore? cityStore = null,
            DriverWallet? wallet = null, IDriverStore? driverStore = null,
            IFriendshipStore? friendships = null,
            IReadOnlyList<RoadTestCar>? cars = null, string? carKsId = null)
    {
        // Deterministic locale: resources resolve to en, numbers format with '.'.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var tel = new FakeTelemetry();
        tel.Next = tel.Next with { SpeedKmh = speedKmh };
        var store = new FakeStore();
        var onboarding = new FakeOnboardingStore();
        if (progress is not null)
        {
            onboarding.Save(progress);
        }
        var board = catalog ?? DeliveryCatalog.Default;
        var career = new DeliveryCareer(hubs ?? HubCatalog.Default, board.StartCredP);
        var brands = new FakeCarBrandSource { CurrentKsId = carKsId };
        var vm = new MainViewModel(tel, clock, new GameClock(clock, gameStart ?? clock.UtcNow), new TaxiShift(), store,
            log ?? new NullLogger(), FareTable.Default,
            new TaxiJobProposer(NamePoolTable.Default, seed: 0),
            onboarding, new FakeContentSource(EmptyContent), new FakeVoiceListener(available: false),
            brands, TodoTable.Default, board, career,
            city ?? new CityEconomy(board.Stores, CityRules.Default), cityStore ?? new FakeCityStore(),
            roads ?? TestRoadCars.Ledger(), roadTestStore ?? new FakeRoadTestStore(),
            clocks ?? new FakeGameClockStore(), startWeek,
            new VoiceBeat(clock), wallet ?? new DriverWallet(),
            driverStore ?? new FakeDriverStore(),
            friendships ?? new FakeFriendshipStore(), cars ?? TestRoadCars.Pool);
        if (carKsId is not null)
        {
            // The watcher thread lands via the dispatcher, which runs inline
            // headless: the VM now follows the announced car like live.
            brands.Fire("test", carKsId);
        }
        return (vm, clock, tel, store);
    }

    [Fact]
    public void NoGame_ShowsWaiting()
    {
        var (vm, _, tel, _) = Create();
        tel.GameRunning = false;
        tel.PhysicsPresent = false;
        vm.Poll();
        Assert.False(vm.IsLive);
        Assert.Equal("0 km/h", vm.SpeedText);
    }

    [Fact]
    public void LiveTelemetry_UpdatesDisplay()
    {
        var (vm, _, tel, _) = Create();
        tel.Next = tel.Next with { PacketId = 7, Rpm = 6000, Gear = 4, Gas = 0.8f };
        vm.Poll();
        Assert.True(vm.IsLive);
        Assert.Equal("90 km/h", vm.SpeedText);
        Assert.Equal("3", vm.GearText); // gear 4 -> 3rd
    }

    // The real route onto an active job: tap the control page's taxi entry
    // (opens the proposal page) and ACCEPT the shown proposal (PORTPLAN 11).
    private static TaxiJobProposalViewModel ProposeJob(MainViewModel vm)
    {
        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        proposal.AcceptCommand.Execute(null);
        return proposal;
    }

    [Fact]
    public void Accept_CapacityFollowsTheDrivenCar()
    {
        // Unity scales the distance charge by passengerCapacity: a 4-seat
        // car runs the same 8.2 km ride at 4x the charge.
        var pool = new List<RoadTestCar>
        {
            new("ks_test_sedan", 2020, "Test", "Sedan", Passengers: 4),
        };
        var (vm, _, _, _) = Create(
            speedKmh: 0f, cars: pool, carKsId: "ks_test_sedan",
            roads: new RoadTestLedger(pool, seed: 0));
        ProposeJob(vm);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        // 43.15 x 4 = 172.60 charge + 10 forfait = 182.60 base,
        // 36.52 tip − 50 fine = 169.12.
        Assert.Equal("€169.12", vm.FareText);
        Assert.Equal("Fare: 4 × €43.15 = €172.60", receipt.FareLine);
        Assert.Contains("Customers: 4.", receipt.CustomerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Accept_UnknownCar_RidesSolo()
    {
        // No driving slice: ACCEPT settles the placeholder ride at once
        // (8.2 km, tip eligible, one 50 fine): 53.15 base + 10.63 tip −
        // 50 fine = 13.78 on the receipt screen, banked to the wallet
        // and the driver save.
        var wallet = new DriverWallet();
        var drivers = new FakeDriverStore();
        var (vm, _, _, _) = Create(speedKmh: 0f, wallet: wallet, driverStore: drivers);
        ProposeJob(vm);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("€13.78", vm.FareText);
        Assert.Contains("1 jobs", vm.ShiftText, StringComparison.Ordinal);
        Assert.Equal(13.78, wallet.Cash);
        Assert.Equal(0.082, wallet.Xp, precision: 9);
        Assert.Equal(new DriverState(13.78, wallet.Xp), drivers.Saved[^1]);
        Assert.Contains("13.78", receipt.TotalLine, StringComparison.Ordinal);
        Assert.Contains("0.08", receipt.XpLine, StringComparison.Ordinal);

        // CONTINUE lands back on the control page.
        receipt.ContinueCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void Accept_SettlesFriendship_AndRepeatRidesGrowIt()
    {
        // First ride with a stranger lands familiarity 1 and the receipt
        // box shows the stranger sentence.
        var friends = new FakeFriendshipStore();
        var (vm, _, _, _) = Create(speedKmh: 0f, friendships: friends);
        ProposeJob(vm);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.True(receipt.HasFriendship);
        Assert.Single(friends.Names);
        string baseName = Assert.Single(friends.Names);
        string first = baseName.Split(' ')[0];
        Assert.Equal($"New familiarity with {first}: 1. Still a stranger though.", receipt.FriendshipLine);
        Assert.False(receipt.FriendshipHighlight);

        // Repeat rides with the same resident grow the persisted level;
        // tier-ups land on 2->3 (stranger to acquaintance) and 4->5
        // (acquaintance to friend, the Unity bands).
        Assert.False(friends.RegisterRide(baseName).TierIncreased);
        Assert.True(friends.RegisterRide(baseName).TierIncreased);
        Assert.Equal(3, friends.GetLevel(baseName));
        Assert.False(friends.RegisterRide(baseName).TierIncreased);
        var fifth = friends.RegisterRide(baseName);
        Assert.Equal(5, fifth.Level);
        Assert.True(fifth.TierIncreased);
    }

    [Fact]
    public void TaxiShiftTap_OpensProposalAndStartsShift_AcceptStartsJob()
    {
        var (vm, _, _, _) = Create(speedKmh: 0f);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);

        // The tap owns the shift (Unity OnStartTaxiShift stamps it started
        // before the job shows), and lands on the proposal page.
        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        Assert.Equal("CONTINUE TAXI SHIFT", control.TaxiLabel);
        Assert.Contains("left", vm.ShiftText, StringComparison.Ordinal);
        Assert.True(control.VoiceActions.ContainsKey("CONTINUE TAXI SHIFT"));

        // Accept settles the placeholder ride straight into its receipt:
        // both placeholder addresses ride the customer line (leg order is
        // the flip's), on the receipt screen.
        proposal.AcceptCommand.Execute(null);
        Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Contains("Address A", vm.JobLine, StringComparison.Ordinal);
        Assert.Contains("Address B", vm.JobLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ProposalDecline_Regenerates_KeepsScreen()
    {
        var (vm, _, _, _) = Create(speedKmh: 0f);
        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        string first = proposal.Phrase;

        proposal.DeclineCommand.Execute(null);
        Assert.Same(proposal, vm.CurrentScreen);
        Assert.NotEqual(first, proposal.Phrase);
    }

    [Fact]
    public void ProposalBack_ClosesPage_ShiftStaysStarted()
    {
        // Unity parity: the tap started the shift; walking away from the
        // proposal does not undo it — the 30-minute idle clock now owns it.
        var (vm, _, _, _) = Create(speedKmh: 0f);
        vm.FinishDialogue();
        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);

        proposal.BackCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Contains("left", vm.ShiftText, StringComparison.Ordinal);
        Assert.DoesNotContain("Proposed", vm.JobLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiftTimeout_EndsShift_FlipsLabelBack()
    {
        var (vm, clock, _, _) = Create(speedKmh: 0f);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        vm.TaxiShiftCommand.Execute(null);
        Assert.Equal("CONTINUE TAXI SHIFT", control.TaxiLabel);

        clock.Advance(TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1)));
        vm.Poll();
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);
        Assert.DoesNotContain("left", vm.ShiftText, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDayFlip_MovesHighlight_LogsTransition()
    {
        // Half speed: ~53 real minutes carry the game across midnight. The
        // flip moves the teal outline; the gate is re-read on every tap.
        var log = new TestLog();
        var (vm, clock, _, _) = Create(speedKmh: 0f, log: log);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(2, control.Today);

        clock.Advance(TimeSpan.FromMinutes(53));
        vm.Poll();
        Assert.Equal(3, control.Today);
        Assert.Contains(log.Infos, m => m.StartsWith("calendar day changed:", StringComparison.Ordinal));
    }

    [Fact]
    public void TapRoadTestDay_MarksTestedAndSwitches()
    {
        // Manual stand-in until the road-test drive flow lands: tapping the
        // Tuesday button inside its gate banks the running car and shows the
        // next pick. The strip opens on Monday, so carry the clock over first.
        var log = new TestLog();
        var roads = TestRoadCars.Ledger();
        var bank = new FakeRoadTestStore();
        var (vm, clock, _, _) = Create(speedKmh: 0f, log: log, roads: roads, roadTestStore: bank);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        clock.Advance(TimeSpan.FromMinutes(53)); // across Monday midnight: Tuesday noon
        vm.Poll();
        string first = TuesdayButton(control);
        Assert.NotEqual(string.Empty, first);

        control.DayCommand.Execute(3);
        string second = TuesdayButton(control);
        Assert.NotEqual(string.Empty, second);
        Assert.NotEqual(first, second);
        Assert.Contains(log.Infos, m => m.StartsWith("road test done:", StringComparison.Ordinal));
        // Completion is the only thing allowed to move the car, and the new
        // pick hits disk at once so the next session opens on it.
        RoadTestSnapshot saved = Assert.Single(bank.Saved);
        Assert.Equal(roads.Current?.Id, saved.CarId);
    }

    [Fact]
    public void TapRoadTestOutsideGate_GlowsThenWarnsAndKeepsCar()
    {
        // Tuesday night: the day matches but the 8pm gate is shut. The tap
        // still acknowledges with the one-beat teal glow, then answers with
        // the COME BACK hint instead of banking the car.
        var log = new TestLog();
        var roads = TestRoadCars.Ledger();
        var bank = new FakeRoadTestStore();
        var (vm, clock, _, _) = Create(speedKmh: 0f, log: log, roads: roads, roadTestStore: bank);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        clock.Advance(TimeSpan.FromMinutes(75)); // 2.2 s per game minute: Tuesday 22:0x
        control.DayCommand.Execute(3);
        Assert.True(Assert.Single(control.Days, d => d.DayOfWeek == 3).Flash);

        clock.Advance(TimeSpan.FromSeconds(0.51)); // the beat drains on the poll
        vm.Poll();
        Assert.Equal("COME BACK ON TUESDAY BETWEEN 8am AND 8pm", TuesdayButton(control));
        Assert.DoesNotContain(log.Infos, m => m.StartsWith("road test done:", StringComparison.Ordinal));
        Assert.Empty(bank.Saved);
    }

    [Fact]
    public void Start_BanksThePickSoAKillCannotRedrawIt()
    {
        // Rebuild/Stop-Debugging never reaches a completion or a Closed event,
        // so the session's pick must be on disk from the first moment.
        var bank = new FakeRoadTestStore();
        var roads = TestRoadCars.Ledger();
        var (vm, _, _, _) = Create(roads: roads, roadTestStore: bank);
        vm.Start();

        RoadTestSnapshot saved = Assert.Single(bank.Saved);
        Assert.Equal(roads.Current?.Id, saved.CarId);
    }

    [Fact]
    public void NewSession_ResumesTheSavedRoadTestCar()
    {
        // Two sessions, two unrelated seeds, one banked pick: the Tuesday
        // button names the same car in both (PORTPLAN 49).
        var bank = new FakeRoadTestStore();
        var (vm, _, _, _) = Create(roads: TestRoadCars.Ledger(seed: 7), roadTestStore: bank);
        vm.Start();
        RoadTestSnapshot saved = Assert.Single(bank.Saved);

        var restarted = TestRoadCars.Ledger(seed: 4242, saved: saved);
        RoadTestCar car = Assert.IsType<RoadTestCar>(restarted.Current);
        var (again, _, _, _) = Create(roads: restarted);
        again.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(again.CurrentScreen);

        Assert.Equal(saved.CarId, car.Id);
        Assert.Equal(TestRoadCars.Button(car), TuesdayButton(control));
    }

    [Fact]
    public void TapOtherDay_StaysLocked()
    {
        var log = new TestLog();
        var roads = TestRoadCars.Ledger();
        var (vm, _, _, _) = Create(speedKmh: 0f, log: log, roads: roads);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        string first = TuesdayButton(control);

        control.DayCommand.Execute(2);
        Assert.Equal(first, TuesdayButton(control));
        Assert.Contains(log.Infos, m => m.StartsWith("calendar day 2 tapped", StringComparison.Ordinal));
    }

    [Fact]
    public void Poll_TicksTheTownOnGameMinutes()
    {
        // The town lives on the game clock (PORTPLAN 25): a full shelf gains
        // one citizen per 30 game minutes, and nothing moves before that
        // interval is due. 2.2 real seconds per game minute here.
        var store = new DeliveryStore("full-market", "Full Market", "Schmitz", 40, 40, 4.40);
        var city = new CityEconomy([store], CityRules.Default);
        var (vm, clock, _, _) = Create(
            speedKmh: 0f, catalog: new DeliveryCatalog([store], 0.7, 100), city: city);
        vm.FinishDialogue();
        vm.Poll(); // primes the town at the session's first moment

        clock.Advance(TimeSpan.FromSeconds(60)); // 27 game minutes: not yet
        vm.Poll();
        Assert.Equal(100, city.TotalCitizens([store]));

        clock.Advance(TimeSpan.FromSeconds(10)); // 31 game minutes: one tick
        vm.Poll();
        Assert.Equal(101, city.TotalCitizens([store]));
    }

    [Fact]
    public void SaveCity_BanksTheTownTheNextSessionReopens()
    {
        // Exit is the only town write (Unity persists storeStates on quit,
        // never mid-session), so the polls leave the store alone until
        // SaveCity runs — and what it writes resumes the same city.
        var bank = new FakeCityStore();
        var store = new DeliveryStore("full-market", "Full Market", "Schmitz", 40, 40, 4.40);
        var city = new CityEconomy([store], CityRules.Default);
        var (vm, clock, _, _) = Create(speedKmh: 0f, city: city, cityStore: bank);
        vm.FinishDialogue();
        vm.Poll(); // primes the town at the session's first moment

        clock.Advance(TimeSpan.FromSeconds(70)); // 31 game minutes: one tick
        vm.Poll();
        Assert.Empty(bank.Saved);

        vm.SaveCity();
        StoreState banked = Assert.Single(Assert.Single(bank.Saved));
        Assert.Equal(101, banked.Citizens);

        var restarted = new CityEconomy([store], CityRules.Default, bank.Saved[^1]);
        Assert.Equal(101, restarted.TotalCitizens([store]));
    }

    [Fact]
    public void SundayToMonday_RollsWeekAndKeepsRoadTestCar()
    {
        // Stepped across the Sunday-midnight boundary: the display week rolls,
        // the moment persists, and the untested car stays on the button — only
        // a driven road test moves it (PORTPLAN 49).
        var log = new TestLog();
        var store = new FakeGameClockStore();
        var roads = TestRoadCars.Ledger();
        var (vm, clock, _, _) = Create(speedKmh: 0f, log: log, roads: roads, clocks: store);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        string first = TuesdayButton(control);

        for (int i = 0; i < 8; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(53));
            vm.Poll();
        }

        Assert.Contains("Week 2", control.DateHeader, StringComparison.Ordinal);
        Assert.Equal(first, TuesdayButton(control));
        Assert.Contains(log.Infos, m => m.StartsWith("week 2 opened:", StringComparison.Ordinal));
        // Nothing was driven, so nothing was banked.
        Assert.Empty(roads.TestedIds);
        Assert.Contains(store.Saved, s => s.Week == 2);
    }

    [Fact]
    public void ExitAndRestart_ResumesDateTimeAndWeek()
    {
        // The exit path (what MainWindow.Closed runs) persists the moment; a
        // new VM seeded from the save reopens on the same date and week.
        var store = new FakeGameClockStore();
        var (vm, clock, _, _) = Create(speedKmh: 0f, clocks: store);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        clock.Advance(TimeSpan.FromMinutes(53));
        vm.Poll();
        vm.SaveClock();
        GameClockState saved = store.Saved[^1];

        var (restarted, _, _, _) = Create(
            progress: OnboardingState.Fresh with { BirthdayDone = true },
            clocks: store, gameStart: saved.GameNow, startWeek: saved.Week);
        var resumed = Assert.IsType<MainControlScreenViewModel>(restarted.CurrentScreen);
        Assert.Equal(control.DateHeader, resumed.DateHeader);
    }

    [Fact]
    public void Poll_Throttled_BanksClockWithoutAnyWindowEvent()
    {
        // The kill-a-rebuild case: no Closed event ever runs, so the poll loop
        // must bank the moment itself. Under the interval nothing is written;
        // past it, exactly one save lands.
        var store = new FakeGameClockStore();
        var (vm, clock, _, _) = Create(speedKmh: 0f, clocks: store);
        DateTime start = clock.UtcNow;
        clock.Advance(TimeSpan.FromSeconds(59));
        vm.Poll();
        Assert.Empty(store.Saved);

        clock.Advance(TimeSpan.FromSeconds(2));
        vm.Poll();
        GameClockState banked = Assert.Single(store.Saved);
        Assert.Equal(1, banked.Week);
        // 61 real seconds at 2.2 s per game minute = 27.7 game minutes: what a
        // killed process loses is the interval, never the whole session.
        Assert.InRange((banked.GameNow - start).TotalMinutes, 27.7, 27.8);
    }

    [Fact]
    public void ShiftAndPaidJob_BankClockImmediately()
    {
        // Checkpoints, not polls: a shift start and a settled accept bank
        // before the throttle is due, so a kill cannot rewind the calendar
        // past progress.
        var store = new FakeGameClockStore();
        var (vm, _, _, _) = Create(speedKmh: 0f, clocks: store);
        ProposeJob(vm);
        Assert.Equal(2, store.Saved.Count);
    }

    [Fact]
    public void Checkpoint_RestampsTheThrottle()
    {
        // Every bank path shares one stamp: a checkpoint resets the interval
        // instead of queueing a second write a few ticks later.
        var store = new FakeGameClockStore();
        var (vm, clock, _, _) = Create(speedKmh: 0f, clocks: store);
        vm.TaxiShiftCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(59));
        vm.Poll();
        Assert.Single(store.Saved);

        clock.Advance(TimeSpan.FromSeconds(2));
        vm.Poll();
        Assert.Equal(2, store.Saved.Count);
    }

    private static string TuesdayButton(MainControlScreenViewModel control) =>
        Assert.Single(control.Days, d => d.DayLabel == "Tuesday").EventLabel;

    [Fact]
    public void ToggleShow_FlipsVisibility()
    {
        var (vm, _, _, _) = Create();
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void FinishDialogue_ShowsControlPage()
    {
        var (vm, _, _, _) = Create();
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Monday 12:00 | Week 1", control.DateHeader);
        Assert.False(vm.IsOnboarding);
    }

    [Fact]
    public void CompletedOnboarding_BootsToControlPage()
    {
        var (vm, _, _, _) = Create(progress: OnboardingState.Fresh with { BirthdayDone = true });
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void DeliveryButton_OpensCareerPage_BackReturns()
    {
        var catalog = new DeliveryCatalog(
            [new DeliveryStore("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50)],
            0.7, 100);
        var (vm, _, _, _) = Create(catalog: catalog);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.True(control.DeliveryCommand.CanExecute(null));

        control.DeliveryCommand.Execute(null);
        var delivery = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        Assert.False(vm.IsOnboarding); // job page, not onboarding
        Assert.Equal("OPEN NEW CAREER", delivery.MainButtonLabel);

        delivery.ToggleOrderCommand.Execute(delivery.Rows[0]);
        Assert.Equal("DELIVER", delivery.MainButtonLabel);

        // Back with a pending cart = CANCEL ORDER: refunds, then exits.
        delivery.BackCommand.Execute(null);
        Assert.Same(control, vm.CurrentScreen);

        // The page and its now-empty cart survive navigation.
        control.DeliveryCommand.Execute(null);
        Assert.Same(delivery, vm.CurrentScreen);
        Assert.Equal("OPEN NEW CAREER", delivery.MainButtonLabel);
    }

    [Fact]
    public void OpenNewCareer_ShowsHubPage_PurchaseBackConnectsBoard()
    {
        // Unity flow: gated board -> OPEN A NEW HUB -> hub page -> buy with
        // CredP -> back lands on a board whose stores are now connected.
        var catalog = new DeliveryCatalog(
            [new DeliveryStore("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries")],
            0.7, 100);
        var hubs = new HubCatalog([
            new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)]),
                new HubCareer("coffee_beans", "COFFEE BEANS", [new HubLevel("CoffeeUs", 100)])])]);
        var (vm, _, _, _) = Create(catalog: catalog, hubs: hubs);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        control.DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        Assert.False(board.HasStores); // the career gate holds until a hub is bought
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        Assert.False(vm.IsOnboarding); // job side, like the board

        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);

        Assert.Same(board, vm.CurrentScreen);
        Assert.True(board.HasStores);
        Assert.Equal("Credibility Points: 0", board.CredPLine);
        Assert.Equal("adenau-post", board.Rows[0].Store.Key);
    }
}
