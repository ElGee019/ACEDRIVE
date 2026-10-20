using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>ViewModel flows run headless against fakes (LSP in action).</summary>
public sealed class MainViewModelTests
{
    internal static readonly OnboardingContent EmptyContent = new([], new Dictionary<string, string[]>(), [], []);
    internal static (MainViewModel Vm, FakeClock Clock, FakeTelemetry Tel, FakeStore Store)
        Create(
            float speedKmh = 90f, OnboardingState? progress = null,
            DeliveryCatalog? catalog = null, HubCatalog? hubs = null, ILog? log = null,
            RoadTestLedger? roads = null, IGameClockStore? clocks = null, int startWeek = 1,
            DateTime? gameStart = null, IRoadTestStore? roadTestStore = null,
            CityEconomy? city = null, ICityStore? cityStore = null,
            DriverWallet? wallet = null, IDriverStore? driverStore = null,
            IFriendshipStore? friendships = null,
            IReadOnlyList<CarSpec>? cars = null, string? carKsId = null,
            string? carPreset = null,
            IBrandMileageStore? brandMileage = null, FakeRivals? rivals = null,
            DeliveryFareTable? deliveryFares = null,
            DriverState? savedDriver = null, Hunger? hunger = null,
            IHungerStateStore? hungerStore = null,
            Garage? garage = null, IGarageStore? garageStore = null,
            BreakdownStrategy? breakdowns = null,
            OnboardingContent? content = null,
            PlayerProfile? profile = null, IProfileStore? profileStore = null,
            AddressMap? addressMap = null, string? detectedName = "ElGee",
            IPreferencesStore? preferencesStore = null,
            PlayerPreferences? preferences = null,
            ICultureService? culture = null,
            PoiGenerator? pois = null,
            ITelemetryHud? hud = null,
            FakeVoiceListener? voice = null,
            Trunk? trunk = null,
            PoiArrivalRouter? poiArrivals = null)
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
        CarIdentity? announced = carKsId is null ? null : new CarIdentity(carKsId, carPreset);
        var brands = new FakeCarBrandSource { CurrentIdentity = announced };
        var profiles = profileStore ?? new FakeProfileStore(profile);
        AddressMap map = addressMap ?? AddressMap.Default;
        var beat = new VoiceBeat(clock);
        var walletInstance = wallet ?? new DriverWallet(savedDriver);
        var garageInstance = garage ?? new Garage(cars ?? TestRoadCars.Pool);
        var friendsInstance = friendships ?? new FakeFriendshipStore();
        var trunkInstance = trunk ?? new Trunk(savedDriver?.Trunk);
        var storeNetwork = new StoreNetwork(StoreCatalog.Default, PoiCatalog.Default);
        var router = poiArrivals ?? new PoiArrivalRouter(
        [
            new StorePoiHandler(
                storeNetwork, trunkInstance, walletInstance, garageInstance,
                friendsInstance, beat),
        ]);
        var vm = new MainViewModel(tel, clock, new GameClock(clock, gameStart ?? clock.UtcNow), new TaxiShift(), store,
            log ?? new NullLogger(), FareTable.Default,
            new TaxiJobProposer(NamePoolTable.Default, seed: 0),
            onboarding, new FakeContentSource(content ?? EmptyContent),
            profiles, map, detectedName,
            voice ?? new FakeVoiceListener(available: false),
            brands, TodoTable.Default, board, career,
            deliveryFares ?? DeliveryFareTable.Default,
            city ?? new CityEconomy(board.Stores, CityRules.Default), cityStore ?? new FakeCityStore(),
            roads ?? TestRoadCars.Ledger(), roadTestStore ?? new FakeRoadTestStore(),
            clocks ?? new FakeGameClockStore(), startWeek,
            beat, walletInstance,
            driverStore ?? new FakeDriverStore(),
            new DailyStreak(clock, savedDriver),
            new OperatingZone(savedDriver, seed: 0),
            friendsInstance, cars ?? TestRoadCars.Pool,
            brandMileage ?? new FakeBrandMileageStore(), rivals ?? new FakeRivals(),
            hunger ?? new Hunger(HungerRules.Default), hungerStore ?? new FakeHungerStore(),
            garageInstance, garageStore ?? new FakeGarageStore(),
            InsuranceRules.Default,
            // Seeded so a test can force a breakdown or keep every trip clean.
            breakdowns ?? new BreakdownStrategy(MaintenanceRules.Default, seed: 0),
            pois ?? new PoiGenerator(map, PoiCatalog.Default, seed: 0),
            preferencesStore ?? new FakePreferencesStore(),
            preferences ?? PlayerPreferences.Default,
            culture ?? new FakeCultureService(),
            hud, trunkInstance, router);
        if (carKsId is not null)
        {
            // The watcher thread lands via the dispatcher, which runs inline
            // headless: the VM now follows the announced car like live.
            brands.Fire("test", announced);
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

    [Fact]
    public void Poll_PushesTheHudSnapshotAndDrivenOdometer()
    {
        var garage = new Garage(TestRoadCars.Pool);
        OwnedCar car = garage.Add(TestRoadCars.Pool[0], week: 1);
        var hud = new FakeTelemetryHud();
        var (vm, clock, tel, _) = Create(garage: garage, hud: hud);
        var track = new TrackInfo("brands_hatch", "indy", 1929.0);
        tel.Next = tel.Next with
        {
            SpeedKmh = 100f,
            NormalizedPosition = 0.25f,
            Track = track,
        };
        clock.Advance(TimeSpan.FromMilliseconds(500));
        vm.Poll();

        Assert.Equal(1, hud.Calls);
        Assert.True(hud.GameRunning);
        Assert.NotNull(hud.Sample);
        Assert.Equal(0.25f, hud.Sample!.NormalizedPosition);
        Assert.Equal(track, hud.Sample.Track);
        // The HUD snapshot is taken after the odometer feed, so it sees the
        // distance this very tick banked into the driven car.
        Assert.True(car.Odometer > 0.0);
        Assert.Equal(car.Odometer, hud.OdometerKm!.Value);
    }

    [Fact]
    public void Poll_PushesTheWaitingHudSnapshot()
    {
        var hud = new FakeTelemetryHud();
        var (vm, _, tel, _) = Create(hud: hud);
        tel.GameRunning = false;
        tel.PhysicsPresent = false;
        vm.Poll();

        Assert.Equal(1, hud.Calls);
        Assert.Null(hud.Sample);
        Assert.False(hud.GameRunning);
    }

    [Fact]
    public void Poll_FlagsALiveCarThatIsNotTheGarageCurrent()
    {
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(
            garage: garage, hud: hud, carKsId: "ks_abarth_695_biposto");

        vm.Poll();

        Assert.True(hud.WrongCar);
        Assert.NotNull(hud.Sample);
    }

    [Fact]
    public void Poll_DoesNotFlagTheGarageCurrent()
    {
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(
            garage: garage, hud: hud, carKsId: "ks_mazda_mx5_na");

        vm.Poll();

        Assert.False(hud.WrongCar);
    }

    [Fact]
    public void Poll_DoesNotFlagAShowroomVariantOfTheGarageCurrent()
    {
        // The intro's grandma-car pick owns the folder row; EVO may spawn one
        // of its mechanical presets. Same car for the career.
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(
            garage: garage, hud: hud, carKsId: "ks_mazda_mx5_na",
            carPreset: "preset_mx5na_mech_2");

        vm.Poll();

        Assert.False(hud.WrongCar);
    }

    [Fact]
    public void Poll_WithoutAKnownCarIdentity_DoesNotFlagAFalseMismatch()
    {
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(garage: garage, hud: hud);

        vm.Poll();

        Assert.False(hud.WrongCar);
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
        var pool = new List<CarSpec>
        {
            new("ks_test_sedan", 2020, "Test", "Sedan", Passengers: 4),
        };
        var (vm, _, _, _) = Create(
            speedKmh: 0f, cars: pool, carKsId: "ks_test_sedan",
            roads: new RoadTestLedger(pool, seed: 0));
        ProposeJob(vm);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        // 43.15 x 4 = 172.60 charge + 10 forfait = 182.60 base,
        // 36.52 tip + 9.13 brand sponsorship (first 8.2 km in
        // the brand: 5%) = 228.25.
        Assert.Equal("€228.25", vm.FareText);
        Assert.Equal(new ReceiptLine("Fare: 4 × €43.15", "€172.60"), receipt.FareLine);
        Assert.True(receipt.HasBrandBonus);
        Assert.Contains("Customers: 4.", receipt.CustomerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Accept_VariantCapacityFollowsThePreset()
    {
        // Two showroom variants in one folder (PORTPLAN 62): the announced
        // mechanical preset resolves its own row, so the 2-seat variant is
        // charged as a 2-seater even though the folder's other row seats 4.
        var pool = new List<CarSpec>
        {
            new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe", Passengers: 4) { Preset = "preset_m2_mech_1" },
            new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe m performance", Passengers: 2) { Preset = "preset_m2_mperf_1" },
        };
        var (vm, _, _, _) = Create(
            speedKmh: 0f, cars: pool, carKsId: "ks_bmw_m2_coupe", carPreset: "preset_m2_mperf_1",
            roads: new RoadTestLedger(pool, seed: 0));
        ProposeJob(vm);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Contains("Customers: 2.", receipt.CustomerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Accept_UnknownCar_RidesSolo()
    {
        // No driving slice: ACCEPT settles the placeholder ride at once
        // (8.2 km, tip eligible, no fine): 53.15 base + 10.63 tip +
        // 2.66 brand sponsorship (first 8.2 km in the brand:
        // 5%) = 66.44 on the receipt screen, banked to the wallet
        // and the driver save.
        var wallet = new DriverWallet();
        var drivers = new FakeDriverStore();
        var (vm, clock, _, _) = Create(speedKmh: 0f, wallet: wallet, driverStore: drivers);
        ProposeJob(vm);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("€66.44", vm.FareText);
        Assert.Contains("1 jobs", vm.ShiftText, StringComparison.Ordinal);
        Assert.Equal(66.44, wallet.Cash);
        Assert.Equal(0.082, wallet.Xp, precision: 9);
        DriverState saved = drivers.Saved[^1];
        Assert.Equal(66.44, saved.Cash);
        Assert.Equal(wallet.Xp, saved.Xp);
        // The settle now carries the streak slice too (SaveDriver): a fresh
        // driver recounts to day 1, and with onboarding incomplete no bonus
        // was granted or stamped.
        Assert.Equal(1, saved.DaysInARow);
        Assert.Equal(clock.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            saved.LastLoginDate);
        Assert.Null(saved.LastDailyBonusDate);
        Assert.Equal(new ReceiptLine("Total earned", "€66.44"), receipt.TotalLine);
        Assert.False(receipt.HasFine);
        Assert.True(receipt.HasBrandBonus);
        Assert.Equal(new ReceiptLine("mazda Sponsorship", "€2.66"), receipt.SponsorshipLine);
        Assert.Contains("0.08", receipt.XpLine, StringComparison.Ordinal);

        // CONTINUE lands back on the control page.
        receipt.ContinueCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void Accept_Standings_RefreshPushAndBank()
    {
        // Unity TaxiJobLifecycleHandler order: the XP banks, the standings
        // refresh against the banked total, the push follows, and the saved
        // driver carries the snapshot for the next session.
        var rivals = new FakeRivals { Result = new RivalsResult(4, 30, 2, 6, false) };
        var wallet = new DriverWallet();
        var drivers = new FakeDriverStore();
        var (vm, _, _, _) = Create(
            speedKmh: 0f, wallet: wallet, driverStore: drivers, rivals: rivals);
        ProposeJob(vm);
        Assert.Equal([wallet.Xp], rivals.RefreshedAt);
        Assert.Equal((wallet.Xp, string.Empty), Assert.Single(rivals.Pushed));
        DriverState saved = drivers.Saved[^1];
        Assert.Equal(66.44, saved.Cash);
        Assert.Equal(wallet.Xp, saved.Xp);
        Assert.Equal(4, saved.RivalsRank);
        Assert.Equal(30, saved.RivalsTotal);
        // The streak slice rides every settle save (SaveDriver).
        Assert.Equal(1, saved.DaysInARow);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Your XP world standings is 4th of 30.", receipt.Standings.Line);
        Assert.Equal("You gained 2 positions.", receipt.Standings.DeltaLine);
    }

    [Fact]
    public void Accept_Standings_PushesProfileDisplayName()
    {
        // The opening screen's confirmed name is what the board sees; before
        // that screen the push stays nameless (covered above).
        var rivals = new FakeRivals();
        var profile = new PlayerProfile("ElGee", "Germany", "Brands", "street-1", 1);
        var (vm, _, _, _) = Create(
            speedKmh: 0f, rivals: rivals, profile: profile);
        ProposeJob(vm);
        var pushed = Assert.Single(rivals.Pushed);
        Assert.Equal("ElGee", pushed.Name);
        Assert.Equal(0.082, pushed.Xp, precision: 9);
    }

    [Fact]
    public void Accept_NoStandingsKnown_BanksNeverSaved()
    {
        // Fresh driver, offline board: 0/0 keeps "never saved", the page
        // shows Unity's em-dash placeholder with an empty delta.
        var rivals = new FakeRivals();
        var wallet = new DriverWallet();
        var drivers = new FakeDriverStore();
        var (vm, _, _, _) = Create(
            speedKmh: 0f, wallet: wallet, driverStore: drivers, rivals: rivals);
        ProposeJob(vm);
        DriverState saved = drivers.Saved[^1];
        Assert.Equal(66.44, saved.Cash);
        Assert.Equal(wallet.Xp, saved.Xp);
        // Fresh driver, offline board: 0/0 keeps "never saved".
        Assert.Equal(0, saved.RivalsRank);
        Assert.Equal(0, saved.RivalsTotal);
        Assert.Equal(1, saved.DaysInARow);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Your XP world standings is —/—.", receipt.Standings.Line);
        Assert.Equal(string.Empty, receipt.Standings.DeltaLine);
        Assert.False(receipt.Standings.Changed);
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
    public void NewSession_ResumesTheSavedCarSpec()
    {
        // Two sessions, two unrelated seeds, one banked pick: the Tuesday
        // button names the same car in both (PORTPLAN 49).
        var bank = new FakeRoadTestStore();
        var (vm, _, _, _) = Create(roads: TestRoadCars.Ledger(seed: 7), roadTestStore: bank);
        vm.Start();
        RoadTestSnapshot saved = Assert.Single(bank.Saved);

        var restarted = TestRoadCars.Ledger(seed: 4242, saved: saved);
        CarSpec car = Assert.IsType<CarSpec>(restarted.Current);
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
    public void SundayToMonday_RollsWeekAndKeepsCarSpec()
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
        // The first-run page opens first (the intro replays); its CONTINUE ->
        // missing content degrades to the control page whose header we read.
        Assert.IsType<PreferencesScreenViewModel>(restarted.CurrentScreen)
            .ContinueCommand.Execute(null);
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
    public void FinishDialogue_ShowsControlPageButLeavesThePanelHidden()
    {
        var (vm, _, _, _) = Create();
        vm.FinishDialogue();

        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Monday 12:00 | Week 1", control.DateHeader);
        Assert.False(vm.IsOnboarding);
        // The birthday hands the screen to the HUD until the player calls the
        // panel back.
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void FinishDialogue_RevealsTheHud()
    {
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(hud: hud);
        Assert.False(hud.Revealed);

        vm.FinishDialogue();

        Assert.True(hud.Revealed);
    }

    [Fact]
    public void ShowDialogue_FillsTheErrandPoiAndRandomAddress()
    {
        var content = new OnboardingContent(
            [], new Dictionary<string, string[]>(), [],
            [new DialogueLine("GRANDMA", "Stop by [POI] on [STREET].")]);
        PointOfInterest expected =
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5).Spawn()!;
        var (vm, _, _, _) = Create(
            content: content,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5));

        vm.ShowDialogue();

        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(
            $"Stop by {expected.Name} on {expected.Address.StreetName} · No. {expected.Address.HouseNumber}.",
            dialogue.Body);
    }

    [Fact]
    public void FinishDialogue_PinsTheErrandOnTheHud()
    {
        var content = new OnboardingContent(
            [], new Dictionary<string, string[]>(), [],
            [new DialogueLine("GRANDMA", "Go.")]);
        PointOfInterest expected =
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5).Spawn()!;
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(
            content: content,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud);

        vm.ShowDialogue();
        vm.FinishDialogue();

        Assert.Equal(expected, hud.Destination);
    }

    [Fact]
    public void EnterRequested_OpensTheStoreInterior()
    {
        var content = new OnboardingContent(
            [], new Dictionary<string, string[]>(), [],
            [new DialogueLine("GRANDMA", "Go.")]);
        PointOfInterest expected =
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5).Spawn()!;
        var log = new TestLog();
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(
            content: content,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud,
            log: log);

        vm.ShowDialogue();
        hud.RaiseEnter();

        var store = Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("MAX'S GROCERY STORE", store.StoreTitle);
        Assert.Contains(
            log.Infos,
            line => line.Contains($"entered {expected.Name}", StringComparison.Ordinal));
    }

    [Fact]
    public void EnterRequested_WhenHidden_PopsTheOverlayOnTheStore()
    {
        var content = new OnboardingContent(
            [], new Dictionary<string, string[]>(), [],
            [new DialogueLine("GRANDMA", "Go.")]);
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(
            content: content,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud);

        vm.ShowDialogue();
        vm.ToggleShowCommand.Execute(null);
        Assert.False(vm.IsShown);

        hud.RaiseEnter();

        Assert.True(vm.IsShown);
        var store = Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);
        store.BackCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void StorePurchase_BanksTheTrunkInTheDriverSave()
    {
        var content = new OnboardingContent(
            [], new Dictionary<string, string[]>(), [],
            [new DialogueLine("GRANDMA", "Go.")]);
        var car = new CarSpec(
            "ks_test_grocery", 2020, "Test", "Grocery", Passengers: 1,
            TrunkLitres: 60);
        var pool = new[] { car };
        var garage = new Garage(pool);
        garage.Add(car, week: 1);
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        var drivers = new FakeDriverStore();
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(
            content: content,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, wallet: wallet, cars: pool, garage: garage,
            driverStore: drivers);

        vm.ShowDialogue();
        hud.RaiseEnter();
        var store = Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);
        StoreItemRowViewModel row = store.Items.First(
            item => item.Item.Name == "MAPLE SYRUP");
        row.SelectCommand.Execute(null);
        store.BuyCommand.Execute(null);

        Assert.Equal(1000.0 - 45.99, wallet.Cash, 2);
        Assert.Contains(drivers.Saved, saved =>
            saved.Trunk is { Count: > 0 }
            && saved.Trunk.Any(item => item.Name == "MAPLE SYRUP"));
        Assert.True(store.BoughtVisible);
    }

    [Fact]
    public void VoiceEnter_RequestsTheHudEntryAtTheDestination()
    {
        var hud = new FakeTelemetryHud { AtDestination = true };
        var mic = new FakeVoiceListener();
        _ = Create(hud: hud, voice: mic);

        mic.Fire("enter", isFinal: true);

        Assert.Equal(1, hud.EnterRequests);
    }

    [Fact]
    public void VoiceEnter_IgnoredBeforeArrival()
    {
        var hud = new FakeTelemetryHud();
        var mic = new FakeVoiceListener();
        _ = Create(hud: hud, voice: mic);

        mic.Fire("enter", isFinal: true);

        Assert.Equal(0, hud.EnterRequests);
    }

    [Fact]
    public void CompletedOnboarding_BootsToTheFirstRunPageThenControl()
    {
        // The unit/language page is content-free, so it opens even with no
        // brand data; continuing then degrades straight to the control page
        // instead of trapping the player on a page with nothing to tap.
        var (vm, _, _, _) = Create(progress: OnboardingState.Fresh with { BirthdayDone = true });
        Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen).ContinueCommand.Execute(null);
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

    [Fact]
    public void Deliver_JumpsStraightToTheDeliveryReceipt()
    {
        // Like the taxi ACCEPT skip (PORTPLAN 16): with no driving slice the
        // DELIVER tap settles the whole run at once — goods money in and
        // out, the hub ramp earns its CredP, the hub owner gains
        // familiarity — and the receipt shows; CONTINUE returns to the
        // delivery board the run came from.
        var catalog = new DeliveryCatalog(
            [new DeliveryStore("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries")],
            0.7, 100);
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])],
            CredPRamp.UnityDefault, ["Elena Marchetti"]);
        var wallet = new DriverWallet();
        var friends = new FakeFriendshipStore();
        var (vm, _, _, _) = Create(
            catalog: catalog, hubs: hubs, wallet: wallet, speedKmh: 0f,
            friendships: friends);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        control.DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);

        // Buy the hub, then stage the full-restock order (28 units @ 2.50).
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);
        Assert.Same(board, vm.CurrentScreen);
        Assert.True(board.HasStores);
        board.ToggleOrderCommand.Execute(board.Rows[0]);
        board.MainButtonCommand.Execute(null);

        var receipt = Assert.IsType<DeliveryReceiptScreenViewModel>(vm.CurrentScreen);
        // 63.00 payout + 129.40 fare - 49.00 wholesale = the profit delta.
        Assert.Equal(143.40, wallet.Cash);
        Assert.Equal(0.082, wallet.Xp, precision: 9);
        // The ramp paid the producing hub's block; the familiarity went to
        // its owner, assigned at purchase (Unity GetHubOwnerName).
        Assert.Equal(["Elena Marchetti"], friends.Names);
        Assert.True(receipt.HasFriendship);
        Assert.Equal("New familiarity with Elena: 1. Still a stranger though.", receipt.FriendshipLine);
        Assert.True(receipt.HasCredP);
        Assert.Equal("Credibility Points earned: 1", receipt.CredPEarnedLine);
        Assert.Equal("Total Credibility Points: 1", receipt.TotalCredPLine);

        // CONTINUE returns to the board, cart emptied by the settle.
        receipt.ContinueCommand.Execute(null);
        Assert.Same(board, vm.CurrentScreen);
        Assert.Equal("OPEN NEW CAREER", board.MainButtonLabel);
    }

    [Fact]
    public void CarHaulTap_SettlesStraightIntoTheReceipt()
    {
        // Like the taxi ACCEPT skip (PORTPLAN 20): with no driving slice
        // the CAR HAUL tap settles a placeholder haul at once — the
        // hauled car is the driven one, its pool row carries the real
        // class band — and the receipt shows; CONTINUE returns to the
        // control page.
        var pool = new List<CarSpec>
        {
            new("ks_test_hatch", 2015, "Test", "Hatch", Passengers: 2, Class: 15),
        };
        var wallet = new DriverWallet();
        var (vm, _, _, _) = Create(
            cars: pool, carKsId: "ks_test_hatch", roads: new RoadTestLedger(pool, seed: 0),
            wallet: wallet, speedKmh: 0f);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        control.HaulCommand.Execute(null);
        var receipt = Assert.IsType<CarHaulReceiptScreenViewModel>(vm.CurrentScreen);
        // Class 15 -> tier 6: 0.83 * (50 rating * 6 / 100) = 2.49/min,
        // the 30-minute placeholder leg earns 74.70. No XP rides a haul.
        Assert.Equal(74.70, wallet.Cash);
        Assert.Equal(0.0, wallet.Xp);
        Assert.Equal("TEST HATCH", receipt.CarLine);
        Assert.Equal("The current class is Tier 6", receipt.TierLine);
        Assert.Equal("Wallet: €74.70", receipt.WalletLine);

        // CONTINUE hands back to a fresh control page (taxi-receipt rule).
        receipt.ContinueCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void CarHaulTap_UnknownCar_FallsBackCleanly()
    {
        // No announced identity: the receipt names the customer's car
        // instead and prices the lowest band rather than crashing.
        var wallet = new DriverWallet();
        var (vm, _, _, _) = Create(wallet: wallet, speedKmh: 0f);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        control.HaulCommand.Execute(null);
        var receipt = Assert.IsType<CarHaulReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("the customer's new car", receipt.CarLine);
        // Lowest band (placeholder class 1): 0.83 * (50 * 1 / 100) = 0.415
        // cents-rewritten to a 0.42 rate, 30 minutes earn 12.45.
        Assert.Equal(12.45, wallet.Cash);
        Assert.Equal(0.0, wallet.Xp);
        Assert.Equal("The current class is Tier 1", receipt.TierLine);
        Assert.Equal("Resulting in a rate of €0.42/min", receipt.RateLine);
    }

    [Fact]
    public void StreakGrant_OnFirstLaunchOfDay_LandsInWalletAndSave()
    {
        // Item 47: the boot path (Unity MapScreen.Show) recounts the streak,
        // then grants the bonus once behind onboarding — 4 CR for day 3.
        var onboarding = new OnboardingState("Audi", "ks_mazda_mx5_na", BirthdayDone: true);
        var saved = new DriverState(10.0, 0.0, DaysInARow: 2, LastLoginDate: "2026-10-11");
        var wallet = new DriverWallet(saved);
        var drivers = new FakeDriverStore(saved);
        var (vm, clock, _, _) = Create(progress: onboarding, wallet: wallet, driverStore: drivers,
            savedDriver: saved);
        // FakeClock default day = 2026-10-12: yesterday's login continues the
        // streak to day 3, whose bonus is 4 CR.
        Assert.Equal(14.0, wallet.Cash);
        DriverState granted = Assert.Single(drivers.Saved);
        Assert.Equal(3, granted.DaysInARow);
        Assert.Equal(clock.UtcNow.ToString("yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture), granted.LastLoginDate);
        Assert.Equal("2026-10-12", granted.LastDailyBonusDate);
        Assert.True(vm.OnboardingDone);
    }

    [Fact]
    public void StreakGrant_BeforeOnboarding_HoldsBack()
    {
        // Unity's firstSceneComplete gate: no bonus while the intro still
        // runs, even when the streak day is due.
        var saved = new DriverState(10.0, 0.0, DaysInARow: 2, LastLoginDate: "2026-10-11");
        var wallet = new DriverWallet(saved);
        var drivers = new FakeDriverStore(saved);
        _ = Create(wallet: wallet, driverStore: drivers, savedDriver: saved);
        Assert.Equal(10.0, wallet.Cash);
        Assert.Empty(drivers.Saved);
    }

    [Fact]
    public void ZoneJobCount_GrowsAtTheSettles_AndExpandsOnTheCurve()
    {
        // Two settled taxi jobs expand the zone once (Unity threshold 2 for
        // the first expansion); the home street already counts from boot, so
        // the expanded zone answers 2 streets — the bank's interest factor
        // reads exactly this count (item 58 + 48).
        var wallet = new DriverWallet();
        var drivers = new FakeDriverStore();
        var (vm, _, _, _) = Create(speedKmh: 0f, wallet: wallet, driverStore: drivers);
        vm.FinishDialogue();
        Assert.Empty(drivers.Saved); // FinishDialogue saves onboarding only

        ProposeJob(vm);
        DriverState after = drivers.Saved[^1];
        Assert.Equal(1, after.ExpansionJobs);
        Assert.Equal(0, after.ExpansionCount);
        Assert.Equal([OperatingZone.HomeStreetId], after.StreetIds);

        ProposeJob(vm);
        after = drivers.Saved[^1];
        // The expansion fired on this second job: the counter resets, the
        // count grows and s1 joins behind the home street.
        Assert.Equal(0, after.ExpansionJobs);
        Assert.Equal(1, after.ExpansionCount);
        Assert.Equal(["home", "s1"], after.StreetIds);
        Assert.Equal(0.0, after.ZoneKm); // interim: no street metadata
    }

    [Fact]
    public void HaulSettle_NeverCountsTheZone()
    {
        // Unity parity: car-hauling pays cash alone — no expansion counter,
        // no bank interest (48). The settle also writes no driver save, so
        // nothing rides the counters in from the tap.
        var wallet = new DriverWallet();
        var drivers = new FakeDriverStore();
        var (vm, _, _, _) = Create(wallet: wallet, driverStore: drivers, speedKmh: 0f);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        int savesBefore = drivers.Saved.Count;
        control.HaulCommand.Execute(null);
        _ = Assert.IsType<CarHaulReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(12.45, wallet.Cash); // the haul paid
        Assert.Equal(1000.0, wallet.BankBalance); // interest never accrued
        Assert.Equal(savesBefore, drivers.Saved.Count);
    }

    [Fact]
    public void Settle_AccruesInterestIntoTheBank()
    {
        // Unity ApplyForCompletion at the taxi settle: the fresh driver
        // answers rate 1 (home street × day one), so the saldo compounds
        // 1000 → 1000.1 while the ride's fare lands in cash.
        var wallet = new DriverWallet();
        var drivers = new FakeDriverStore();
        var (vm, _, _, _) = Create(speedKmh: 0f, wallet: wallet, driverStore: drivers);
        vm.FinishDialogue();
        ProposeJob(vm);
        Assert.Equal(1000.1, wallet.BankBalance);
        DriverState saved = drivers.Saved[^1];
        Assert.Equal(1000.1, saved.Bank);
        Assert.False(saved.HasEnteredBank); // not visited anything yet
    }

    [Fact]
    public void BankCommand_OpensTheBankScreen_AndBackReturns()
    {
        // Temporary entry (48): the BANK verb opens the withdraw screen and
        // stamps the visit; CONTINUE hands back to the control page.
        var wallet = new DriverWallet();
        var (vm, _, _, _) = Create(speedKmh: 0f, wallet: wallet);
        vm.FinishDialogue();
        vm.BankCommand.Execute(null);
        var bank = Assert.IsType<BankScreenViewModel>(vm.CurrentScreen);
        Assert.True(wallet.HasEnteredBank);
        Assert.Equal("Current Bank Saldo: CR1000.00", bank.SaldoLine);
        bank.BackCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void BankWithdraw_PersistsThroughTheSharedSave()
    {
        // Unity SaveManager.Save on withdraw: the moved CR reaches the
        // driver save with visit stamp, the saldo never forks on a screen.
        var saved = new DriverState(0, 0, Bank: 1200.0);
        var wallet = new DriverWallet(saved);
        var drivers = new FakeDriverStore(saved);
        var (vm, _, _, _) = Create(speedKmh: 0f, wallet: wallet, driverStore: drivers,
            savedDriver: saved);
        vm.FinishDialogue();
        vm.BankCommand.Execute(null);
        var bank = Assert.IsType<BankScreenViewModel>(vm.CurrentScreen);
        bank.AmountText = "150";
        bank.WithdrawCommand.Execute(null);
        DriverState banked = drivers.Saved[^1];
        Assert.Equal(1050.0, banked.Bank);
        Assert.Equal(150.0, banked.Cash);
        Assert.True(banked.HasEnteredBank);
    }

    [Fact]
    public void SaveHunger_BanksTheReading()
    {
        // Unity PlayerData.hunger survives restarts; the window close routes
        // through SaveHunger like the city and the clock.
        var hungerStateStore = new FakeHungerStore();
        var (vm, _, _, _) = Create(hungerStore: hungerStateStore);
        vm.SaveHunger();

        HungerState saved = Assert.Single(hungerStateStore.Saved);
        Assert.Equal(0, saved.Hunger);
        Assert.False(saved.ThresholdTriggered);
    }

    [Fact]
    public void TaxiAccept_DirtiesTheCarDriven()
    {
        // PORTPLAN 33, Unity TaxiJobLifecycleHandler's dropoff block: every
        // completed ride is +1 on the car driven now, banked with the settle.
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var store = new FakeGarageStore();
        var (vm, _, _, _) = Create(speedKmh: 0f, garage: garage, garageStore: store);

        ProposeJob(vm);

        Assert.Equal(1, garage.Current!.Filth.Value);
        Assert.Equal(1, store.Load()!.Cars!.Single().Filth);
    }

    [Fact]
    public void TaxiAccept_ClampsTheDirtAtTheCap()
    {
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1).Filth.Add(24);
        var (vm, _, _, _) = Create(speedKmh: 0f, garage: garage);

        ProposeJob(vm);

        Assert.Equal(25, garage.Current!.Filth.Value);
    }

    [Fact]
    public void TaxiAccept_FilthyCar_RefusesTheRideAndLosesFriendship()
    {
        // Unity's pickup refusal at filth 25: no ride, no receipt, familiarity
        // with the drawn customer drops 1 and the page shows the reason. The
        // concentration -1 stays parked for item 06.
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1).Filth.Add(25);
        var friends = new FakeFriendshipStore();
        var (vm, _, _, _) = Create(speedKmh: 0f, garage: garage, friendships: friends);

        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        proposal.AcceptCommand.Execute(null);

        Assert.Same(proposal, vm.CurrentScreen); // no job, no receipt
        Assert.True(proposal.HasRefusal);
        (string owner, int delta) = Assert.Single(friends.Deltas);
        Assert.Equal(-1, delta);
        Assert.Contains(owner.Split(' ')[0], proposal.RefusalText, StringComparison.Ordinal);
        Assert.Equal(0, friends.GetLevel(owner)); // clamped, never negative
        Assert.Equal(25, garage.Current!.Filth.Value); // the refusal adds no dirt

        // A second tap on the same proposal repeats the message, never the
        // penalty (Unity's _tooDirtyArrived guard).
        proposal.AcceptCommand.Execute(null);
        Assert.Same(proposal, vm.CurrentScreen);
        Assert.Single(friends.Deltas);
    }

    [Fact]
    public void DeliveryRun_DirtiesTheCarDriven()
    {
        // Unity DeliveryJobHandler's store arrival, the delivery twin: a
        // completed run is +1 on the car driven now, banked with the settle.
        var catalog = new DeliveryCatalog(
            [new DeliveryStore("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries")],
            0.7, 100);
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])],
            CredPRamp.UnityDefault, ["Elena Marchetti"]);
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var store = new FakeGarageStore();
        var (vm, _, _, _) = Create(
            catalog: catalog, hubs: hubs, speedKmh: 0f,
            garage: garage, garageStore: store);
        vm.FinishDialogue();
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen).DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);
        board.ToggleOrderCommand.Execute(board.Rows[0]);
        board.MainButtonCommand.Execute(null);

        Assert.IsType<DeliveryReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(1, garage.Current!.Filth.Value);
        Assert.Equal(1, store.Load()!.Cars!.Single().Filth);
    }

    [Fact]
    public void CarWashCommand_OpensTheWashPage_AndBanksTheWash()
    {
        // The temporary entry (PORTPLAN 33): the page spends the flat price
        // and clears the reading; the host banks the cash and the garage.
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1).Filth.Add(6);
        var garageStore = new FakeGarageStore();
        var wallet = new DriverWallet(new DriverState(500.0, 0.0));
        var drivers = new FakeDriverStore(new DriverState(500.0, 0.0));
        var (vm, _, _, _) = Create(
            speedKmh: 0f, garage: garage, garageStore: garageStore,
            wallet: wallet, driverStore: drivers);
        vm.FinishDialogue();

        vm.CarWashCommand.Execute(null);
        var wash = Assert.IsType<CarWashScreenViewModel>(vm.CurrentScreen);
        wash.WashCommand.Execute(null);

        Assert.Equal(0, garage.Current!.Filth.Value);
        Assert.Equal(450.0, wallet.Cash);
        Assert.Equal(0, garageStore.Load()!.Cars!.Single().Filth);
        Assert.Equal(450.0, drivers.Saved[^1].Cash);
        Assert.Equal("Removed 6 filth for CR 50.00", wash.ResultLine);

        wash.BackCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }
}
