using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Onboarding flow: brand -&gt; grandma's car -&gt; birthday -&gt; jobs, persisted.</summary>
public sealed class OnboardingTests
{
    private static readonly OnboardingContent Content = new(
        ["Honda", "Mazda"],
        new Dictionary<string, string[]>(),
        [new GrandmaCar("ks_mazda_mx5_na", "Mazda MX-5 NA", 1990, 12000, null),
         new GrandmaCar("ks_datsun_240z_fairlady", "Datsun 240Z", 1970, 35000, null)],
        [new DialogueLine("GRANDMA", "Happy birthday!"),
         new DialogueLine("YOU", "Thanks!")],
        [new DialogueLine("GRANDMA", "Pancakes!")],
        [],
        []);

    // Test-shell career: no hub content, just Max's gift balance.
    private static DeliveryCareer HubCareerShell() =>
        new(HubCatalog.Default, DeliveryCatalog.Default.StartCredP);

    private static MainViewModel Create(
        OnboardingState? start = null, FakeClock? clock = null,
        FakeProfileStore? profiles = null, FakeOnboardingStore? onboarding = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var store = onboarding ?? new FakeOnboardingStore();
        if (start is not null) store.Save(start);
        clock ??= new FakeClock();
        // One instance per service graph: the saver and the fleet share the
        // stores and state the VM gets (the root builds them the same way).
        var game = new GameClock(clock, clock.UtcNow);
        var shift = new TaxiShift();
        var shiftStore = new FakeShiftStore();
        var contentSource = new FakeContentSource(Content);
        var logger = new NullLogger();
        var city = new CityEconomy([], CityRules.Default);
        var cityStore = new FakeCityStore();
        var roads = new RoadTestLedger([], seed: 0);
        var roadTestStore = new FakeRoadTestStore();
        var clockStore = new FakeGameClockStore();
        var wallet = new DriverWallet();
        var driverStore = new FakeDriverStore();
        var streak = new DailyStreak(clock, null);
        var zone = new OperatingZone(null, seed: 0);
        IReadOnlyList<CarSpec> cars = [];
        var garage = new Garage([]);
        var garageStore = new FakeGarageStore();
        var hunger = new Hunger(HungerRules.Default);
        var hungerStore = new FakeHungerStore();
        var shifts = new ShiftRecord(null);
        var jobs = new JobLedger();
        var trunk = new Trunk();
        var carBrands = new FakeCarBrandSource();
        var beat = new VoiceBeat(clock);
        var fares = FareTable.Default;
        var taxiRules = TaxiRules.Default;
        var catalog = DeliveryCatalog.Default;
        var career = HubCareerShell();
        var deliveryFares = DeliveryFareTable.Default;
        var friendships = new FakeFriendshipStore();
        var brandMileage = new FakeBrandMileageStore();
        var rivals = new FakeRivals();
        var rarity = new OfflineCarRarity();
        var playerTelemetry = new OfflinePlayerTelemetry();
        var saver = new GameStateSaver(
            clock, game, clockStore, driverStore, garageStore, cityStore,
            hungerStore, roadTestStore, shiftStore, wallet, streak, zone, trunk,
            garage, city, hunger, roads, shift, shifts, jobs);
        var fleet = new FleetService(
            garage, cars, game, wallet, InsuranceRules.Default, logger, carBrands, contentSource);
        var meter = new TaxiDriveMeter(taxiRules.MaxMeterSpeedKmh);
        var deliveryBoard = new DeliveryBoard(catalog);
        var breakdowns = new TripBreakdowns(
            new BreakdownStrategy(MaintenanceRules.Default, seed: 0),
            garage, store, wallet, rivals, saver, beat, logger);
        var settlement = new JobSettlement(
            wallet, garage, shift, zone, jobs, streak, clock, fleet, rarity,
            friendships, brandMileage, playerTelemetry, rivals, deliveryBoard, career,
            city, deliveryFares, meter, fares, taxiRules, beat, null, logger);
        var shops = new ShopInteriors(
            wallet, garage, game, InsuranceRules.Default, cars, saver, beat, logger);
        var proposals = TaxiTestData.Proposer();
        var taxi = new TaxiJobFlow(
            proposals, AddressMap.Default, carBrands, cars, null,
            CustomerRequestTable.Empty, null, taxiRules, fares, garage, wallet,
            friendships, fleet, game, clock, beat, meter, saver, rivals, breakdowns,
            null, logger);
        var delivery = new DeliveryFlow(
            deliveryBoard, career, city, friendships, meter, breakdowns, beat, logger);
        return new MainViewModel(new FakeTelemetry(), clock, game, shift, shiftStore,
            new FakeStore(), logger, fares,
            store, contentSource,
            profiles ?? new FakeProfileStore(), new OfflinePlayerNameRegistry(),
            AddressMap.Default, "ElGee",
            new FakeVoiceListener(available: false),
            carBrands, TodoTable.Default, deliveryBoard, career,
            deliveryFares,
            city, cityStore,
            roads, roadTestStore,
            clockStore, 1, beat, wallet, driverStore,
            streak, zone, friendships, cars, rivals,
            playerTelemetry, shifts,
            new OfflineShiftStandings(),
            hunger, hungerStore,
            garage, garageStore, InsuranceRules.Default,
            new Random(0),
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 0),
            new PoiDirectory(), new FakePoiDirectoryStore(),
            new PoiSpawner(
                AddressMap.Default, PoiCatalog.Default,
                new ProceduralNameGenerator(NamePoolTable.Default, seed: 0), seed: 0),
            jobs,
            new FakePreferencesStore(), PlayerPreferences.Default, new FakeCultureService(),
            trunk, taxiRules, saver, fleet, settlement, new OverlayShell(), meter, shops,
            taxi, delivery);
    }

    // The first-run page (unit + language) sits before the name form; its
    // CONTINUE is the only way into the walk.
    private static void ContinuePreferences(MainViewModel vm) =>
        Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen).ContinueCommand.Execute(null);

    // Preferences page -> name + address form -> brand page.
    private static void ToBrandPage(MainViewModel vm)
    {
        ContinuePreferences(vm);
        Assert.IsType<OpeningScreenViewModel>(vm.CurrentScreen).ContinueCommand.Execute(null);
    }

    [Fact]
    public void FreshBoot_ShowsPreferencesThenOpeningThenBrand()
    {
        var profiles = new FakeProfileStore();
        var vm = Create(profiles: profiles);
        Assert.True(vm.IsOnboarding);
        Assert.False(vm.JobsVisible);

        var preferences = Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(2, preferences.UnitOptions.Count);
        Assert.Equal(2, preferences.LanguageOptions.Count);
        preferences.ContinueCommand.Execute(null);

        var opening = Assert.IsType<OpeningScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("ElGee", opening.DisplayName);
        Assert.Equal("Germany", opening.Country);
        Assert.Equal("Brands", opening.City);
        Assert.Equal(2, opening.Streets.Count);
        Assert.Equal(33, opening.HouseNumbers.Count);
        Assert.True(opening.CanContinue);

        opening.ContinueCommand.Execute(null);
        var brands = Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(["Honda", "Mazda"], brands.Brands);

        PlayerProfile saved = Assert.IsType<PlayerProfile>(profiles.Profile);
        Assert.Equal("ElGee", saved.DisplayName);
        Assert.Equal("street-1", saved.StreetId);
        Assert.Equal(1, saved.HouseNumber);
    }

    [Fact]
    public void SavedProfile_PrefillsOpeningScreen()
    {
        var saved = new PlayerProfile("Edited Name", "Germany", "Brands", "street-2", 17);
        var vm = Create(
            new OnboardingState(null, null, false, Stage: OnboardingStage.Opening),
            profiles: new FakeProfileStore(saved));

        var opening = Assert.IsType<OpeningScreenViewModel>(vm.CurrentScreen);
        // The saved profile wins over the Steam name on later launches so a
        // manual edit is never overwritten at boot.
        Assert.Equal("Edited Name", opening.DisplayName);
        Assert.Equal("street-2", opening.SelectedStreet!.Id);
        Assert.Equal(17, opening.SelectedHouseNumber);
    }

    [Fact]
    public void DoneBoot_StartsOnTheMenuPage()
    {
        // A finished introduction is not replayed: once the whole tour is
        // banked, the next launch opens the menu with its three tiles, and
        // Jobs leads to the career page.
        var vm = Create(OnboardingState.Fresh with
        {
            BirthdayDone = true,
            MaxErrandDone = true,
            JustinPickedUp = true,
            AnnabellaPickedUp = true,
            PancakesDone = true,
            CommunityCentreDone = true,
        });
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        Assert.False(vm.IsOnboarding);
        Assert.False(vm.IntroTourInProgress);
        Assert.True(vm.IsShown);

        menu.JobsCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void BootBeforeTheBirthday_StartsHiddenAndResumesTheWalk()
    {
        // The walk pages are the introduction the player must see, so a new
        // career still opens the panel at once (with a mic the app root calls
        // Show() for this same page).
        var vm = Create();

        Assert.True(vm.IsShown);
        Assert.False(vm.IntroTourInProgress);
        Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void BootDuringTheTour_StartsHiddenUntilTheTourEnds()
    {
        var vm = Create(OnboardingState.Fresh with { BirthdayDone = true });

        Assert.True(vm.IntroTourInProgress);
        Assert.False(vm.IsShown);
    }

    [Theory]
    [InlineData(OnboardingStage.Opening, typeof(OpeningScreenViewModel))]
    [InlineData(OnboardingStage.Brand, typeof(BrandScreenViewModel))]
    [InlineData(OnboardingStage.GrandmaCar, typeof(GrandmaCarScreenViewModel))]
    [InlineData(OnboardingStage.VoiceInfo, typeof(VoiceInfoScreenViewModel))]
    [InlineData(OnboardingStage.Dialogue, typeof(DialogueScreenViewModel))]
    public void SavedStage_ReopensThatPage(OnboardingStage stage, Type page)
    {
        var vm = Create(new OnboardingState(null, null, false, Stage: stage));

        Assert.IsType(page, vm.CurrentScreen);
    }

    [Fact]
    public void EveryPage_IsBankedBeforeItOpens()
    {
        var store = new FakeOnboardingStore();
        var vm = Create(onboarding: store);
        Assert.Equal(OnboardingStage.Preferences, store.State.Stage);

        ContinuePreferences(vm);
        Assert.Equal(OnboardingStage.Opening, store.State.Stage);

        Assert.IsType<OpeningScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);
        Assert.Equal(OnboardingStage.Brand, store.State.Stage);

        vm.ChooseBrand("Mazda");
        Assert.Equal(OnboardingStage.GrandmaCar, store.State.Stage);

        vm.ChooseCar("ks_mazda_mx5_na");
        Assert.Equal(OnboardingStage.VoiceInfo, store.State.Stage);

        Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);
        Assert.Equal(OnboardingStage.Dialogue, store.State.Stage);

        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null);

        Assert.True(store.State.BirthdayDone);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void LegacySaveWithoutAStage_ResumesAtTheFurthestBankedPick()
    {
        // Saves written before the stage existed still carry their picks; the
        // walk resumes at the page those picks imply instead of replaying.
        var vm = Create(new OnboardingState("Mazda", "ks_mazda_mx5_na", BirthdayDone: false));

        Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void HotkeyCall_OpensMenuPageFromAnyPage()
    {
        // Ctrl+Shift+C is a call to the app like the wake word: the hidden ->
        // shown edge opens the menu page instead of resuming the page the
        // player was last on.
        var vm = Create(OnboardingState.Fresh with { BirthdayDone = true });
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);

        vm.ToggleShowCommand.Execute(null); // hide
        vm.ToggleShowCommand.Execute(null); // call it back
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void ChooseBrand_PersistsAndShowsCars()
    {
        var vm = Create();
        vm.ChooseBrand("Mazda");
        var cars = Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(2, cars.Options.Count);
        Assert.Equal("1990 Mazda MX-5 NA", cars.Options[0].Label);
    }

    [Fact]
    public void ChooseUnknownBrand_Ignored()
    {
        var vm = Create();
        ToBrandPage(vm);
        vm.ChooseBrand("Nope");
        Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void FullFlow_PersistsEveryStep()
    {
        var store = new FakeOnboardingStore();
        var clock = new FakeClock();
        // Same shell as the other onboarding tests: one shared service graph.
        var vm = Create(clock: clock, onboarding: store);

        vm.ChooseBrand("Honda");
        Assert.Equal("Honda", store.State.FavoriteBrand);

        vm.ChooseCar("ks_datsun_240z_fairlady");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("ks_datsun_240z_fairlady", store.State.GrandmaCarId);

        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(Strings.Get("BirthdayTitle"), dialogue.Title);
        Assert.Equal("Grandma", dialogue.SpeakerName);

        dialogue.ContinueCommand.Execute(null);
        Assert.Equal("You", dialogue.SpeakerName);

        dialogue.ContinueCommand.Execute(null); // last page finishes
        // The finished flow lands on the menu; progress is persisted.
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        Assert.True(store.State.BirthdayDone);
    }

    [Fact]
    public void BrandPage_MouseOnly_VoiceOff()
    {
        // Companion parity: brand/car pages expose no voice commands at all;
        // clicks drive them. Only the dialogue's Continue responds to speech.
        var vm = Create();
        ToBrandPage(vm);
        var brands = Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
        brands.ChooseBrandCommand.Execute("Mazda");
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void ChooseCar_ShowsVoiceInfo()
    {
        var vm = Create();
        vm.ChooseBrand("Mazda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        Assert.Contains("Control navigation with your voice.", voice.LeadText, StringComparison.Ordinal);
        Assert.DoesNotContain("HEY", voice.LeadText, StringComparison.Ordinal);
        Assert.Null(voice.HintText);
    }

    [Fact]
    public void VoiceInfoContinue_ShowsDialogue()
    {
        var vm = Create();
        vm.ChooseBrand("Mazda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void VoiceInfoVoiceContinue_GlowsThenWaitsBeatThenShowsDialogue()
    {
        // Voice navigation waits out the half-second beat so the teal glow
        // is actually seen; clicks (the test above) stay instant.
        var clock = new FakeClock();
        var vm = Create(clock: clock);
        vm.ChooseBrand("Mazda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        string? label = VoiceMatch.MatchLabel("could you please continue", voice.VoiceActions.Keys);
        Assert.Equal("CONTINUE", label);
        voice.VoiceActions[label!].Invoke();
        Assert.True(voice.ContinueFlash);
        Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen); // still glowing, not yet flipped

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        vm.Poll();
        Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.False(voice.ContinueFlash);
    }

    [Fact]
    public void DialogueVoiceContinue_GlowsThenAdvancesPages()
    {
        // Every dialogue page acknowledges the voice tap for the shared beat:
        // the teal text is shown before the page turns, not only on the last
        // page.
        var clock = new FakeClock();
        var vm = Create(clock: clock);
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        string? label = VoiceMatch.MatchLabel("could you please continue", dialogue.VoiceActions.Keys);
        Assert.Equal("CONTINUE", label);
        dialogue.VoiceActions[label!].Invoke();
        Assert.True(dialogue.ContinueFlash);
        Assert.Equal("Happy birthday!", dialogue.Body); // still glowing on page 1

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        vm.Poll();
        Assert.False(dialogue.ContinueFlash);
        Assert.Equal("Thanks!", dialogue.Body);
    }

    [Fact]
    public void ContentLoader_ValidDir_LoadsAll()
    {
        string dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "brands.json"), """["Mazda","Honda"]""");
            File.WriteAllText(Path.Combine(dir, "brand_aliases.json"), """{"lancia":["lancer"]}""");
            File.WriteAllText(Path.Combine(dir, "grandma_cars.json"),
                """[{"id":"ks_mazda_mx5_na","name":"Mazda MX-5 NA","year":1990,"price":12000}]""");
            File.WriteAllText(Path.Combine(dir, "grandma_dialogue.json"),
                """[{"speaker":"GRANDMA","text":"Hi!"}]""");
            File.WriteAllText(Path.Combine(dir, "pancake_dialogue.json"),
                """[{"speaker":"GRANDMA","text":"Pancakes!"}]""");
            File.WriteAllText(Path.Combine(dir, "community_centre_dialogue.json"),
                """[{"speaker":"SIGNORE MORO","text":"Welcome!"}]""");
            File.WriteAllText(Path.Combine(dir, "max_dialogue.json"),
                """[{"speaker":"MAX","text":"Easy job."}]""");
            File.WriteAllText(Path.Combine(dir, "voice_confusions.json"),
                """{"Your":"NEW","dear":"tier","two words":"x","y":"y","":"z"}""");
            var log = new TestLog();
            OnboardingContent loaded = new JsonOnboardingContent(dir, log).Load();
            Assert.Equal(["Honda", "Mazda"], loaded.Brands); // sorted
            Assert.Equal(["lancer"], loaded.Aliases["lancia"]);
            Assert.Single(loaded.Cars);
            Assert.Null(loaded.Cars[0].ImagePath); // no Thumbs shipped in temp dir
            Assert.Single(loaded.Dialogue);
            Assert.Single(loaded.PancakeDialogue);
            Assert.Single(loaded.CommunityCentreDialogue);
            Assert.Single(loaded.MaxDialogue);
            // "Your"/"NEW" normalized; multi-word, self-map and empty halves dropped.
            Assert.Equal(2, loaded.Confusions!.Count);
            Assert.Equal("new", loaded.Confusions["your"]);
            Assert.Equal("tier", loaded.Confusions["dear"]);
            Assert.Empty(log.Warnings);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ContentLoader_ShippedData_KeepsLoggedWakeDoors()
    {
        // The logged-mishearing tables ARE the wake detector's reach: if an
        // edit drops the 2026-10-14 doors ("a"/"have"/"hair"/"halo" read as
        // "hey", lotus answering to "lord of"), the "hey lotus" misses come
        // back silently in-game. This fails at the table instead.
        var log = new TestLog();
        OnboardingContent loaded = new JsonOnboardingContent(log).Load();
        Assert.Equal("hey", loaded.Confusions!["a"]);
        Assert.Equal("hey", loaded.Confusions["have"]);
        Assert.Equal("hey", loaded.Confusions["hair"]);
        Assert.Equal("hey", loaded.Confusions["halo"]);
        Assert.Contains("lord of", loaded.Aliases["lotus"]);
        Assert.Contains("majority", loaded.Aliases["maserati"]); // 2026-10-14 Maserati retry storm
        Assert.Contains("muster", loaded.Aliases["mazda"]); // 2026-10-14 Mazda retry storm
        Assert.Contains("many", loaded.Aliases["mazda"]); // live 17:13 retest garble
        Assert.Contains("me some", loaded.Aliases["mazda"]); // live 17:17 retest garble
        Assert.Contains("nissan", loaded.Aliases["datsun"]); // live 17:13 Datsun garble
        Assert.Contains("nathan", loaded.Aliases["datsun"]); // live 17:17 Datsun garble
        Assert.Contains("henderson", loaded.Aliases["datsun"]); // live 17:17:36 Datsun fused-word garble
        Assert.NotEmpty(loaded.PancakeDialogue); // home leg's dialogue ships with the build
        Assert.NotEmpty(loaded.CommunityCentreDialogue); // post-pancake leg's dialogue ships too
        Assert.NotEmpty(loaded.MaxDialogue); // first post-intro Max visit ships its offer
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void ContentLoader_MissingDir_EmptiesWithWarnings()
    {
        var log = new TestLog();
        OnboardingContent loaded = new JsonOnboardingContent(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), log).Load();
        Assert.Empty(loaded.Brands);
        Assert.Empty(loaded.Cars);
        Assert.Empty(loaded.Dialogue);
        Assert.Empty(loaded.PancakeDialogue);
        Assert.Empty(loaded.CommunityCentreDialogue);
        Assert.Empty(loaded.MaxDialogue);
        Assert.Empty(loaded.Confusions!); // loader always passes a (possibly empty) table
        Assert.Equal(8, log.Warnings.Count);
    }

    [Fact]
    public void Content_ReReads_AliasEdits()
    {
        // WHY: the 2026-10-14 wake-word sessions lost minutes per retry —
        // Data edits were made while the overlay ran and silently tested a
        // stale in-memory snapshot. The Content view must re-read on change.
        string dir = NewTempDir();
        try
        {
            string file = Path.Combine(dir, "brand_aliases.json");
            File.WriteAllText(file, """{"mazda":["muster"]}""");
            var source = new JsonOnboardingContent(dir, new TestLog());
            Assert.Equal(["muster"], source.Content.Aliases["mazda"]);

            File.WriteAllText(file, """{"mazda":["muster","many"]}""");
            // WHY pin after the write: the test writes twice inside one
            // filesystem tick; the reload trigger is the write time itself.
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(1));
            Assert.Equal(["muster", "many"], source.Content.Aliases["mazda"]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void OnboardingStore_RoundTrips()
    {
        string file = Path.GetTempFileName();
        try
        {
            var store = new JsonOnboardingStore(file);
            Assert.Equal(OnboardingState.Fresh, store.Load());
            var state = new OnboardingState(
                "Mazda", "ks_mazda_mx5_na", true,
                MaxStoreId: "max-grocery",
                MaxStoreAddress: new AddressLocation("Street 1", 12, HouseSide.Left),
                CommunityCentreAddress: new AddressLocation("Street 2", 7, HouseSide.Right),
                Stage: OnboardingStage.VoiceInfo);
            store.Save(state);
            Assert.Equal(state, new JsonOnboardingStore(file).Load());
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void OnboardingStore_InvalidFile_FallsBackToFresh()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");
            Assert.Equal(OnboardingState.Fresh, new JsonOnboardingStore(file).Load());
        }
        finally { File.Delete(file); }
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }
}
