using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>ViewModel flows run headless against fakes (LSP in action).</summary>
public sealed class MainViewModelTests
{
    // A returning tour whose Max visit is already banked: later-leg tests can
    // skip the store without relying on a persistent maple-syrup item.
    private static OnboardingState ErrandDone =>
        new(null, null, BirthdayDone: true, MaxErrandDone: true);

    // The walk's closing car page needs a car to offer; every other test drives
    // the earlier pages or a save that is already past the introduction, so
    // only the closing-page tests hand this content in.
    private static readonly OnboardingContent WalkWithCar = new(
        [], new Dictionary<string, string[]>(),
        [new GrandmaCar("ks_mazda_mx5_na", "Mazda MX-5 NA", 1990, 12000, null)],
        [new DialogueLine("GRANDMA", "Go.")], [], [], []);
    internal static TestSessionResult Create(
        float speedKmh = 90f, OnboardingState? progress = null,
        DeliveryCatalog? catalog = null, HubCatalog? hubs = null, ILog? log = null,
        DeliveryCareer? career = null,
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
        Random? rng = null,
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
        IReadOnlyList<IPoiArrivalHandler>? poiHandlers = null,
        PoiDirectory? poiDirectory = null,
        IPoiDirectoryStore? poiStore = null,
        PoiSpawner? poiSpawner = null,
        JobLedger? jobs = null,
        FakeOnboardingStore? onboarding = null,
        TaxiRules? taxiRules = null,
        ICustomerRequestRoller? requests = null,
        CustomerRequestTable? requestTable = null,
        PolicePatrolPlanner? police = null,
        ICarRaritySource? rarity = null,
        IPlayerTelemetry? telemetry = null,
        ShiftRecord? shifts = null,
        IShiftStandings? shiftStandings = null,
        IPlayerNameRegistry? playerNames = null,
        TaxiShift? shift = null, IShiftStore? shiftStore = null,
        FakeSaveDataResetter? saveResets = null,
        FakeAppRestarter? restarter = null,
        StoreCatalog? storeCatalog = null,
        FakeClock? clock = null, FakeCarBrandSource? carBrands = null,
        FakeTelemetry? physics = null) =>
        TestSession.Create(new TestSessionOptions
        {
            SpeedKmh = speedKmh,
            Progress = progress,
            Catalog = catalog,
            Hubs = hubs,
            Log = log,
            Career = career,
            Roads = roads,
            Clocks = clocks,
            StartWeek = startWeek,
            GameStart = gameStart,
            RoadTestStore = roadTestStore,
            City = city,
            CityStore = cityStore,
            Wallet = wallet,
            DriverStore = driverStore,
            Friendships = friendships,
            Cars = cars,
            CarKsId = carKsId,
            CarPreset = carPreset,
            BrandMileage = brandMileage,
            Rivals = rivals,
            DeliveryFares = deliveryFares,
            SavedDriver = savedDriver,
            Hunger = hunger,
            HungerStore = hungerStore,
            Garage = garage,
            GarageStore = garageStore,
            Breakdowns = breakdowns,
            Rng = rng,
            Content = content,
            Profile = profile,
            ProfileStore = profileStore,
            AddressMap = addressMap,
            DetectedName = detectedName,
            PreferencesStore = preferencesStore,
            Preferences = preferences,
            Culture = culture,
            Pois = pois,
            Hud = hud,
            Voice = voice,
            Trunk = trunk,
            PoiHandlers = poiHandlers,
            PoiDirectory = poiDirectory,
            PoiStore = poiStore,
            PoiSpawner = poiSpawner,
            Jobs = jobs,
            Onboarding = onboarding,
            TaxiRules = taxiRules,
            Requests = requests,
            RequestTable = requestTable,
            Police = police,
            Rarity = rarity,
            Telemetry = telemetry,
            Shifts = shifts,
            ShiftStandings = shiftStandings,
            PlayerNames = playerNames,
            Shift = shift,
            ShiftStore = shiftStore,
            SaveResets = saveResets,
            Restarter = restarter,
            StoreCatalog = storeCatalog,
            Clock = clock,
            CarBrands = carBrands,
            Physics = physics,
        });

    // The production way into a business: HOME → ADDRESSES, pin the row, then
    // ENTER on the companion while parked at the cached destination.
    internal static T OpenPoi<T>(
        MainViewModel vm, FakeTelemetryHud hud, string name) where T : class
    {
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        menu.HomeCommand.Execute(null);
        var home = Assert.IsType<HomeScreenViewModel>(vm.CurrentScreen);
        home.AddressesCommand.Execute(null);
        var addresses = Assert.IsType<AddressesScreenViewModel>(vm.CurrentScreen);
        AddressRow row = Assert.Single(addresses.Rows, r => r.Name == name);
        addresses.SelectCommand.Execute(row);
        Assert.Equal(row.Poi, hud.CurrentDestination);
        hud.RaiseEnter();
        return Assert.IsType<T>(vm.CurrentScreen);
    }

    // Delta assertions need a known base: a brand-new wallet now opens on
    // DriverWallet.StartingCash, so the money tests start from an empty one.
    private static DriverWallet EmptyWallet() => new(new DriverState(0.0, 0.0));

    [Fact]
    public void SettingsReset_TwoTaps_WipesSavesThenRestarts()
    {
        var resets = new FakeSaveDataResetter();
        var restarter = new FakeAppRestarter();
        var (vm, _, _, _) = Create(
            progress: OnboardingState.Fresh with { BirthdayDone = true },
            saveResets: resets, restarter: restarter);
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        menu.SettingsCommand.Execute(null);
        var settings = Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);
        Assert.True(settings.CanReset);

        settings.ResetCommand.Execute(null);

        Assert.Equal(0, resets.Calls);
        Assert.Equal(0, restarter.Calls);

        settings.ResetCommand.Execute(null);

        Assert.Equal(1, resets.Calls);
        Assert.Equal(1, restarter.Calls);
        // The window's close path must not bank the old career back to disk.
        Assert.True(vm.SkipShutdownSave);
    }

    [Fact]
    public void SettingsReset_WipeFailure_KeepsTheSessionAndNamesTheFile()
    {
        var resets = new FakeSaveDataResetter
        {
            Report = new SaveResetReport("driver.json", SaveResetBlock.InUse),
        };
        var restarter = new FakeAppRestarter();
        var (vm, _, _, _) = Create(
            progress: OnboardingState.Fresh with { BirthdayDone = true },
            saveResets: resets, restarter: restarter);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen).SettingsCommand
            .Execute(null);
        var settings = Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);

        settings.ResetCommand.Execute(null);
        settings.ResetCommand.Execute(null);

        Assert.Equal(1, resets.Calls);
        Assert.Equal(0, restarter.Calls);
        Assert.False(vm.SkipShutdownSave);
        // The page names the file to unblock; the old generic line sent the
        // player to the overlay that had just refused the wipe.
        Assert.Equal(
            "Could not reset the game: driver.json is open in another program. " +
            "Close it and try again.",
            settings.ResetStatus);
    }

    [Fact]
    public void SettingsReset_RestartFailure_StillSkipsTheShutdownSave()
    {
        var resets = new FakeSaveDataResetter();
        var restarter = new FakeAppRestarter { Result = false };
        var (vm, _, _, _) = Create(
            progress: OnboardingState.Fresh with { BirthdayDone = true },
            saveResets: resets, restarter: restarter);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen).SettingsCommand
            .Execute(null);
        var settings = Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);

        settings.ResetCommand.Execute(null);
        settings.ResetCommand.Execute(null);

        Assert.Equal(1, resets.Calls);
        Assert.Equal(1, restarter.Calls);
        // The wipe is final: a failed restart must not bank the old career.
        Assert.True(vm.SkipShutdownSave);
        Assert.Equal(
            "The saves are deleted, but AceDrive could not start again. " +
            "Close AceDrive and open it again.",
            settings.ResetStatus);
    }

    [Fact]
    public void SettingsWithoutResetServices_HidesTheResetAction()
    {
        var (vm, _, _, _) = Create(
            progress: OnboardingState.Fresh with { BirthdayDone = true });
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen).SettingsCommand
            .Execute(null);

        Assert.False(
            Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen).CanReset);
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
    private static TaxiJobProposalViewModel ProposeJob(MainViewModel vm) =>
        TaxiFlow.Propose(vm);

    [Fact]
    public void Accept_CapacityFollowsTheDrivenCar()
    {
        // Unity scales the distance charge by passengerCapacity: a 4-seat
        // car runs the same 8.2 km ride at 4x the charge.
        var pool = new List<CarSpec>
        {
            new("ks_test_sedan", 2020, "Test", "Sedan", Passengers: 4),
        };
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, cars: pool, carKsId: "ks_test_sedan",
            roads: new RoadTestLedger(pool, seed: 0),
            requests: new FakeCustomerRequestRoller
            {
                Result = CustomerRequestTestData.PassiveRequest,
            });
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        // 43.15 x 4 = 172.60 charge + 10 forfait = 182.60 base,
        // 36.52 tip + 9.13 brand sponsorship (first 8.2 km in
        // the brand: 5%) = 228.25, plus the 99% starter bonus = 454.22.
        Assert.Equal("€454.22", vm.FareText);
        Assert.Equal(new ReceiptLine("Fare: 4 × €43.15", "€172.60"), receipt.FareLine);
        Assert.True(receipt.HasBrandBonus);
        Assert.Contains("Customers: 4.", receipt.CustomerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void TaxiReceipt_KeepsThePanelUpPastTheIdleWindow()
    {
        // The fare breakdown waits for CONTINUE; the 15 s auto-hide must not
        // close the panel while the player reads it.
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f,
            requests: new FakeCustomerRequestRoller
            {
                Result = CustomerRequestTestData.PassiveRequest,
            });
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.True(vm.IsShown);

        clock.Advance(TimeSpan.FromSeconds(16));
        vm.Poll();

        Assert.True(vm.IsShown);
        Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
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
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, cars: pool, carKsId: "ks_bmw_m2_coupe", carPreset: "preset_m2_mperf_1",
            roads: new RoadTestLedger(pool, seed: 0));
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Contains("Customers: 2.", receipt.CustomerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Accept_UnknownCar_RidesSolo()
    {
        // A live 8.2 km ride, tip eligible, no fine: 53.15 base + 10.63 tip +
        // 2.66 brand sponsorship (first 8.2 km in the brand: 5%) = 66.44,
        // plus the 99% starter bonus (1.929 km zone) = 132.22 on the receipt
        // screen, banked to the wallet and the driver save.
        var wallet = EmptyWallet();
        var drivers = new FakeDriverStore();
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, wallet: wallet, driverStore: drivers,
            requests: new FakeCustomerRequestRoller
            {
                Result = CustomerRequestTestData.PassiveRequest,
            });
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("€132.22", vm.FareText);
        Assert.Contains("1 jobs", vm.ShiftText, StringComparison.Ordinal);
        Assert.Equal(132.22, wallet.Cash);
        Assert.Equal(0.082, wallet.Xp, precision: 9);
        DriverState saved = drivers.Saved[^1];
        Assert.Equal(132.22, saved.Cash);
        Assert.Equal(wallet.Xp, saved.Xp);
        // The settle now carries the streak slice too (SaveDriver): a fresh
        // driver recounts to day 1, and with onboarding incomplete no bonus
        // was granted or stamped.
        Assert.Equal(1, saved.DaysInARow);
        Assert.Equal(clock.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            saved.LastLoginDate);
        Assert.Null(saved.LastDailyBonusDate);
        Assert.Equal(new ReceiptLine("Total earned", "€132.22"), receipt.TotalLine);
        Assert.False(receipt.HasFine);
        Assert.True(receipt.HasBrandBonus);
        Assert.Equal(new ReceiptLine("mazda Sponsorship", "€2.66"), receipt.SponsorshipLine);
        // The seeded 1.929 km zone pays 99% of the settled total.
        Assert.True(receipt.HasStarterBonus);
        Assert.Equal(
            new ReceiptLine("Business Starter Bonus 99.0%", "€65.78"),
            receipt.StarterBonusLine);
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
        var wallet = EmptyWallet();
        var drivers = new FakeDriverStore();
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, wallet: wallet, driverStore: drivers, rivals: rivals,
            requests: new FakeCustomerRequestRoller
            {
                Result = CustomerRequestTestData.PassiveRequest,
            });
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        Assert.Equal([wallet.Xp], rivals.RefreshedAt);
        Assert.Equal((wallet.Xp, string.Empty), Assert.Single(rivals.Pushed));
        DriverState saved = drivers.Saved[^1];
        Assert.Equal(132.22, saved.Cash);
        Assert.Equal(wallet.Xp, saved.Xp);
        Assert.Equal(4, saved.RivalsRank);
        Assert.Equal(30, saved.RivalsTotal);
        // The streak slice rides every settle save (SaveDriver).
        Assert.Equal(1, saved.DaysInARow);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Your Global XP Rank is 4th of 30.", receipt.Standings.Line);
        Assert.Equal("You gained 2 positions.", receipt.Standings.DeltaLine);
        Assert.Equal(1, receipt.Standings.PulseSignal);
    }

    [Fact]
    public void Accept_Standings_PushesProfileDisplayName()
    {
        // The opening screen's confirmed name is what the board sees; before
        // that screen the push stays nameless (covered above).
        var rivals = new FakeRivals();
        var profile = new PlayerProfile("ElGee", "Germany", "Brands", "street-1", 1);
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, rivals: rivals, profile: profile);
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
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
        var wallet = EmptyWallet();
        var drivers = new FakeDriverStore();
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, wallet: wallet, driverStore: drivers, rivals: rivals,
            requests: new FakeCustomerRequestRoller
            {
                Result = CustomerRequestTestData.PassiveRequest,
            });
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        DriverState saved = drivers.Saved[^1];
        Assert.Equal(132.22, saved.Cash);
        Assert.Equal(wallet.Xp, saved.Xp);
        // Fresh driver, offline board: 0/0 keeps "never saved".
        Assert.Equal(0, saved.RivalsRank);
        Assert.Equal(0, saved.RivalsTotal);
        Assert.Equal(1, saved.DaysInARow);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Your Global XP Rank is —/—.", receipt.Standings.Line);
        Assert.Equal(string.Empty, receipt.Standings.DeltaLine);
        Assert.False(receipt.Standings.Changed);
        Assert.Equal(0, receipt.Standings.PulseSignal);
    }

    [Fact]
    public void Accept_SettlesFriendship_AndRepeatRidesGrowIt()
    {
        // First ride with a stranger lands familiarity 1 and the receipt
        // box shows the stranger sentence.
        var friends = new FakeFriendshipStore();
        var (vm, clock, tel, _) = Create(speedKmh: 0f, friendships: friends);
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
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
        var session = Create(speedKmh: 0f);
        var (vm, _, _, _) = session;
        var control = FinishIntro(session);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);

        // The tap owns the shift (Unity OnStartTaxiShift stamps it started
        // before the job shows), and lands on the proposal page.
        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        Assert.Equal("CONTINUE TAXI SHIFT", control.TaxiLabel);
        Assert.Contains("left", vm.ShiftText, StringComparison.Ordinal);
        Assert.True(control.VoiceActions.ContainsKey("CONTINUE TAXI SHIFT"));

        // ACCEPT commits the job and steps the panel aside: the HUD owns the
        // drive to the pickup, and the control page's job line names both
        // addresses.
        proposal.AcceptCommand.Execute(null);
        Assert.Same(proposal, vm.CurrentScreen);
        Assert.False(vm.IsShown);
        Assert.True(vm.PickupCommand.CanExecute(null));
        Assert.Contains("To pickup", vm.JobLine, StringComparison.Ordinal);
        Assert.Contains("Street 1", vm.JobLine, StringComparison.Ordinal);
        Assert.Contains("Street 2", vm.JobLine, StringComparison.Ordinal);
    }

    [Fact]
    public void JobFlow_PinsThePickupThenTheDropoff_AndSettlesFromTheHud()
    {
        var hud = new FakeTelemetryHud();
        var pool = new List<CarSpec>
        {
            new("ks_test_sedan", 2020, "Test", "Sedan", Passengers: 4),
        };
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, hud: hud, cars: pool, carKsId: "ks_test_sedan");

        TaxiJobProposalViewModel proposal = TaxiFlow.Propose(vm);
        string customer = proposal.Phrase[..proposal.Phrase.IndexOf('\n')].TrimEnd('\r');
        string suffix = NamePoolTable.Default.Suffixes
            .First(s => customer.EndsWith($" {s}", StringComparison.Ordinal));
        string baseName = customer[..^(suffix.Length + 1)];
        Assert.NotNull(hud.JobGuidance);
        Assert.Equal("HudJobPickupDrive", hud.JobGuidance!.DriveKey);
        // The line names the whole party; the template adds the door itself.
        Assert.Equal(customer, hud.JobGuidance.TargetName);
        Assert.False(vm.IsShown);

        vm.PickupCommand.Execute(null);
        Assert.Equal("HudJobDropoffDrive", hud.JobGuidance!.DriveKey);
        // The drop-off names the customer alone, without the companion suffix.
        Assert.Equal(baseName, hud.JobGuidance.TargetName);

        TaxiFlow.Drive(vm, clock, tel);
        hud.RaiseJobEnter();

        Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.True(vm.IsShown);
        Assert.Null(hud.JobGuidance);
        // The receipt CONTINUE path lands back on the control page.
        Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen).ContinueCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void TaxiProposal_OneSeatCar_NamesTheCustomerAlone()
    {
        var pool = new List<CarSpec>
        {
            new("ks_test_solo", 2020, "Test", "Solo", Passengers: 1),
        };
        var (vm, _, _, _) = Create(
            speedKmh: 0f, cars: pool, carKsId: "ks_test_solo");

        TaxiJobProposalViewModel proposal = TaxiFlow.Propose(vm);
        string customer = proposal.Phrase[..proposal.Phrase.IndexOf('\n')].TrimEnd('\r');

        // One seat means one customer, so the drawn companion group is never
        // promised on the page (or on the job the host accepts).
        Assert.DoesNotContain(
            NamePoolTable.Default.Suffixes,
            suffix => customer.EndsWith($" {suffix}", StringComparison.Ordinal));
    }

    [Fact]
    public void Pickup_NoRequest_NoTip()
    {
        // Unity parity: a ride with no special request never tips, even when
        // the meter and the sponsorship both settle normally.
        var (vm, clock, tel, _) = Create(speedKmh: 0f);
        TaxiFlow.AcceptAndDrive(vm, clock, tel);

        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.False(receipt.HasTip);
    }

    [Fact]
    public void Pickup_PassiveRequest_AcceptOpensTheDropoffAndTips()
    {
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.PassiveRequest,
        };
        var hud = new FakeTelemetryHud();
        var (vm, clock, tel, _) = Create(speedKmh: 0f, hud: hud, requests: requests);

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));
        // The ask rides the companion, not the panel: the mini window owns it.
        Assert.Same(screen, hud.Request);
        Assert.False(vm.IsShown);
        Assert.Equal("HudJobDropoffDrive", hud.JobGuidance!.DriveKey);

        screen.Options[0].Command.Execute(null);
        screen.BackCommand.Execute(null);

        Assert.Null(hud.Request);
        Assert.Equal(1, hud.ClearRequestCalls);
        Assert.False(vm.IsShown);
        Assert.Equal("HudJobDropoffDrive", hud.JobGuidance!.DriveKey);
        Assert.Equal(1, requests.Calls);

        TaxiFlow.Drive(vm, clock, tel);
        vm.DropoffCommand.Execute(null);

        Assert.True(Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen).HasTip);
    }

    [Fact]
    public void Pickup_PassiveRequest_DeclineDoesNotTip()
    {
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.PassiveRequest,
        };
        var (vm, clock, tel, _) = Create(speedKmh: 0f, requests: requests);

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));
        screen.Options[1].Command.Execute(null);
        screen.BackCommand.Execute(null);

        TaxiFlow.Drive(vm, clock, tel);
        vm.DropoffCommand.Execute(null);

        Assert.False(Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen).HasTip);
    }

    [Fact]
    public void Pickup_RequestWaitsOutTheDropoffTaskText()
    {
        // The player must read where to drive before an ask replaces the HUD
        // line: the drop-off task text goes up first and the rolled request
        // waits out the rule's read-in delay.
        var hud = new FakeTelemetryHud();
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.PassiveRequest,
        };
        var (vm, clock, _, _) = Create(speedKmh: 0f, hud: hud, requests: requests);

        TaxiFlow.Propose(vm);
        vm.PickupCommand.Execute(null);

        Assert.Equal("HudJobDropoffDrive", hud.JobGuidance!.DriveKey);
        Assert.Null(hud.Request);

        clock.Advance(TimeSpan.FromSeconds(TaxiRules.Default.RequestDelaySeconds - 0.1));
        vm.Poll();
        Assert.Null(hud.Request);

        clock.Advance(TimeSpan.FromSeconds(0.2));
        vm.Poll();
        Assert.NotNull(hud.Request);
    }

    [Fact]
    public void Pickup_RequestCountdown_TicksFromPoll()
    {
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.PassiveRequest,
        };
        var (vm, clock, _, _) = Create(speedKmh: 0f, requests: requests);

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));

        clock.Advance(TimeSpan.FromSeconds(1));
        vm.Poll();

        Assert.Equal(18, screen.RemainingSeconds);
    }

    [Fact]
    public void Pickup_Request_AnswersByVoiceOnTheCompanion()
    {
        // The ask is answered on the mini window mid-drive: with the panel
        // hidden, the voice router must reach the companion's request instead
        // of treating the words as chatter for the wake word.
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.PassiveRequest,
        };
        var hud = new FakeTelemetryHud();
        var voice = new FakeVoiceListener();
        var (vm, clock, _, _) = Create(
            speedKmh: 0f, hud: hud, voice: voice, requests: requests);

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));
        Assert.False(vm.IsShown);

        voice.Fire("sure sure", isFinal: true);

        Assert.True(screen.ShowBack);
        Assert.False(screen.TimerVisible);

        screen.BackCommand.Execute(null);
        Assert.Null(hud.Request);
    }

    [Fact]
    public void Pickup_QuizCorrect_TipsAndBanksTheAskedCategory()
    {
        var pool = new List<CarSpec> { CustomerRequestTestData.Car() };
        var garage = new Garage(pool);
        OwnedCar car = garage.Add(pool[0], week: 1);
        var garageStore = new FakeGarageStore();
        var question = new CarStatQuestion(
            CustomerRequestTestData.Weight, "How heavy is this car?",
            [1400.0, 1500.0, 1600.0], CorrectIndex: 1);
        var requests = new FakeCustomerRequestRoller
        {
            Result = new CustomerRequest(
                CustomerRequestKind.CarQuiz, question.Text, question),
        };
        var wallet = new DriverWallet();
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, cars: pool, garage: garage, garageStore: garageStore,
            requests: requests, wallet: wallet,
            requestTable: CustomerRequestTestData.Table(
                categories: CustomerRequestTestData.SixCategories));

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));
        screen.Options[1].Command.Execute(null);
        screen.BackCommand.Execute(null);

        // Unity HandleAnswer: a correct answer lifts off-the-line by 1.
        Assert.Equal(21.0, wallet.Rating.OffTheLine);

        Assert.Equal(pool[0], requests.LastCar);
        Assert.Contains("weight", car.AskedCategories);
        Assert.Contains("weight",
            Assert.Single(garageStore.Load()!.Cars!).AskedCategories!);

        TaxiFlow.Drive(vm, clock, tel);
        vm.DropoffCommand.Execute(null);

        Assert.True(Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen).HasTip);
    }

    [Fact]
    public void Pickup_QuizWrong_DoesNotTip()
    {
        var question = new CarStatQuestion(
            CustomerRequestTestData.Weight, "How heavy is this car?",
            [1400.0, 1500.0, 1600.0], CorrectIndex: 1);
        var requests = new FakeCustomerRequestRoller
        {
            Result = new CustomerRequest(
                CustomerRequestKind.CarQuiz, question.Text, question),
        };
        var wallet = new DriverWallet();
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, requests: requests, wallet: wallet,
            requestTable: CustomerRequestTestData.Table(
                categories: CustomerRequestTestData.SixCategories));

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));
        screen.Options[0].Command.Execute(null);
        screen.BackCommand.Execute(null);

        // A wrong answer drops off-the-line by 1 (Unity HandleAnswer).
        Assert.Equal(19.0, wallet.Rating.OffTheLine);

        TaxiFlow.Drive(vm, clock, tel);
        vm.DropoffCommand.Execute(null);

        Assert.False(Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen).HasTip);
    }

    [Fact]
    public void Pickup_QuizTimeout_NoTipButStillBanksTheAskedCategory()
    {
        var pool = new List<CarSpec> { CustomerRequestTestData.Car() };
        var garage = new Garage(pool);
        OwnedCar car = garage.Add(pool[0], week: 1);
        var question = new CarStatQuestion(
            CustomerRequestTestData.Weight, "How heavy is this car?",
            [1400.0, 1500.0, 1600.0], CorrectIndex: 1);
        var requests = new FakeCustomerRequestRoller
        {
            Result = new CustomerRequest(
                CustomerRequestKind.CarQuiz, question.Text, question),
        };
        var wallet = new DriverWallet();
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, cars: pool, garage: garage, requests: requests,
            wallet: wallet,
            requestTable: CustomerRequestTestData.Table(
                categories: CustomerRequestTestData.SixCategories));

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));

        clock.Advance(TimeSpan.FromSeconds(19));
        vm.Poll();

        Assert.True(screen.ShowBack);
        Assert.Equal("No tip then.", screen.Feedback);
        screen.BackCommand.Execute(null);

        // A timeout costs the same point a wrong answer does (Unity's
        // HandleAnswer runs the null branch for an expiry).
        Assert.Equal(19.0, wallet.Rating.OffTheLine);

        Assert.Contains("weight", car.AskedCategories);
        TaxiFlow.Drive(vm, clock, tel);
        vm.DropoffCommand.Execute(null);

        Assert.False(Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen).HasTip);
    }

    private static readonly PoliceRules TestPoliceRules = new(
        PatrolsPerJob: 1,
        PatrolRadiusMeters: 30.0,
        SpeedLimitKmh: 50,
        ExcludedStopRadiusMeters: 0.0,
        WarningRadiusMeters: 120.0);

    // One hundred steps per lap on the 1,929 m Brands Indy loop. The keep-out
    // is off in the fixture so the single patrol can sit anywhere and a full
    // lap always passes through it.
    private static void DriveLaps(
        MainViewModel vm, FakeClock clock, FakeTelemetry tel,
        int laps, float speedKmh)
    {
        var track = new TrackInfo("brands_hatch", "indy", 1929.0);
        for (int i = 0; i < laps * 100; i++)
        {
            tel.Next = tel.Next with
            {
                SpeedKmh = speedKmh,
                NormalizedPosition = (i % 100) / 100f,
                Track = track,
            };
            clock.Advance(TimeSpan.FromSeconds(TaxiFlow.DriveTickSeconds));
            vm.Poll();
        }
    }

    [Fact]
    public void Pickup_SpeedChallenge_FinesOnceAndDropsTheTip()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet();
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.SpeedRequest,
        };
        var police = new PolicePatrolPlanner(
            AddressMap.Default, TestPoliceRules, seed: 0);
        // Keep the delivery clock out of this test: it is about one patrol
        // charging once, not about the deadline dropping the tip too.
        TaxiRules rules = TaxiRules.Default with
        {
            SpeedChallenge = SpeedChallengeRules.Default with
            {
                SlackFactor = 10.0,
            },
        };
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, hud: hud, requests: requests, police: police,
            taxiRules: rules, wallet: wallet);

        TaxiFlow.Propose(vm);
        TaxiFlow.Pickup(vm, clock);
        TaxiFlow.AnswerRequest(vm);
        Assert.Equal("HudJobDropoffDrive", hud.JobGuidance!.DriveKey);
        // Three laps through the same patrol: the second and third pass must
        // not charge again.
        DriveLaps(vm, clock, tel, laps: 3, speedKmh: 80f);
        vm.DropoffCommand.Execute(null);

        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.True(receipt.HasFine);
        Assert.False(receipt.HasTip);
        Assert.Contains("50.00", receipt.FineLine.Amount, StringComparison.Ordinal);
        Assert.Contains("Speeding fine", hud.Notice ?? string.Empty, StringComparison.Ordinal);
        // The deadline was beaten, so the gamble still pays its cornering
        // point even though the patrol fined the tip away (PORTPLAN 06).
        Assert.Equal(21.0, wallet.Rating.Cornering);
    }

    [Fact]
    public void Pickup_SpeedChallenge_BelowTheLimitKeepsTheTip()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet();
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.SpeedRequest,
        };
        var police = new PolicePatrolPlanner(
            AddressMap.Default, TestPoliceRules, seed: 0);
        // The test drives 8.2 km in about 82 s, much longer than the real
        // route between the two doors; a generous deadline isolates the
        // police rule this test is about from the delivery clock.
        TaxiRules rules = TaxiRules.Default with
        {
            SpeedChallenge = SpeedChallengeRules.Default with
            {
                SlackFactor = 10.0,
            },
        };
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, hud: hud, requests: requests, police: police,
            taxiRules: rules, wallet: wallet);

        TaxiFlow.Propose(vm);
        TaxiFlow.Pickup(vm, clock);
        TaxiFlow.AnswerRequest(vm);
        DriveLaps(vm, clock, tel, laps: 1, speedKmh: 40f);
        vm.DropoffCommand.Execute(null);

        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.False(receipt.HasFine);
        Assert.True(receipt.HasTip);
        Assert.Contains("Police patrol ahead", hud.Notice ?? string.Empty, StringComparison.Ordinal);
        // Beating the deadline is the challenge's success (PORTPLAN 06): the
        // gamble pays one cornering point.
        Assert.Equal(21.0, wallet.Rating.Cornering);
    }

    [Fact]
    public void Pickup_SpeedChallenge_Late_RemovesTheTip()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet();
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.SpeedRequest,
        };
        // A deliberately tiny allowance makes the 82 s test drive late.
        TaxiRules rules = TaxiRules.Default with
        {
            SpeedChallenge = new SpeedChallengeRules(
                TopSpeedFraction: 0.55, MinAverageKmh: 130.0,
                MaxAverageKmh: 130.0, SlackFactor: 0.1,
                BufferSeconds: 0),
        };
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, hud: hud, requests: requests, taxiRules: rules,
            wallet: wallet);

        TaxiFlow.Propose(vm);
        TaxiFlow.Pickup(vm, clock);
        TaxiFlow.AnswerRequest(vm);
        DriveLaps(vm, clock, tel, laps: 1, speedKmh: 40f);
        vm.DropoffCommand.Execute(null);

        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.False(receipt.HasTip);
        Assert.Equal(Strings.Get("HudDeadlineLate"), hud.Notice);
        // A beaten deadline pays nothing: the challenge failed, so cornering
        // stays where it started.
        Assert.Equal(20.0, wallet.Rating.Cornering);
    }

    [Fact]
    public void Pickup_BrakePromise_HardStopShowsTheReasonAndDropsTheTip()
    {
        var hud = new FakeTelemetryHud();
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.BrakePromise,
        };
        var tel = new FakeTelemetry();
        var (vm, clock, _, _) = Create(
            speedKmh: 0f, hud: hud, requests: requests, physics: tel);

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));
        screen.Options[0].Command.Execute(null);
        screen.BackCommand.Execute(null);
        Assert.Null(hud.Notice);

        // A gentle sample at speed keeps the promise.
        tel.Next = tel.Next with { SpeedKmh = 60f, Gas = 0.3f, Brake = 0.1f };
        clock.Advance(TimeSpan.FromSeconds(TaxiFlow.DriveTickSeconds));
        vm.Poll();
        Assert.Null(hud.Notice);

        // Stomping the brake at speed breaks it; the HUD names the reason.
        tel.Next = tel.Next with { SpeedKmh = 60f, Gas = 0.1f, Brake = 0.9f };
        clock.Advance(TimeSpan.FromSeconds(TaxiFlow.DriveTickSeconds));
        vm.Poll();
        Assert.Equal(Strings.Get("HudPromiseBrake"), hud.Notice);

        // The ride still settles; only the tip is gone.
        TaxiFlow.Drive(vm, clock, tel);
        vm.DropoffCommand.Execute(null);

        Assert.False(Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen).HasTip);
    }

    [Fact]
    public void Pickup_HurryPromise_ArmsTheHudClockAndAnOnTimeRideStillTips()
    {
        var hud = new FakeTelemetryHud();
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.HurryPromise,
        };
        var (vm, clock, tel, _) = Create(speedKmh: 0f, hud: hud, requests: requests);

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));
        screen.Options[0].Command.Execute(null);
        screen.BackCommand.Execute(null);

        // The passenger's hurry rides the same clock with its own label.
        Assert.NotNull(hud.JobGuidance!.Deadline);
        Assert.Equal("HudHurryLeft", hud.JobGuidance.DeadlineKey);

        TaxiFlow.Drive(vm, clock, tel);
        vm.DropoffCommand.Execute(null);

        Assert.True(Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen).HasTip);
    }

    [Fact]
    public void Pickup_HurryPromise_LateRemovesTheTip()
    {
        var hud = new FakeTelemetryHud();
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.HurryPromise,
        };
        // A tiny allowance: waiting five minutes at the door is too late.
        TaxiRules rules = TaxiRules.Default with
        {
            SpeedChallenge = new SpeedChallengeRules(
                TopSpeedFraction: 0.55, MinAverageKmh: 130.0,
                MaxAverageKmh: 130.0, SlackFactor: 0.1,
                BufferSeconds: 0),
        };
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, hud: hud, requests: requests, taxiRules: rules);

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));
        screen.Options[0].Command.Execute(null);
        screen.BackCommand.Execute(null);

        clock.Advance(TimeSpan.FromMinutes(5));
        TaxiFlow.Drive(vm, clock, tel);
        vm.DropoffCommand.Execute(null);

        Assert.False(Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen).HasTip);
        Assert.Equal(Strings.Get("HudDeadlineLate"), hud.Notice);
    }

    [Fact]
    public void Pickup_SpeedChallenge_ReadInThenBackToTheTaskText()
    {
        var hud = new FakeTelemetryHud();
        var requests = new FakeCustomerRequestRoller
        {
            Result = CustomerRequestTestData.SpeedRequest,
        };
        var police = new PolicePatrolPlanner(
            AddressMap.Default, TestPoliceRules, seed: 0);
        var (vm, clock, _, _) = Create(
            speedKmh: 0f, hud: hud, requests: requests, police: police);

        TaxiFlow.Propose(vm);
        var screen = Assert.IsType<CustomerRequestScreenViewModel>(
            TaxiFlow.Pickup(vm, clock));
        Assert.True(screen.IsSpeedChallenge);
        Assert.Empty(screen.Options);
        Assert.False(screen.ShowBack);
        Assert.False(screen.TimerVisible);
        Assert.Same(screen, hud.Request);

        // No answer and no button: the read-in closes itself and the drop-off
        // task text comes back with the delivery clock still running.
        clock.Advance(TimeSpan.FromSeconds(TaxiRules.Default.NoticeSeconds));
        vm.Poll();

        Assert.Null(hud.Request);
        Assert.Equal(1, hud.ClearRequestCalls);
        Assert.Equal("HudJobDropoffDrive", hud.JobGuidance!.DriveKey);
        Assert.NotNull(hud.JobGuidance.Deadline);
    }

    [Fact]
    public void ShiftTimeout_DoesNotEndAnActiveJob()
    {
        var (vm, clock, _, _) = Create(speedKmh: 0f);
        TaxiFlow.Propose(vm);
        Assert.Contains("left", vm.ShiftText, StringComparison.Ordinal);

        // A long drive to a far door is work in progress, not idle time.
        clock.Advance(TimeSpan.FromMinutes(31));
        vm.Poll();

        Assert.Contains("left", vm.ShiftText, StringComparison.Ordinal);
        Assert.True(vm.PickupCommand.CanExecute(null));
    }

    [Fact]
    public void ShiftTimeout_AfterACompletedJob_StillEndsTheShift()
    {
        var (vm, clock, tel, _) = Create(speedKmh: 0f);
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        Assert.Contains("left", vm.ShiftText, StringComparison.Ordinal);

        clock.Advance(TimeSpan.FromMinutes(31));
        vm.Poll();

        Assert.DoesNotContain("left", vm.ShiftText, StringComparison.Ordinal);
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
        var session = Create(speedKmh: 0f);
        var (vm, _, _, _) = session;
        session.Story.FinishDialogue();
        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);

        proposal.BackCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Contains("left", vm.ShiftText, StringComparison.Ordinal);
        Assert.DoesNotContain("Proposed", vm.JobLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlBack_ReturnsToTheMenu()
    {
        // The control page (the menu's Jobs tile) is the jobs hub; BACK is its
        // one way out and lands on the menu, the same page a call opens.
        var session = Create(speedKmh: 0f);
        var (vm, _, _, _) = session;
        var control = FinishIntro(session);

        control.BackCommand.Execute(null);

        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void MenuWallet_OpensTheAccountSummary_AndBackReturnsToMenu()
    {
        // The menu's WALLET tile opens the account page; every figure reads
        // the live wallet, so a job settled since the last visit shows.
        var wallet = new DriverWallet(new DriverState(123.45, 678.0, Bank: 1500.0));
        var (vm, _, _, _) = Create(
            progress: OnboardingState.Fresh with { BirthdayDone = true }, wallet: wallet,
            // Today already claimed: the boot grant must not add to the cash
            // the page is about to show.
            savedDriver: new DriverState(0.0, 0.0, LastDailyBonusDate: "2026-10-12"));
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);

        menu.WalletCommand.Execute(null);

        var page = Assert.IsType<WalletScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("CURRENT CR", page.CashLabel);
        Assert.Equal("CR123.45", page.CashAmount);
        Assert.Equal("CURRENT XP", page.XpLabel);
        Assert.Equal("XP678.00", page.XpAmount);
        Assert.Equal("CURRENT BANK SALDO", page.BankLabel);
        Assert.Equal("CR1500.00", page.BankAmount);
        Assert.Equal("Concentration: 20%", page.ConcentrationLine);

        page.BackCommand.Execute(null);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void ShiftTimeout_EndsShift_FlipsLabelBack()
    {
        var session = Create(speedKmh: 0f);
        var (vm, clock, _, _) = session;
        var control = FinishIntro(session);
        vm.TaxiShiftCommand.Execute(null);
        Assert.Equal("CONTINUE TAXI SHIFT", control.TaxiLabel);

        clock.Advance(TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1)));
        vm.Poll();
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);
        Assert.DoesNotContain("left", vm.ShiftText, StringComparison.Ordinal);
    }

    [Fact]
    public void TaxiShiftStart_BanksTheShiftForTheNextSession()
    {
        var shifts = new FakeShiftStore();
        var (vm, _, _, _) = Create(speedKmh: 0f, shiftStore: shifts);

        vm.TaxiShiftCommand.Execute(null);

        TaxiShiftSnapshot saved = Assert.Single(shifts.Saved);
        Assert.True(saved.IsStarted);
        Assert.Equal(0, saved.JobsDone);
        Assert.NotEqual(default, saved.LastJobEnd);
    }

    [Fact]
    public void RestoredShift_KeepsCountingUntilThirtyMinutesAfterTheLastJob()
    {
        // FakeClock opens at 12:00; the last dropoff was 10 minutes ago, so a
        // restart resumes the same shift with 20 minutes left.
        var restart = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        var shift = new TaxiShift(new TaxiShiftSnapshot(
            IsStarted: true, StartedAt: restart.AddMinutes(-15),
            LastJobEnd: restart.AddMinutes(-10), JobsDone: 2, Earned: 42.0));
        var (vm, clock, _, _) = Create(speedKmh: 0f, shift: shift);

        vm.Poll();
        Assert.Contains("left", vm.ShiftText, StringComparison.Ordinal);
        Assert.Contains("2 jobs", vm.ShiftText, StringComparison.Ordinal);

        clock.Advance(TimeSpan.FromMinutes(20).Add(TimeSpan.FromSeconds(1)));
        vm.Poll();
        Assert.DoesNotContain("left", vm.ShiftText, StringComparison.Ordinal);
        Assert.Contains("2 jobs", vm.ShiftText, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoredShift_ExpiredWhileClosed_EndsOnTheFirstPoll()
    {
        // The overlay was shut for 50 minutes after the last dropoff: the
        // shift is already over and the first poll banks it as closed.
        var shifts = new FakeShiftStore();
        var online = new FakePlayerTelemetry();
        var board = new FakeShiftStandings();
        var driverStore = new FakeDriverStore();
        var restart = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        var shift = new TaxiShift(new TaxiShiftSnapshot(
            IsStarted: true, StartedAt: restart.AddMinutes(-55),
            LastJobEnd: restart.AddMinutes(-50), JobsDone: 1, Earned: 20.0));
        var (vm, _, _, _) = Create(
            speedKmh: 0f, shift: shift, shiftStore: shifts, telemetry: online,
            shiftStandings: board, driverStore: driverStore);

        vm.Poll();

        Assert.DoesNotContain("left", vm.ShiftText, StringComparison.Ordinal);
        Assert.Contains("1 jobs", vm.ShiftText, StringComparison.Ordinal);
        Assert.Contains(shifts.Saved, s => !s.IsStarted);
        // The closed shift still feeds the best-shift record and the board.
        Assert.Single(online.ShiftPushes);
        Assert.Equal(20.0, online.ShiftPushes[0].Earnings);
        // The notification page opens on that tick (Unity chains the
        // shift-ended screen before the map), so the timeout is never silent.
        var page = Assert.IsType<ShiftEndedScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Jobs completed: 1", page.JobsLine);
        Assert.Equal("Total earnings: €20.00", page.EarningsLine);
        Assert.Equal("Shift duration: 5 min", page.DurationLine);
        Assert.Equal("Longest shift: 1 jobs", page.LongestLine);
        Assert.Equal("Total shifts completed: 1", page.TotalShiftsLine);
        // The rank was refreshed against the board and banked with the
        // lifetime counters (Unity SaveToPlayerData).
        Assert.Equal((20.0, restart.AddMinutes(-20).Ticks), Assert.Single(board.Refreshes));
        Assert.Equal(1, driverStore.Saved[^1].ShiftsCompleted);
        Assert.Equal(1, driverStore.Saved[^1].LongestShiftJobs);
    }

    [Fact]
    public void TaxiTap_OnATimedOutShift_ShowsShiftEnded_InsteadOfANewShift()
    {
        // Unity JobSelectionScreen: the tap that finds a stale shift ends it,
        // shows the stats page and returns without starting a new shift.
        var session = Create(speedKmh: 0f);
        var (vm, clock, _, _) = session;
        session.Story.FinishDialogue();
        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        proposal.BackCommand.Execute(null);

        clock.Advance(TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1)));
        vm.TaxiShiftCommand.Execute(null);

        var page = Assert.IsType<ShiftEndedScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Jobs completed: 0", page.JobsLine);
        Assert.DoesNotContain("left", vm.ShiftText, StringComparison.Ordinal);

        // CONTINUE lands on the control page; the next tap then opens a
        // fresh shift as usual.
        page.ContinueCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.DoesNotContain("left", vm.ShiftText, StringComparison.Ordinal);

        vm.TaxiShiftCommand.Execute(null);
        Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        Assert.Contains("left", vm.ShiftText, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDayFlip_MovesHighlight_LogsTransition()
    {
        // Half speed: ~53 real minutes carry the game across midnight. The
        // flip moves the teal outline; the gate is re-read on every tap.
        var log = new TestLog();
        var session = Create(speedKmh: 0f, log: log);
        var (vm, clock, _, _) = session;
        session.Story.FinishDialogue();
        var calendar = ShowCalendar(vm);
        Assert.Equal(2, calendar.Today);

        clock.Advance(TimeSpan.FromMinutes(53));
        vm.Poll();
        Assert.Equal(3, calendar.Today);
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
        var session = Create(speedKmh: 0f, log: log, roads: roads, roadTestStore: bank);
        var (vm, clock, _, _) = session;
        session.Story.FinishDialogue();
        var calendar = ShowCalendar(vm);
        clock.Advance(TimeSpan.FromMinutes(53)); // across Monday midnight: Tuesday noon
        vm.Poll();
        string first = TuesdayButton(calendar);
        Assert.NotEqual(string.Empty, first);

        calendar.DayCommand.Execute(3);
        string second = TuesdayButton(calendar);
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
        var session = Create(speedKmh: 0f, log: log, roads: roads, roadTestStore: bank);
        var (vm, clock, _, _) = session;
        session.Story.FinishDialogue();
        var calendar = ShowCalendar(vm);
        clock.Advance(TimeSpan.FromMinutes(75)); // 2.2 s per game minute: Tuesday 22:0x
        calendar.DayCommand.Execute(3);
        Assert.True(Assert.Single(calendar.Days, d => d.DayOfWeek == 3).Flash);

        clock.Advance(TimeSpan.FromSeconds(0.51)); // the beat drains on the poll
        vm.Poll();
        Assert.Equal("COME BACK ON TUESDAY BETWEEN 8am AND 8pm", TuesdayButton(calendar));
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
        var session = Create(roads: TestRoadCars.Ledger(seed: 7), roadTestStore: bank);
        var (vm, _, _, _) = session;
        vm.Start();
        RoadTestSnapshot saved = Assert.Single(bank.Saved);

        var restarted = TestRoadCars.Ledger(seed: 4242, saved: saved);
        CarSpec car = Assert.IsType<CarSpec>(restarted.Current);
        var session2 = Create(roads: restarted);
        var (again, _, _, _) = session2;
        session2.Story.FinishDialogue();
        var calendar = ShowCalendar(again);

        Assert.Equal(saved.CarId, car.Id);
        Assert.Equal(TestRoadCars.Button(car), TuesdayButton(calendar));
    }

    [Fact]
    public void TapOtherDay_StaysLocked()
    {
        var log = new TestLog();
        var roads = TestRoadCars.Ledger();
        var session = Create(speedKmh: 0f, log: log, roads: roads);
        var (vm, _, _, _) = session;
        session.Story.FinishDialogue();
        var calendar = ShowCalendar(vm);
        string first = TuesdayButton(calendar);

        calendar.DayCommand.Execute(2);
        Assert.Equal(first, TuesdayButton(calendar));
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
        var session = Create(
            speedKmh: 0f, catalog: new DeliveryCatalog([store], 0.7, 100), city: city);
        var (vm, clock, _, _) = session;
        session.Story.FinishDialogue();
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
        var session = Create(speedKmh: 0f, city: city, cityStore: bank);
        var (vm, clock, _, _) = session;
        session.Story.FinishDialogue();
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
        var session = Create(speedKmh: 0f, log: log, roads: roads, clocks: store);
        var (vm, clock, _, _) = session;
        session.Story.FinishDialogue();
        var calendar = ShowCalendar(vm);
        string first = TuesdayButton(calendar);

        for (int i = 0; i < 8; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(53));
            vm.Poll();
        }

        Assert.Contains("WEEK 2", calendar.DayWeekText, StringComparison.Ordinal);
        Assert.Equal(first, TuesdayButton(calendar));
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
        var session = Create(speedKmh: 0f, clocks: store);
        var (vm, clock, _, _) = session;
        session.Story.FinishDialogue();
        var calendar = ShowCalendar(vm);
        clock.Advance(TimeSpan.FromMinutes(53));
        vm.Poll();
        vm.SaveClock();
        GameClockState saved = store.Saved[^1];

        var session2 = Create(
            progress: OnboardingState.Fresh with { BirthdayDone = true },
            clocks: store, gameStart: saved.GameNow, startWeek: saved.Week);
        var (restarted, _, _, _) = session2;
        // A finished introduction opens on the menu; its Calendar tile leads
        // to the calendar page whose header we read.
        var menu = Assert.IsType<MenuScreenViewModel>(restarted.CurrentScreen);
        menu.CalendarCommand.Execute(null);
        var resumed = Assert.IsType<CalendarScreenViewModel>(restarted.CurrentScreen);
        Assert.Equal(calendar.ClockText, resumed.ClockText);
        Assert.Equal(calendar.DayWeekText, resumed.DayWeekText);
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
        // Checkpoints, not polls: a shift start and a settled job bank before
        // the throttle is due, so a kill cannot rewind the calendar past
        // progress. The drive's own 60 s wall-clock bank may fire mid-ride;
        // the settle must still add its write on top.
        var store = new FakeGameClockStore();
        var (vm, clock, tel, _) = Create(speedKmh: 0f, clocks: store);
        vm.TaxiShiftCommand.Execute(null);
        Assert.Single(store.Saved); // shift start

        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        proposal.AcceptCommand.Execute(null);
        vm.PickupCommand.Execute(null);
        TaxiFlow.Drive(vm, clock, tel);
        int beforeSettle = store.Saved.Count;
        vm.DropoffCommand.Execute(null);
        Assert.Equal(beforeSettle + 1, store.Saved.Count);
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

    private static string TuesdayButton(CalendarScreenViewModel calendar) =>
        Assert.Single(calendar.Days, d => d.DayLabel == "Tuesday").EventLabel;

    // The intro's last CONTINUE lands on the menu and leaves the panel up;
    // these flows take the Jobs tile into the career page they exercise. The
    // dialogue is the story flow's, so the shortcut drives it directly.
    private static MainControlScreenViewModel FinishIntro(TestSessionResult session)
    {
        session.Story.FinishDialogue();
        Assert.IsType<MenuScreenViewModel>(session.Vm.CurrentScreen);
        return ShowControl(session.Vm);
    }

    // A call opens the menu; its Jobs tile is the door into the career page.
    private static MainControlScreenViewModel ShowControl(MainViewModel vm)
    {
        vm.Show();
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        menu.JobsCommand.Execute(null);
        return Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    // A call lands on the menu; its Calendar tile opens the page whose week
    // strip these tests read.
    private static CalendarScreenViewModel ShowCalendar(MainViewModel vm)
    {
        vm.Show();
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        menu.CalendarCommand.Execute(null);
        return Assert.IsType<CalendarScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void ToggleShow_FlipsVisibility()
    {
        var (vm, _, _, _) = Create();
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void Show_DuringAcceptedJob_LeavesThePanelHidden()
    {
        // ACCEPT hands the drive (and the mic) to the HUD. A call must not
        // pop the job centre over the ride: the player could start a second
        // job from the control page.
        var (vm, _, _, _) = Create(speedKmh: 0f);
        TaxiFlow.Propose(vm);
        Assert.False(vm.IsShown);

        vm.Show();

        Assert.False(vm.IsShown);
        Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void Show_WhilePassengerAboard_LeavesThePanelHidden()
    {
        var (vm, _, _, _) = Create(speedKmh: 0f);
        TaxiFlow.Propose(vm);
        vm.PickupCommand.Execute(null);
        Assert.False(vm.IsShown);

        vm.Show();

        Assert.False(vm.IsShown);
    }

    [Fact]
    public void Show_BeforeAJobIsAccepted_StillOpensTheMenu()
    {
        // The gate covers the accepted job, not the shift: a running shift
        // with no customer must still reach the job centre to pick one.
        var (vm, _, _, _) = Create(speedKmh: 0f);
        vm.TaxiShiftCommand.Execute(null);
        vm.ToggleShowCommand.Execute(null);
        Assert.False(vm.IsShown);

        vm.Show();

        Assert.True(vm.IsShown);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void Show_AfterTheJobSettles_OpensTheMenu()
    {
        var (vm, clock, tel, _) = Create(speedKmh: 0f);
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        vm.ToggleShowCommand.Execute(null); // back to driving with the job done
        Assert.False(vm.IsShown);

        vm.Show();

        Assert.True(vm.IsShown);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void FinishDialogue_ShowsGrandmasCarPage()
    {
        var session = Create(content: WalkWithCar);
        var (vm, _, _, _) = session;

        session.Story.FinishDialogue();

        // Grandma's last line sends the player out for maple syrup; one page is
        // left in the walk, so the introduction is not over yet.
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen);
        Assert.False(session.Story.Done);
    }

    [Fact]
    public void CarPage_Continue_ClosesThePanelForTheTour()
    {
        var session = Create(content: WalkWithCar);
        var (vm, _, _, _) = session;
        session.Story.FinishDialogue();
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);

        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        // The guided tour still has to be driven (Max, the friends, pancakes,
        // the community centre): the hand-over hides the panel and lets the
        // HUD guide instead of leaving the menu over the road.
        Assert.False(vm.IsShown);
        Assert.True(session.Story.Done);
    }

    [Fact]
    public void CarPage_Continue_RevealsTheHud()
    {
        var hud = new FakeTelemetryHud();
        var session = Create(content: WalkWithCar, hud: hud);
        Assert.False(hud.Revealed);

        session.Story.FinishDialogue();
        Assert.False(hud.Revealed); // the closing page still owns the screen

        Assert.IsType<GrandmaCarScreenViewModel>(session.Vm.CurrentScreen)
            .ContinueCommand.Execute(null);

        Assert.True(hud.Revealed);
    }

    [Fact]
    public void ShowDialogue_FillsTheErrandPoiAndRandomAddress()
    {
        var content = new OnboardingContent(
            [], new Dictionary<string, string[]>(), [],
            [new DialogueLine("GRANDMA", "Stop by [POI] on [STREET].")], [], [], []);
        PointOfInterest expected =
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5)
                .Spawn(PoiCatalog.MaxGroceryId)!;
        var session = Create(
            content: content,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5));
        var (vm, _, _, _) = session;

        session.Story.ShowDialogue();

        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(
            $"Stop by {expected.Name} on {expected.Address.StreetName} · No. {expected.Address.HouseNumber}.",
            dialogue.Body);
    }

    [Fact]
    public void FinishDialogue_PinsTheErrandOnTheHud()
    {
        PointOfInterest expected =
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5)
                .Spawn(PoiCatalog.MaxGroceryId)!;
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud);

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();

        Assert.Equal(expected, hud.Destination);
    }

    [Fact]
    public void FinishDialogue_AddsTheErrandToTheAddressesPage()
    {
        var poiStore = new FakePoiDirectoryStore();
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, poiStore: poiStore);
        var (vm, _, _, _) = session;

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();

        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        menu.HomeCommand.Execute(null);
        var home = Assert.IsType<HomeScreenViewModel>(vm.CurrentScreen);
        home.AddressesCommand.Execute(null);
        var addresses = Assert.IsType<AddressesScreenViewModel>(vm.CurrentScreen);
        AddressRow errand = Assert.Single(
            addresses.Rows, row => row.Poi.Id == PoiCatalog.MaxGroceryId);

        Assert.Equal("Max's Grocery Store", errand.Name);
        Assert.Contains(
            poiStore.Saved, poi => poi.Id == PoiCatalog.MaxGroceryId);
    }

    [Fact]
    public void BirthdayErrand_ReusesTheBankedStoreAcrossLaunches()
    {
        var onboarding = new FakeOnboardingStore();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            onboarding: onboarding);

        session.Story.ShowDialogue();
        PointOfInterest expected =
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5)
                .Spawn(PoiCatalog.MaxGroceryId)!;
        Assert.Equal(expected.Address, onboarding.State.MaxStoreAddress);
        Assert.Equal(expected.Id, onboarding.State.MaxStoreId);

        var hud = new FakeTelemetryHud();
        var session2 = Create(
            content: TestSession.OneLineDialogue,
            // A different seed proves the banked address wins over a fresh draw.
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 99),
            hud: hud, onboarding: onboarding);
        session2.Story.ShowDialogue();
        session2.Story.FinishDialogue();

        Assert.Equal(expected, hud.Destination);
        Assert.Equal(expected.Address, onboarding.State.MaxStoreAddress);
    }

    [Fact]
    public void EnterRequested_OpensTheStoreInterior()
    {
        PointOfInterest expected =
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5)
                .Spawn(PoiCatalog.MaxGroceryId)!;
        var log = new TestLog();
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud,
            log: log);
        var (vm, _, _, _) = session;

        session.Story.ShowDialogue();
        hud.RaiseEnter();

        var store = Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("MAX'S GROCERY STORE", store.StoreTitle);
        Assert.Contains(
            log.Infos,
            line => line.Contains($"entered {expected.Name}", StringComparison.Ordinal));
    }

    [Fact]
    public void EnterRequested_HidesTheCompanionTaskText()
    {
        // The companion asked "enter?" and the player walked into the door:
        // the guidance and its button are consumed by the entry, so the mini
        // window must not keep showing them.
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud);

        session.Story.ShowDialogue();
        hud.RaiseEnter();

        Assert.Equal(1, hud.DestinationEnteredCalls);
    }

    [Fact]
    public void BirthdayErrandVisit_DoesNotGrantTheIntroCredP()
    {
        // Unity only hands the delivery gift over on the first visit AFTER
        // firstSceneComplete (hasSeenMaxJobIntro); the birthday errand that
        // starts the tour must leave the career at zero.
        var career = new DeliveryCareer(HubCatalog.Default, 0);
        var onboarding = new FakeOnboardingStore();
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, onboarding: onboarding, career: career);
        var (vm, _, _, _) = session;

        session.Story.ShowDialogue();
        Assert.Equal(0, career.CredP);

        hud.RaiseEnter();
        Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(0, career.CredP);
        Assert.False(onboarding.State.MaxDeliveryIntroSeen);
    }

    [Fact]
    public void FirstPostIntroMaxVisit_ShowsTheDeliveryOfferThenGrantsTheCredP()
    {
        // Unity: firstSceneComplete && !hasSeenMaxJobIntro shows
        // MaxDialogueScreen; its Continue pays the 100 CredP and opens the
        // shop. The delivery page must read 0 until that visit.
        var career = new DeliveryCareer(HubCatalog.Default, 0);
        var onboarding = new FakeOnboardingStore();
        var hud = new FakeTelemetryHud();
        var progress = new OnboardingState(
            null, null, BirthdayDone: true,
            JustinPickedUp: true, AnnabellaPickedUp: true, PancakesDone: true,
            MaxErrandDone: true, CommunityCentreDone: true);
        var (vm, _, _, _) = Create(
            content: TestSession.OneLineDialogue, progress: progress,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, onboarding: onboarding, career: career);

        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(0, career.CredP);

        hud.SetDestination(
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5)
                .Spawn(PoiCatalog.MaxGroceryId));
        hud.RaiseEnter();

        var offer = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("A NEW TURN", offer.Title);
        Assert.Equal(0, career.CredP);
        Assert.False(onboarding.State.MaxDeliveryIntroSeen);

        offer.ContinueCommand.Execute(null);

        Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(DeliveryCatalog.Default.StartCredP, career.CredP);
        Assert.True(onboarding.State.MaxDeliveryIntroSeen);

        // Backing out and entering again is a plain store visit now: the
        // offer and its gift are one-shot.
        Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen).BackCommand.Execute(null);
        hud.RaiseEnter();
        Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(DeliveryCatalog.Default.StartCredP, career.CredP);
    }

    [Fact]
    public void ReturnVisitToMaxStore_DoesNotGrantTheIntroCredPAgain()
    {
        // A banked first visit restores the gift from App's career seed; the
        // entering screen must not add a second copy.
        var career = new DeliveryCareer(HubCatalog.Default, DeliveryCatalog.Default.StartCredP);
        var onboarding = new FakeOnboardingStore();
        onboarding.Save(OnboardingState.Fresh with
        {
            BirthdayDone = true,
            MaxDeliveryIntroSeen = true,
            MaxErrandDone = true,
            CommunityCentreDone = true,
        });
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, onboarding: onboarding, career: career,
            progress: onboarding.State);

        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        hud.SetDestination(
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5)
                .Spawn(PoiCatalog.MaxGroceryId));
        hud.RaiseEnter();

        Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(DeliveryCatalog.Default.StartCredP, career.CredP);
    }

    [Fact]
    public void EnterRequested_WhenHidden_PopsTheOverlayOnTheStore()
    {
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud);
        var (vm, _, _, _) = session;

        session.Story.ShowDialogue();
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
        var car = new CarSpec(
            "ks_test_grocery", 2020, "Test", "Grocery", Passengers: 1,
            TrunkLitres: 60);
        var pool = new[] { car };
        var garage = new Garage(pool);
        garage.Add(car, week: 1);
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        var drivers = new FakeDriverStore();
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, wallet: wallet, cars: pool, garage: garage,
            driverStore: drivers);
        var (vm, _, _, _) = session;

        session.Story.ShowDialogue();
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
    public void MenuTrunkTile_OpensTheCarriedGoodsPage_AndABiteBanksTrunkAndHunger()
    {
        var car = new CarSpec(
            "ks_test_trunk", 2020, "Test", "Trunk", Passengers: 1,
            TrunkLitres: 60);
        var pool = new[] { car };
        var garage = new Garage(pool);
        garage.Add(car, week: 1);
        var trunk = new Trunk([new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 2 }]);
        var hunger = new Hunger(
            HungerRules.Default, new HungerState(10, ThresholdTriggered: false));
        var hungerStore = new FakeHungerStore();
        var drivers = new FakeDriverStore();
        var session = Create(
            cars: pool, garage: garage, trunk: trunk, hunger: hunger,
            hungerStore: hungerStore, driverStore: drivers);
        var (vm, _, _, _) = session;

        session.Story.FinishDialogue();
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        menu.TrunkCommand.Execute(null);
        var page = Assert.IsType<TrunkScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Free space: 59.20/60L", page.FreeSpaceLine);

        page.EatCommand.Execute(Assert.Single(page.Rows));

        // A bite moves two files: the trunk rides driver.json, the appetite
        // has its own store, so the host banks both in the same tap.
        Assert.Equal(1, trunk.Items.Single().Quantity);
        Assert.Equal(7, hunger.Value);
        Assert.Contains(drivers.Saved, saved =>
            saved.Trunk is { Count: 1 } && saved.Trunk[0].Quantity == 1);
        Assert.Contains(hungerStore.Saved, state => state.Hunger == 7);

        page.BackCommand.Execute(null);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void StoreBack_WithoutMaple_KeepsTheArrivalAndThePanel()
    {
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud);
        var (vm, _, _, _) = session;

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();
        PointOfInterest? errand = hud.Destination;
        hud.RaiseEnter();
        var store = Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);

        store.BackCommand.Execute(null);

        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.True(vm.IsShown);
        Assert.Same(errand, hud.Destination);
    }

    [Fact]
    public void StoreBack_WithAnOldMapleStackButNoPurchase_KeepsTheErrand()
    {
        // The trunk persists across sessions, so an old maple stack must not
        // let BACK alone bank this tour's store visit.
        var trunk = new Trunk(
            [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hud = new FakeTelemetryHud();
        var onboarding = new FakeOnboardingStore();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, trunk: trunk, onboarding: onboarding);
        var (vm, _, _, _) = session;

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();
        PointOfInterest? errand = hud.Destination;
        hud.RaiseEnter();
        var store = Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);

        store.BackCommand.Execute(null);

        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.True(vm.IsShown);
        Assert.Same(errand, hud.Destination);
        Assert.False(onboarding.State.MaxErrandDone);
    }

    [Fact]
    public void StoreBack_WithAnOldMapleStackAfterBuyingMore_AdvancesTheStory()
    {
        // Buying the errand item this visit still completes it, so the old
        // stack must not block progress either.
        var car = new CarSpec(
            "ks_test_grocery", 2020, "Test", "Grocery", Passengers: 1,
            TrunkLitres: 60);
        var pool = new[] { car };
        var garage = new Garage(pool);
        garage.Add(car, week: 1);
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        var trunk = new Trunk(
            [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, wallet: wallet, cars: pool, garage: garage, trunk: trunk);
        var (vm, _, _, _) = session;

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();
        hud.RaiseEnter();
        var store = Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);
        StoreItemRowViewModel row = store.Items.First(
            item => item.Item.Name == "MAPLE SYRUP");
        row.SelectCommand.Execute(null);
        store.BuyCommand.Execute(null);

        store.BackCommand.Execute(null);

        Assert.False(vm.IsShown);
        Assert.Equal(StoryFriends.Justin.Id, hud.Destination?.Id);
    }

    [Fact]
    public void StoreBack_AfterMaple_HidesThePanelAndPinsTheNextPhase()
    {
        var car = new CarSpec(
            "ks_test_grocery", 2020, "Test", "Grocery", Passengers: 1,
            TrunkLitres: 60);
        var pool = new[] { car };
        var garage = new Garage(pool);
        garage.Add(car, week: 1);
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, wallet: wallet, cars: pool, garage: garage);
        var (vm, _, _, _) = session;

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();
        hud.RaiseEnter();
        var store = Assert.IsType<StoreScreenViewModel>(vm.CurrentScreen);
        StoreItemRowViewModel row = store.Items.First(
            item => item.Item.Name == "MAPLE SYRUP");
        row.SelectCommand.Execute(null);
        store.BuyCommand.Execute(null);

        store.BackCommand.Execute(null);

        Assert.False(vm.IsShown);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(StoryFriends.Justin.Id, hud.Destination?.Id);
        Assert.Equal(PoiTypes.Person, hud.Destination?.Type);
    }

    [Fact]
    public void FinishDialogue_WithMapleAlreadyInTheTrunk_StillStartsAtMaxsStore()
    {
        // The inventory persists, so old maple syrup must not skip this run's
        // birthday errand. Only MaxErrandDone banks the store visit.
        var trunk = new Trunk(
            [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, trunk: trunk);

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();

        Assert.Equal("max-grocery", hud.Destination?.Id);
        Assert.Equal(PoiTypes.Grocery, hud.Destination?.Type);
        Assert.Null(hud.HintKey);
    }

    [Fact]
    public void PickUpJustin_PinsAnnabellasDoorOnTheSameStreet()
    {
        var trunk = new Trunk(
            [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hud = new FakeTelemetryHud();
        var onboarding = new FakeOnboardingStore();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, trunk: trunk, onboarding: onboarding, progress: ErrandDone);
        session.Story.ShowDialogue();
        session.Story.FinishDialogue();
        PointOfInterest justin = Assert.IsType<PointOfInterest>(hud.Destination);

        hud.RaiseEnter();

        PointOfInterest annabella = Assert.IsType<PointOfInterest>(hud.Destination);
        Assert.Equal(StoryFriends.Annabella.Id, annabella.Id);
        Assert.Equal(StoryFriends.Annabella.Name, annabella.Name);
        Assert.Equal(PoiTypes.Person, annabella.Type);
        Assert.Equal(justin.Address.StreetName, annabella.Address.StreetName);
        Assert.NotEqual(justin.Address.HouseNumber, annabella.Address.HouseNumber);
        Assert.Equal("HudHintAnnabella", hud.HintKey);
        Assert.True(onboarding.State.JustinPickedUp);
        Assert.Equal(annabella.Address, onboarding.State.AnnabellaAddress);
    }

    [Fact]
    public void PickingUpAnnabella_SendsThePlayerHomeForPancakes()
    {
        var trunk = new Trunk(
            [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hud = new FakeTelemetryHud();
        var onboarding = new FakeOnboardingStore();
        var profile = new PlayerProfile("Ace", "Germany", "Brands", "street-2", 5);
        var content = TestSession.OneLineDialogue with
        {
            PancakeDialogue = [new DialogueLine("GRANDMA", "Pancakes!")],
            CommunityCentreDialogue =
            [
                new DialogueLine("SIGNORE MORO", "Hello."),
                new DialogueLine("YOU", "It's [YOU]."),
            ],
        };
        var session = Create(
            content: content,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, trunk: trunk, onboarding: onboarding, profile: profile,
            progress: ErrandDone);
        var (vm, _, _, _) = session;
        session.Story.ShowDialogue();
        session.Story.FinishDialogue();

        hud.RaiseEnter(); // Justin
        hud.RaiseEnter(); // Annabella: the tour hands over to the home leg

        PointOfInterest home = Assert.IsType<PointOfInterest>(hud.Destination);
        Assert.Equal(StoryHome.Definition.Id, home.Id);
        Assert.Equal(PoiTypes.Home, home.Type);
        Assert.Equal(new AddressLocation("Street 2", 5, HouseSide.Right), home.Address);
        Assert.Equal("HudHintHome", hud.HintKey);
        Assert.Equal("HudArrivedHome", hud.Copy?.ArrivedKey);
        Assert.Equal("HudPancakes", hud.Copy?.ActionKey);
        Assert.True(onboarding.State.AnnabellaPickedUp);
        Assert.False(onboarding.State.PancakesDone);

        hud.RaiseEnter(); // PANCAKES

        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.True(vm.IsShown);
        Assert.Equal(Strings.Get("PancakeTitle"), dialogue.Title);
        Assert.NotEqual(Strings.Get("BirthdayTitle"), dialogue.Title);
        Assert.Equal("Pancakes!", dialogue.Body);

        dialogue.ContinueCommand.Execute(null);

        Assert.True(onboarding.State.PancakesDone);
        Assert.False(vm.IsShown);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        PointOfInterest centre = Assert.IsType<PointOfInterest>(hud.Destination);
        Assert.Equal(StoryCommunityCentre.Definition.Id, centre.Id);
        Assert.Equal(StoryCommunityCentre.Definition.Name, centre.Name);
        Assert.Equal(PoiTypes.CommunityCentre, centre.Type);
        Assert.Equal("HudHintCommunityCentre", hud.HintKey);
        Assert.Equal("HudArrivedCommunityCentre", hud.Copy?.ArrivedKey);
        Assert.Equal("HudEnter", hud.Copy?.ActionKey);
        Assert.Equal(centre.Address, onboarding.State.CommunityCentreAddress);
        Assert.False(onboarding.State.CommunityCentreDone);

        hud.RaiseEnter(); // ENTER at the community centre

        var centreDialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.True(vm.IsShown);
        Assert.Equal(Strings.Get("CommunityCentreTitle"), centreDialogue.Title);
        Assert.Equal(Strings.Get("SpeakerSignoreMoro"), centreDialogue.SpeakerName);
        Assert.Equal("Hello.", centreDialogue.Body);

        centreDialogue.ContinueCommand.Execute(null);
        Assert.Equal("It's Ace.", centreDialogue.Body);

        centreDialogue.ContinueCommand.Execute(null);

        Assert.True(onboarding.State.CommunityCentreDone);
        // The closing "how to call the panel" page is still the introduction
        // even though the tour is banked; its CONTINUE is what ends it.
        Assert.True(session.Story.IntroInProgress);
        Assert.Null(hud.Destination);
        var voiceInfo = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        Assert.True(vm.IsShown);
        // The wake word and the hotkey moved to the menu's clock band; this
        // closing page only names the next step.
        Assert.Null(voiceInfo.LeadText);
        Assert.Null(voiceInfo.DetailText);
        string hint = Assert.IsType<string>(voiceInfo.HintText);
        Assert.Contains("Click \"CONTINUE\"", hint, StringComparison.Ordinal);
        Assert.Contains("select \"JOBS\"", hint, StringComparison.Ordinal);
        Assert.Contains("first taxi shift", hint, StringComparison.Ordinal);

        voiceInfo.ContinueCommand.Execute(null);

        // The tour is over: the panel opens on the menu by itself for the
        // first time since the introduction started, with the reminder above
        // the clock.
        Assert.False(session.Story.IntroInProgress);
        Assert.True(vm.IsShown);
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        Assert.Contains("\"Hey {BRAND}\"", menu.VoiceWakeText, StringComparison.Ordinal);
        Assert.Contains("\"Hey MAZDA\"", menu.VoiceWakeText, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", menu.VoiceWakeText, StringComparison.Ordinal);
        Assert.Contains("CTRL+Shift+C", menu.VoiceHotkeyText, StringComparison.Ordinal);
    }

    [Fact]
    public void BootDuringTheTour_StaysHiddenAndRevealsTheLeg()
    {
        var justin = new AddressLocation("Street 2", 12, HouseSide.Left);
        var progress = new OnboardingState(
            null, null, BirthdayDone: true, JustinAddress: justin, MaxErrandDone: true);
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(
            progress: progress, hud: hud,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5));

        // The menu is the page waiting behind the panel, but the intro tour is
        // not finished: the launch keeps the panel closed and the HUD guides.
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        Assert.False(vm.IsShown);
        Assert.True(hud.Revealed);
        Assert.Equal(StoryFriends.Justin.Id, hud.Destination?.Id);
        Assert.Equal(justin, hud.Destination?.Address);
    }

    [Fact]
    public void BootAfterBirthday_ResumesTheErrandBeforeTheFriends()
    {
        // The birthday hands Max's store over first; a relaunch must not skip
        // it and jump straight to the friend tour.
        var address = new AddressLocation("Street 1", 9, HouseSide.Left);
        var progress = new OnboardingState(
            null, null, BirthdayDone: true,
            MaxStoreId: "max-grocery", MaxStoreAddress: address);
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = Create(
            progress: progress, hud: hud, content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5));

        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        PointOfInterest errand = Assert.IsType<PointOfInterest>(hud.Destination);
        Assert.Equal("max-grocery", errand.Id);
        Assert.Equal(address, errand.Address);
    }

    [Fact]
    public void ReturningAfterTheJustinPickup_StartsAtAnnabellasPlace()
    {
        var justin = new AddressLocation("Street 2", 12, HouseSide.Left);
        var annabella = new AddressLocation("Street 2", 7, HouseSide.Right);
        var progress = new OnboardingState(
            null, null, BirthdayDone: true,
            JustinAddress: justin, AnnabellaAddress: annabella, JustinPickedUp: true,
            MaxErrandDone: true);
        var trunk = new Trunk(
            [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, trunk: trunk, progress: progress);

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();

        Assert.Equal(StoryFriends.Annabella.Id, hud.Destination?.Id);
        Assert.Equal(annabella, hud.Destination?.Address);
        Assert.Equal("HudHintAnnabella", hud.HintKey);
    }

    [Fact]
    public void ReturningAfterTheAnnabellaPickup_StartsTheHomeLeg()
    {
        var progress = new OnboardingState(
            null, null, BirthdayDone: true,
            JustinPickedUp: true, AnnabellaPickedUp: true, MaxErrandDone: true);
        var trunk = new Trunk(
            [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hud = new FakeTelemetryHud();
        var profile = new PlayerProfile("Ace", "Germany", "Brands", "street-2", 5);
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, trunk: trunk, progress: progress, profile: profile);

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();

        PointOfInterest home = Assert.IsType<PointOfInterest>(hud.Destination);
        Assert.Equal(StoryHome.Definition.Id, home.Id);
        Assert.Equal(new AddressLocation("Street 2", 5, HouseSide.Right), home.Address);
        Assert.Equal("HudHintHome", hud.HintKey);
    }

    [Fact]
    public void ReturningAfterPancakes_StartsTheCommunityCentreLeg()
    {
        var centre = new PointOfInterest(
            StoryCommunityCentre.Definition.Id, StoryCommunityCentre.Definition.Name,
            new AddressLocation("Street 1", 17, HouseSide.Left), PoiTypes.CommunityCentre);
        var progress = new OnboardingState(
            null, null, BirthdayDone: true,
            JustinPickedUp: true, AnnabellaPickedUp: true, PancakesDone: true,
            CommunityCentreAddress: centre.Address, MaxErrandDone: true);
        var trunk = new Trunk(
            [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, trunk: trunk, progress: progress);

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();

        Assert.Equal(centre, hud.Destination);
        Assert.Equal("HudHintCommunityCentre", hud.HintKey);
        Assert.Equal("HudArrivedCommunityCentre", hud.Copy?.ArrivedKey);
        Assert.Equal("HudEnter", hud.Copy?.ActionKey);
    }

    [Fact]
    public void JustinsPlace_SitsInThePlayersStreetOffTheirOwnHouse()
    {
        var trunk = new Trunk(
            [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hud = new FakeTelemetryHud();
        var profile = new PlayerProfile("Ace", "Germany", "Brands", "street-2", 5);
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, trunk: trunk, profile: profile, progress: ErrandDone);

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();

        PointOfInterest justin = Assert.IsType<PointOfInterest>(hud.Destination);
        Assert.Equal("Street 2", justin.Address.StreetName);
        Assert.NotEqual(5, justin.Address.HouseNumber);
    }

    [Fact]
    public void JustinsPlace_ReusesTheStoredAddress()
    {
        var trunk = new Trunk(
            [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var stored = new AddressLocation("Street 2", 12, HouseSide.Left);
        var progress = new OnboardingState(
            null, null, BirthdayDone: true, JustinAddress: stored, MaxErrandDone: true);
        var hud = new FakeTelemetryHud();
        var session = Create(
            content: TestSession.OneLineDialogue,
            pois: new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 5),
            hud: hud, trunk: trunk, progress: progress);

        session.Story.ShowDialogue();
        session.Story.FinishDialogue();

        Assert.Equal(stored, hud.Destination?.Address);
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

    [Theory]
    [InlineData("pick up")]
    [InlineData("drop off")]
    public void VoiceJobAction_AnswersTheHudsOwnWords(string spoken)
    {
        // The HUD hands the router the words its action button means (PICK UP /
        // DROP OFF), so the player says what the button prints instead of the
        // generic "enter".
        var hud = new FakeTelemetryHud
        {
            AtDestination = true,
            EnterKeywords = ["enter", spoken],
        };
        var mic = new FakeVoiceListener();
        _ = Create(hud: hud, voice: mic);

        mic.Fire(spoken, isFinal: true);

        Assert.Equal(1, hud.EnterRequests);
    }

    [Fact]
    public void VoiceJobAction_IgnoredWhileTheButtonOnlySaysEnter()
    {
        // The router carries no pickup words of its own: the HUD's table is the
        // only door, so a stale "pick up" cannot fire a plain ENTER button.
        var hud = new FakeTelemetryHud
        {
            AtDestination = true,
            EnterKeywords = ["enter"],
        };
        var mic = new FakeVoiceListener();
        _ = Create(hud: hud, voice: mic);

        mic.Fire("pick up", isFinal: true);

        Assert.Equal(0, hud.EnterRequests);
    }

    [Fact]
    public void VoiceJobAction_IgnoredBeforeArrival()
    {
        var hud = new FakeTelemetryHud
        {
            AtDestination = false,
            EnterKeywords = ["enter", "pick up"],
        };
        var mic = new FakeVoiceListener();
        _ = Create(hud: hud, voice: mic);

        mic.Fire("pick up", isFinal: true);

        Assert.Equal(0, hud.EnterRequests);
    }

    [Fact]
    public void CompletedOnboarding_BootsToTheMenuPage()
    {
        // A finished introduction opens on the menu; Jobs is the door into
        // the career page.
        var (vm, _, _, _) = Create(progress: OnboardingState.Fresh with { BirthdayDone = true });
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        menu.JobsCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void DeliveryButton_OpensCareerPage_BackReturns()
    {
        var catalog = new DeliveryCatalog(
            [new DeliveryStore("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50)],
            0.7, 100);
        var session = Create(catalog: catalog);
        var (vm, _, _, _) = session;
        var control = FinishIntro(session);
        Assert.True(control.DeliveryCommand.CanExecute(null));

        control.DeliveryCommand.Execute(null);
        var delivery = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
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
        var session = Create(catalog: catalog, hubs: hubs);
        var (vm, _, _, _) = session;
        var control = FinishIntro(session);

        control.DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        Assert.False(board.HasStores); // the career gate holds until a hub is bought
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);

        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);

        Assert.Same(board, vm.CurrentScreen);
        Assert.True(board.HasStores);
        Assert.Equal("Credibility Points: 0", board.CredPLine);
        Assert.Equal("adenau-post", board.Rows[0].Store.Key);
    }

    [Fact]
    public void GroceryHub_Purchase_PlacesTheFirstStore_AndShowsItOnTheBoard()
    {
        // Unity StorePlacementHelper.BackfillStoresOnExistingStreets: buying
        // the grocery career immediately opens one store, even before the
        // first expansion. The door is a POI and a delivery-board row.
        var hubs = new HubCatalog([
            new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES",
                    [new HubLevel("Grocers and Sons", 100)])])]);
        var catalog = new DeliveryCatalog([], 0.7, 100);
        var directory = new PoiDirectory();
        var session = Create(
            catalog: catalog, hubs: hubs, poiDirectory: directory);
        var (vm, _, _, _) = session;
        var control = FinishIntro(session);

        control.DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        Assert.False(board.HasStores); // no catalogue row and no placed store yet

        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);

        PointOfInterest store = Assert.Single(
            directory.All, poi => poi.Type == PoiTypes.GroceryStore);
        Assert.Equal("groceries", store.Category);
        Assert.False(string.IsNullOrWhiteSpace(store.OwnerName));

        DeliveryRow row = Assert.Single(board.Rows);
        Assert.Equal(store.Id, row.Store.Key);
        Assert.Equal(store.Name, row.Store.Name);
        Assert.Equal(store.OwnerName, row.Store.Owner);
        Assert.Equal("groceries", row.Store.Category);
        Assert.Equal(200, row.Store.UnitCapacity); // 10 layout slots × 20
        Assert.True(board.HasStores);
    }

    [Fact]
    public void GroceryHub_Purchase_ShowsMaxsAndTheFirstBackfilledStore()
    {
        // The grocery board is made of real shop doors: Max's story grocery,
        // which the introduction already banked, plus the one store Unity's
        // backfill opens at the purchase.
        var address = new AddressLocation("Street 1", 9, HouseSide.Left);
        var max = new PointOfInterest(
            PoiCatalog.MaxGroceryId, "Max's Grocery Store", address,
            PoiTypes.Grocery, "Max Silvestri", "groceries", ["MAPLE SYRUP"]);
        var directory = new PoiDirectory([max]);
        var hubs = new HubCatalog([
            new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES",
                    [new HubLevel("Grocers and Sons", 100)])])]);
        var session = Create(
            catalog: new DeliveryCatalog([], 0.7, 100), hubs: hubs,
            poiDirectory: directory);
        var (vm, _, _, _) = session;
        var control = FinishIntro(session);

        control.DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        Assert.False(board.HasStores); // Max's stays behind the career gate

        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);

        PointOfInterest backfilled = Assert.Single(
            directory.All, poi => poi.Type == PoiTypes.GroceryStore);
        Assert.Equal(
            [PoiCatalog.MaxGroceryId, backfilled.Id],
            board.Rows.Select(row => row.Store.Key).ToArray());

        DeliveryRow maxRow = board.Rows[0];
        Assert.Equal("Max's Grocery Store", maxRow.Store.Name);
        Assert.Equal("Max Silvestri", maxRow.Store.Owner);
        Assert.Equal("groceries", maxRow.Store.Category);
        Assert.Equal(address, maxRow.Store.Address);
    }

    [Fact]
    public void CoffeeHub_Purchase_ShowsOneRealCoffeeShop()
    {
        // The coffee career has no story anchor, so its first board row is the
        // single coffee shop the backfill opens at the purchase.
        var directory = new PoiDirectory();
        var hubs = new HubCatalog([
            new HubTier(1, true, [
                new HubCareer("coffee_beans", "COFFEE BEANS",
                    [new HubLevel("CoffeeUs", 100)])])]);
        var session = Create(
            catalog: new DeliveryCatalog([], 0.7, 100), hubs: hubs,
            poiDirectory: directory,
            // The coffee shelf layout derives from the catalogue's items; the
            // test-shell default only carries groceries.
            storeCatalog: new StoreCatalog(
                [new StoreItem("BEANS", 10.0, 0.1, 0, "coffee_beans")],
                DefaultStock: 20));
        var (vm, _, _, _) = session;
        var control = FinishIntro(session);

        control.DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);

        PointOfInterest shop = Assert.Single(
            directory.All, poi => poi.Type == PoiTypes.CoffeeShop);
        DeliveryRow row = Assert.Single(board.Rows);
        Assert.Equal(shop.Id, row.Store.Key);
        Assert.Equal("coffee_beans", row.Store.Category);
        Assert.DoesNotContain(board.Rows, r => r.Store.Key == PoiCatalog.MaxGroceryId);
    }

    [Fact]
    public void BuyingAHub_PlacesItsPickupDoorAndBanksTheDirectory()
    {
        // Unity PlaceHubOnExistingStreet: the owned hub gets a map door, so
        // the delivery run can pin the pickup leg without a second table.
        var hubs = new HubCatalog(
            [
                new HubTier(1, true, [
                    new HubCareer("groceries", "GROCERIES",
                        [new HubLevel("Grocers and Sons", 100)])]),
            ],
            OwnerNames: ["Elena Marchetti"]);
        var directory = new PoiDirectory();
        var poiStore = new FakePoiDirectoryStore();
        var session = Create(
            catalog: new DeliveryCatalog([], 0.7, 100), hubs: hubs,
            poiDirectory: directory, poiStore: poiStore);
        var (vm, _, _, _) = session;
        FinishIntro(session).DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);

        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);

        PointOfInterest door = Assert.Single(
            directory.All, poi => poi.Type == PoiTypes.Hub);
        Assert.Equal("groceries_grocers_and_sons", door.Id);
        Assert.Equal("groceries", door.Category);
        Assert.Equal("Grocers and Sons", door.Name);
        Assert.NotEmpty(door.OwnerName);
        Assert.Contains(door, poiStore.Saved);
    }

    [Fact]
    public void DeliveryRun_ServesEveryStoreInCartOrder()
    {
        var first = new DeliveryStore(
            "first", "First Store", "Hahn", 10, 40, 2.50, "groceries",
            new AddressLocation("Street 1", 5, HouseSide.Right));
        var second = new DeliveryStore(
            "second", "Second Store", "Fuchs", 10, 40, 2.20, "groceries",
            new AddressLocation("Street 2", 5, HouseSide.Left));
        var catalog = new DeliveryCatalog([first, second], 0.7, 100);
        var city = new CityEconomy(catalog.Stores, CityRules.Default);
        var hubs = new HubCatalog([
            new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES",
                    [new HubLevel("Grocers and Sons", 100)])])]);
        var hud = new FakeTelemetryHud();
        var session = Create(
            catalog: catalog, hubs: hubs, city: city, hud: hud);
        var (vm, clock, tel, _) = session;
        FinishIntro(session).DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);

        board.ToggleOrderCommand.Execute(board.Rows.Single(r => r.Store.Key == "first"));
        board.ToggleOrderCommand.Execute(board.Rows.Single(r => r.Store.Key == "second"));
        board.MainButtonCommand.Execute(null);

        Assert.Equal("HudDeliveryHubDrive", hud.JobGuidance!.DriveKey);
        hud.RaiseJobEnter(); // load the freight once for the category
        Assert.Equal("HudDeliveryStoreDrive", hud.JobGuidance!.DriveKey);
        Assert.Equal("First Store", hud.JobGuidance.TargetName);

        TaxiFlow.Drive(vm, clock, tel, 1.0);
        hud.RaiseJobEnter(); // first store
        Assert.Equal("Second Store", hud.JobGuidance!.TargetName);

        TaxiFlow.Drive(vm, clock, tel, 1.0);
        hud.RaiseJobEnter(); // last store

        Assert.IsType<DeliveryReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(40, city.Live(first).StockedUnits);
        Assert.Equal(40, city.Live(second).StockedUnits);
    }

    [Fact]
    public void DeliveryRun_StoreWithoutDoor_SkipsTheLegAndStillSettles()
    {
        // A hand-edited catalogue row without an address must not strand the
        // run: the leg is skipped, its shelves still fill, and the receipt
        // opens. Every shipped row has a door, so this is the degrade path.
        var store = new DeliveryStore(
            "no-door", "No Door", "Hahn", 10, 40, 2.50, "groceries");
        var catalog = new DeliveryCatalog([store], 0.7, 100);
        var city = new CityEconomy(catalog.Stores, CityRules.Default);
        var hubs = new HubCatalog([
            new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES",
                    [new HubLevel("Grocers and Sons", 100)])])]);
        var hud = new FakeTelemetryHud();
        var session = Create(
            catalog: catalog, hubs: hubs, city: city, hud: hud);
        var (vm, _, _, _) = session;
        FinishIntro(session).DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);
        board.ToggleOrderCommand.Execute(board.Rows[0]);
        board.MainButtonCommand.Execute(null);

        Assert.Equal("HudDeliveryHubDrive", hud.JobGuidance!.DriveKey);
        hud.RaiseJobEnter(); // pickup; the store leg has no door and skips

        Assert.IsType<DeliveryReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(40, city.Live(store).StockedUnits);
    }

    [Fact]
    public void Deliver_PinsTheHubPickupThenTheStoreAndSettlesTheReceipt()
    {
        // PORTPLAN 19: DELIVER hands the run to the host, which pins the hub
        // pickup first, then the store leg. The store leg's measured 8.2 km
        // settle the goods money, the hub ramp's CredP and the owner's
        // familiarity; CONTINUE returns to the delivery board the run came
        // from.
        var catalog = new DeliveryCatalog(
            [new DeliveryStore(
                "adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries",
                new AddressLocation("Street 2", 5, HouseSide.Left))],
            0.7, 100);
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])],
            CredPRamp.UnityDefault, ["Elena Marchetti"]);
        var wallet = EmptyWallet();
        var friends = new FakeFriendshipStore();
        var hud = new FakeTelemetryHud();
        var session = Create(
            catalog: catalog, hubs: hubs, wallet: wallet, speedKmh: 0f,
            friendships: friends, hud: hud);
        var (vm, clock, tel, _) = session;
        var control = FinishIntro(session);
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

        // DELIVER pins the hub pickup first, then the store leg after the
        // freight is loaded; the store leg's 8.2 km prices the receipt.
        Assert.False(vm.IsShown);
        Assert.Equal("HudDeliveryHubDrive", hud.JobGuidance!.DriveKey);
        hud.RaiseJobEnter();
        Assert.Equal("HudDeliveryStoreDrive", hud.JobGuidance!.DriveKey);
        TaxiFlow.Drive(vm, clock, tel, TaxiFlow.DefaultDistanceKm);
        hud.RaiseJobEnter();

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
    public void Deliver_UnownedCar_EarnsTheMoneyButNoCareerProgress()
    {
        // Same run as above, but the live car matches no owned row: the
        // goods money and fare still settle, while XP, standings, country km,
        // zone jobs and the global car km stay untouched.
        var catalog = new DeliveryCatalog(
            [new DeliveryStore(
                "adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries",
                new AddressLocation("Street 2", 5, HouseSide.Left))],
            0.7, 100);
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])],
            CredPRamp.UnityDefault, ["Elena Marchetti"]);
        var wallet = EmptyWallet();
        var rarity = new FakeCarRarity { CarCount = 3 };
        var telemetry = new FakePlayerTelemetry();
        var rivals = new FakeRivals { Result = new RivalsResult(4, 30, 2, 6, false) };
        var drivers = new FakeDriverStore();
        var hud = new FakeTelemetryHud();
        var session = Create(
            catalog: catalog, hubs: hubs, wallet: wallet, speedKmh: 0f,
            carKsId: "ks_mazda_mx5_na", rarity: rarity,
            telemetry: telemetry, rivals: rivals, driverStore: drivers, hud: hud);
        var (vm, clock, tel, _) = session;
        FinishIntro(session).DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);
        board.ToggleOrderCommand.Execute(board.Rows[0]);
        board.MainButtonCommand.Execute(null);

        hud.RaiseJobEnter(); // load the freight at the hub
        TaxiFlow.Drive(vm, clock, tel, TaxiFlow.DefaultDistanceKm);
        hud.RaiseJobEnter(); // deliver the store

        var receipt = Assert.IsType<DeliveryReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(143.40, wallet.Cash);
        Assert.Equal(0.0, wallet.Xp);
        Assert.Contains("0.00", receipt.XpLine, StringComparison.Ordinal);
        Assert.Empty(rarity.Recorded);
        Assert.Empty(telemetry.CountryPushes);
        Assert.Empty(rivals.RefreshedAt);
        Assert.Empty(rivals.Pushed);
        DriverState saved = drivers.Saved[^1];
        Assert.Equal(143.40, saved.Cash);
        Assert.Equal(0, saved.ExpansionJobs);
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
        var wallet = EmptyWallet();
        var session = Create(
            cars: pool, carKsId: "ks_test_hatch", roads: new RoadTestLedger(pool, seed: 0),
            wallet: wallet, speedKmh: 0f);
        var (vm, _, _, _) = session;
        var control = FinishIntro(session);

        control.HaulCommand.Execute(null);
        var receipt = Assert.IsType<CarHaulReceiptScreenViewModel>(vm.CurrentScreen);
        // Class 15 -> tier 6: 0.83 * (20 rating * 6 / 100) = 0.996/min,
        // the 30-minute placeholder leg earns 29.88. No XP rides a haul.
        Assert.Equal(29.88, wallet.Cash);
        Assert.Equal(0.0, wallet.Xp);
        Assert.Equal("TEST HATCH", receipt.CarLine);
        Assert.Equal("The current class is Tier 6", receipt.TierLine);
        Assert.Equal("Wallet: €29.88", receipt.WalletLine);

        // CONTINUE hands back to a fresh control page (taxi-receipt rule).
        receipt.ContinueCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void CarHaulTap_UnknownCar_FallsBackCleanly()
    {
        // No announced identity: the receipt names the customer's car
        // instead and prices the lowest band rather than crashing.
        var wallet = EmptyWallet();
        var session = Create(wallet: wallet, speedKmh: 0f);
        var (vm, _, _, _) = session;
        var control = FinishIntro(session);

        control.HaulCommand.Execute(null);
        var receipt = Assert.IsType<CarHaulReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("the customer's new car", receipt.CarLine);
        // Lowest band (placeholder class 1): 0.83 * (20 * 1 / 100) = 0.166
        // cents-rewritten to a 0.17 rate, 30 minutes earn 4.98.
        Assert.Equal(4.98, wallet.Cash);
        Assert.Equal(0.0, wallet.Xp);
        Assert.Equal("The current class is Tier 1", receipt.TierLine);
        Assert.Equal("Resulting in a rate of €0.17/min", receipt.RateLine);
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
        var session = Create(progress: onboarding, wallet: wallet, driverStore: drivers,
            savedDriver: saved);
        // FakeClock default day = 2026-10-12: yesterday's login continues the
        // streak to day 3, whose bonus is 4 CR.
        Assert.Equal(14.0, wallet.Cash);
        DriverState granted = Assert.Single(drivers.Saved);
        Assert.Equal(3, granted.DaysInARow);
        Assert.Equal(session.Clock.UtcNow.ToString("yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture), granted.LastLoginDate);
        Assert.Equal("2026-10-12", granted.LastDailyBonusDate);
        Assert.True(session.Story.Done);
        // Unity's streak tail: day 3 hands three random skill points out
        // (PORTPLAN 06), banked with the same grant.
        Assert.Equal(63.0,
            wallet.Rating.Cornering + wallet.Rating.Concentration + wallet.Rating.OffTheLine,
            precision: 9);
        Assert.Equal(wallet.Rating.Overall, (granted.Cornering + granted.Concentration + granted.OffTheLine) / 3.0,
            precision: 9);
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
        var session = Create(speedKmh: 0f, wallet: wallet, driverStore: drivers);
        var (vm, clock, tel, _) = session;
        session.Story.FinishDialogue();
        Assert.Empty(drivers.Saved); // FinishDialogue saves onboarding only

        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        DriverState after = drivers.Saved[^1];
        Assert.Equal(1, after.ExpansionJobs);
        Assert.Equal(0, after.ExpansionCount);
        Assert.Equal([OperatingZone.HomeStreetId], after.StreetIds);

        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        after = drivers.Saved[^1];
        // The expansion fired on this second job: the counter resets, the
        // count grows and s1 joins behind the home street.
        Assert.Equal(0, after.ExpansionJobs);
        Assert.Equal(1, after.ExpansionCount);
        Assert.Equal(["home", "s1"], after.StreetIds);
        // Interim: the 1.929 km placeholder map + one mean street (0.9645 km).
        Assert.Equal(2.8935, after.ZoneKm, precision: 4);
    }

    [Fact]
    public void HaulSettle_NeverCountsTheZone()
    {
        // Unity parity: car-hauling pays cash alone — no expansion counter,
        // no bank interest (48). The settle also writes no driver save, so
        // nothing rides the counters in from the tap.
        var wallet = EmptyWallet();
        var drivers = new FakeDriverStore();
        var session = Create(wallet: wallet, driverStore: drivers, speedKmh: 0f);
        var (vm, _, _, _) = session;
        var control = FinishIntro(session);
        int savesBefore = drivers.Saved.Count;
        control.HaulCommand.Execute(null);
        _ = Assert.IsType<CarHaulReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(4.98, wallet.Cash); // the haul paid
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
        var session = Create(speedKmh: 0f, wallet: wallet, driverStore: drivers);
        var (vm, clock, tel, _) = session;
        session.Story.FinishDialogue();
        TaxiFlow.AcceptAndDrive(vm, clock, tel);
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
        var hud = new FakeTelemetryHud();
        var session = Create(speedKmh: 0f, wallet: wallet, hud: hud);
        var (vm, _, _, _) = session;
        session.Story.FinishDialogue();
        var bank = OpenPoi<BankScreenViewModel>(vm, hud, "Bank");
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
        var hud = new FakeTelemetryHud();
        var session = Create(speedKmh: 0f, wallet: wallet, driverStore: drivers,
            savedDriver: saved, hud: hud);
        var (vm, _, _, _) = session;
        session.Story.FinishDialogue();
        var bank = OpenPoi<BankScreenViewModel>(vm, hud, "Bank");
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
    public void TaxiDropoff_DirtiesTheCarDriven()
    {
        // PORTPLAN 33, Unity TaxiJobLifecycleHandler's dropoff block: every
        // completed ride is +1 on the car driven now, banked with the settle.
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var store = new FakeGarageStore();
        var (vm, clock, tel, _) = Create(speedKmh: 0f, garage: garage, garageStore: store);

        TaxiFlow.AcceptAndDrive(vm, clock, tel);

        Assert.Equal(1, garage.Current!.Filth.Value);
        Assert.Equal(1, store.Load()!.Cars!.Single().Filth);
    }

    [Fact]
    public void TaxiDropoff_ClampsTheDirtAtTheCap()
    {
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1).Filth.Add(24);
        var (vm, clock, tel, _) = Create(speedKmh: 0f, garage: garage);

        TaxiFlow.AcceptAndDrive(vm, clock, tel);

        Assert.Equal(25, garage.Current!.Filth.Value);
    }

    [Fact]
    public void TaxiDropoff_BanksTheLifetimeJobCount()
    {
        // PORTPLAN 24, Unity jobsCompleted++: the lifetime total gates the
        // dealer and mechanic POIs and must survive the restart.
        var drivers = new FakeDriverStore();
        var (vm, clock, tel, _) = Create(speedKmh: 0f, driverStore: drivers);

        TaxiFlow.AcceptAndDrive(vm, clock, tel);

        Assert.Equal(1, drivers.Saved[^1].JobsCompleted);
    }

    [Fact]
    public void FreshCareer_OpensNoBusinessDoor()
    {
        // Unity starts with story POIs only: the car wash, bank, dealer,
        // mechanic and insurance open from the street/job thresholds.
        var directory = new PoiDirectory();

        _ = Create(poiDirectory: directory);

        Assert.Empty(directory.All);
    }

    [Fact]
    public void SavedProgress_OpensTheBusinessesTheThresholdsUnlocked()
    {
        var directory = new PoiDirectory();
        var saved = new DriverState(
            500.0, 0.0,
            StreetIds: ["home", "s1", "s2", "s3", "s4", "s5"],
            JobsCompleted: 10);

        _ = Create(
            progress: OnboardingState.Fresh with { BirthdayDone = true },
            savedDriver: saved, poiDirectory: directory);

        Assert.Contains(directory.All, poi => poi.Type == PoiTypes.CarWash);
        Assert.Contains(directory.All, poi => poi.Type == PoiTypes.Dealer);
    }

    [Fact]
    public void TaxiPickup_FilthyCar_RefusesTheRideAndLosesFriendship()
    {
        // Unity's pickup refusal at filth 25: no ride, no receipt, familiarity
        // with the drawn customer drops 1, concentration drops 1 and the page
        // shows the reason.
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1).Filth.Add(25);
        var friends = new FakeFriendshipStore();
        var wallet = new DriverWallet();
        var (vm, _, _, _) = Create(
            speedKmh: 0f, garage: garage, friendships: friends, wallet: wallet);

        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        proposal.AcceptCommand.Execute(null);
        vm.PickupCommand.Execute(null);

        Assert.Same(proposal, vm.CurrentScreen); // no ride
        Assert.True(proposal.HasRefusal);
        Assert.True(vm.IsShown); // the refusal brings the panel back
        Assert.False(vm.PickupCommand.CanExecute(null));
        (string owner, int delta) = Assert.Single(friends.Deltas);
        Assert.Equal(-1, delta);
        Assert.Contains(owner.Split(' ')[0], proposal.RefusalText, StringComparison.Ordinal);
        Assert.Equal(0, friends.GetLevel(owner)); // clamped, never negative
        Assert.Equal(25, garage.Current!.Filth.Value); // the refusal adds no dirt
        // Unity HandleTooDirtyArrival: one concentration point per cancelled
        // pickup.
        Assert.Equal(19.0, wallet.Rating.Concentration);

        // A second ACCEPT on the same proposal and pickup repeats the message,
        // never the penalty (Unity's _tooDirtyArrived guard).
        proposal.AcceptCommand.Execute(null);
        vm.PickupCommand.Execute(null);
        Assert.Same(proposal, vm.CurrentScreen);
        Assert.Single(friends.Deltas);
        Assert.Equal(19.0, wallet.Rating.Concentration);
    }

    [Fact]
    public void DeliveryRun_DirtiesTheCarDriven()
    {
        // Unity DeliveryJobHandler's store arrival, the delivery twin: a
        // completed run is +1 on the car driven now, banked with the settle.
        var catalog = new DeliveryCatalog(
            [new DeliveryStore(
                "adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries",
                new AddressLocation("Street 2", 5, HouseSide.Left))],
            0.7, 100);
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])],
            CredPRamp.UnityDefault, ["Elena Marchetti"]);
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var store = new FakeGarageStore();
        var hud = new FakeTelemetryHud();
        var session = Create(
            catalog: catalog, hubs: hubs, speedKmh: 0f,
            garage: garage, garageStore: store, hud: hud);
        var (vm, clock, tel, _) = session;
        FinishIntro(session).DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        board.MainButtonCommand.Execute(null);
        var hub = Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        hub.ChooseCareerCommand.Execute(hub.Columns[0].Cells[0]);
        hub.OpenCareerCommand.Execute(null);
        hub.BackCommand.Execute(null);
        board.ToggleOrderCommand.Execute(board.Rows[0]);
        board.MainButtonCommand.Execute(null);

        hud.RaiseJobEnter(); // load the freight at the hub
        TaxiFlow.Drive(vm, clock, tel, 1.0);
        hud.RaiseJobEnter(); // deliver the store

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
        var hud = new FakeTelemetryHud();
        var session = Create(
            speedKmh: 0f, garage: garage, garageStore: garageStore,
            wallet: wallet, driverStore: drivers, hud: hud);
        var (vm, _, _, _) = session;
        session.Story.FinishDialogue();

        var wash = OpenPoi<CarWashScreenViewModel>(vm, hud, "Car Wash");
        wash.WashCommand.Execute(null);

        Assert.Equal(0, garage.Current!.Filth.Value);
        Assert.Equal(450.0, wallet.Cash);
        Assert.Equal(0, garageStore.Load()!.Cars!.Single().Filth);
        Assert.Equal(450.0, drivers.Saved[^1].Cash);
        Assert.Equal("Removed 6 filth for CR 50.00", wash.ResultLine);

        wash.BackCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void TaxiXp_OwnedCar_UsesItsClassAndGlobalRarity()
    {
        // The real class/rarity feed: a class-15 car ranked 2nd of 3 earns
        // 8.2 * 15 * 2 / 100 = 2.46 XP, contributes its rounded 8 km to the
        // car board and 8 km to the country aggregate.
        var pool = new List<CarSpec>
        {
            new("ks_test_sedan", 2020, "Test", "Sedan", Passengers: 4, Class: 15),
        };
        var garage = new Garage(pool);
        garage.Add(pool[0], week: 1);
        var wallet = new DriverWallet();
        var rarity = new FakeCarRarity { CarCount = 3 };
        rarity.RarityByKey["ks_test_sedan"] = 2;
        rarity.KmByKey["ks_test_sedan"] = 1234;
        var telemetry = new FakePlayerTelemetry();
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, cars: pool, carKsId: "ks_test_sedan",
            garage: garage, wallet: wallet, rarity: rarity, telemetry: telemetry,
            profile: new PlayerProfile("Ace", "Germany", "Brands", "street-1", 1),
            roads: new RoadTestLedger(pool, seed: 0),
            requests: new FakeCustomerRequestRoller
            {
                Result = CustomerRequestTestData.PassiveRequest,
            });

        TaxiFlow.AcceptAndDrive(vm, clock, tel);

        Assert.Equal(2.46, wallet.Xp, precision: 9);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(
            "XP: (8.2 km × class 15 × rarity 2) / 100 × street knowledge 100%",
            receipt.XpCalcLine);
        Assert.True(receipt.HasRarityLine);
        Assert.Contains("2nd most commonly used", receipt.RarityLine, StringComparison.Ordinal);
        Assert.Equal(("ks_test_sedan", 8), Assert.Single(rarity.Recorded));
        Assert.Equal(("Germany", 8), Assert.Single(telemetry.CountryPushes));
    }

    [Fact]
    public void TaxiXp_UnownedCar_EarnsTheFareButNoCareerProgress()
    {
        // A free-roam car that is not in the garage pays the fare, but
        // contributes nothing to the career: no XP, no world standings, no
        // country km, no zone job and no global car km. Class/rarity read 0
        // so the receipt's own formula explains the zero XP.
        var pool = new List<CarSpec>
        {
            new("ks_test_sedan", 2020, "Test", "Sedan", Passengers: 4, Class: 15),
        };
        var wallet = EmptyWallet();
        var rarity = new FakeCarRarity { CarCount = 3 };
        var telemetry = new FakePlayerTelemetry();
        var rivals = new FakeRivals { Result = new RivalsResult(4, 30, 2, 6, false) };
        var drivers = new FakeDriverStore();
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, cars: pool, carKsId: "ks_test_sedan",
            garage: new Garage(pool), wallet: wallet,
            rarity: rarity, telemetry: telemetry, rivals: rivals,
            driverStore: drivers,
            profile: new PlayerProfile("Ace", "Germany", "Brands", "street-1", 1),
            roads: new RoadTestLedger(pool, seed: 0),
            requests: new FakeCustomerRequestRoller
            {
                Result = CustomerRequestTestData.PassiveRequest,
            });

        TaxiFlow.AcceptAndDrive(vm, clock, tel);

        Assert.Equal(0.0, wallet.Xp);
        // The 4-seat fare still pays, and the starter bonus rides along even
        // for an unowned car (it is a payout line, not career progress).
        Assert.Equal(454.22, wallet.Cash);
        var receipt = Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(
            "XP: (8.2 km × class 0 × rarity 0) / 100 × street knowledge 100%",
            receipt.XpCalcLine);
        Assert.Contains("0.00", receipt.XpLine, StringComparison.Ordinal);
        Assert.False(receipt.HasRarityLine);
        Assert.Empty(rarity.Recorded);
        Assert.Empty(telemetry.CountryPushes);
        Assert.Empty(rivals.RefreshedAt);
        Assert.Empty(rivals.Pushed);
        // The settle still banks the cash; the zone counter stays put.
        DriverState saved = drivers.Saved[^1];
        Assert.Equal(454.22, saved.Cash);
        Assert.Equal(0.0, saved.Xp);
        Assert.Equal(0, saved.ExpansionJobs);
    }

    [Fact]
    public void TaxiXp_NoLiveIdentity_FallsBackToTheGarageCurrentCar()
    {
        var pool = new List<CarSpec>
        {
            new("ks_test_sedan", 2020, "Test", "Sedan", Passengers: 4, Class: 15),
        };
        var garage = new Garage(pool);
        garage.Add(pool[0], week: 1);
        var wallet = new DriverWallet();
        var rarity = new FakeCarRarity { CarCount = 3 };
        rarity.RarityByKey["ks_test_sedan"] = 2;
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, cars: pool, garage: garage,
            wallet: wallet, rarity: rarity,
            profile: new PlayerProfile("Ace", "Germany", "Brands", "street-1", 1),
            roads: new RoadTestLedger(pool, seed: 0),
            requests: new FakeCustomerRequestRoller
            {
                Result = CustomerRequestTestData.PassiveRequest,
            });

        TaxiFlow.AcceptAndDrive(vm, clock, tel);

        Assert.Equal(2.46, wallet.Xp, precision: 9);
        Assert.Equal(("ks_test_sedan", 8), Assert.Single(rarity.Recorded));
    }

    [Fact]
    public void ShiftEnd_BanksAndPushesTheBestShift()
    {
        var shifts = new ShiftRecord(null);
        var telemetry = new FakePlayerTelemetry();
        var board = new FakeShiftStandings();
        var (vm, clock, tel, _) = Create(
            speedKmh: 0f, shifts: shifts, telemetry: telemetry,
            shiftStandings: board,
            requests: new FakeCustomerRequestRoller
            {
                Result = CustomerRequestTestData.PassiveRequest,
            });

        TaxiFlow.AcceptAndDrive(vm, clock, tel);
        clock.Advance(TimeSpan.FromMinutes(31));
        vm.Poll();

        Assert.True(shifts.BestEarnings > 0.0);
        var pushed = Assert.Single(telemetry.ShiftPushes);
        Assert.Equal(shifts.BestEarnings, pushed.Earnings);
        Assert.Equal(shifts.BestFirstEarnedTicks, pushed.Ticks);
        Assert.True(pushed.Ticks > 0);
        // The page reports the shift that just closed, and its board was
        // refreshed for the record, not for the shift's own earnings.
        var page = Assert.IsType<ShiftEndedScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Jobs completed: 1", page.JobsLine);
        Assert.Equal(1, shifts.ShiftsCompleted);
        Assert.Equal((shifts.BestEarnings, shifts.BestFirstEarnedTicks), Assert.Single(board.Refreshes));
    }
}
