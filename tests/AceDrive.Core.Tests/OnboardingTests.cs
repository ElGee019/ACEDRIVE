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
        FakeProfileStore? profiles = null, FakeOnboardingStore? onboarding = null) =>
        CreateSession(start, clock, profiles, onboarding).Vm;

    // The walk's verbs and flags live on the story flow, so a test that drives
    // the introduction keeps the whole session instead of a coordinator that
    // forwards them.
    private static TestSessionResult CreateSession(
        OnboardingState? start = null, FakeClock? clock = null,
        FakeProfileStore? profiles = null, FakeOnboardingStore? onboarding = null)
    {
        var store = onboarding ?? new FakeOnboardingStore();
        if (start is not null) store.Save(start);
        // The walk suite's session: an empty career and the smallest content
        // that can carry the pages, driven through the shared builder.
        return MainViewModelTests.Create(
            clock: clock, onboarding: store, profileStore: profiles,
            content: Content, cars: [], garage: new Garage([]),
            city: new CityEconomy([], CityRules.Default),
            roads: new RoadTestLedger([], seed: 0),
            shiftStandings: new OfflineShiftStandings(),
            rarity: new OfflineCarRarity(),
            telemetry: new OfflinePlayerTelemetry());
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
        var session = CreateSession(OnboardingState.Fresh with
        {
            BirthdayDone = true,
            MaxErrandDone = true,
            JustinPickedUp = true,
            AnnabellaPickedUp = true,
            PancakesDone = true,
            CommunityCentreDone = true,
        });
        var vm = session.Vm;
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        Assert.False(session.Story.IntroTourInProgress);
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
        var session = CreateSession();
        var vm = session.Vm;

        Assert.True(vm.IsShown);
        Assert.False(session.Story.IntroTourInProgress);
        Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void BootDuringTheTour_StartsHiddenUntilTheTourEnds()
    {
        var session = CreateSession(OnboardingState.Fresh with { BirthdayDone = true });
        var vm = session.Vm;

        Assert.True(session.Story.IntroTourInProgress);
        Assert.False(vm.IsShown);
    }

    [Theory]
    [InlineData(OnboardingStage.Opening, typeof(OpeningScreenViewModel))]
    [InlineData(OnboardingStage.Brand, typeof(BrandScreenViewModel))]
    [InlineData(OnboardingStage.GrandmaCar, typeof(GrandmaCarScreenViewModel))]
    [InlineData(OnboardingStage.VoiceInfo, typeof(VoiceConsentScreenViewModel))]
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
        var session = CreateSession(onboarding: store);
        var vm = session.Vm;
        Assert.Equal(OnboardingStage.Preferences, store.State.Stage);

        ContinuePreferences(vm);
        Assert.Equal(OnboardingStage.Opening, store.State.Stage);

        Assert.IsType<OpeningScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);
        Assert.Equal(OnboardingStage.Brand, store.State.Stage);

        session.Story.ChooseBrand("Mazda");
        Assert.Equal(OnboardingStage.VoiceInfo, store.State.Stage);

        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen)
            .ConfirmCommand.Execute(null);
        Assert.True(store.State.VoiceConsent);
        Assert.Equal(OnboardingStage.Dialogue, store.State.Stage);

        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null);

        // Grandma's last line sends the player out for maple syrup; the car
        // page is the walk's closing page, banked before it paints.
        Assert.Equal(OnboardingStage.GrandmaCar, store.State.Stage);
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);

        Assert.True(store.State.BirthdayDone);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void LegacySaveWithoutAStage_ResumesAtTheFurthestBankedPick()
    {
        // Saves written before the stage existed still carry their picks; the
        // walk resumes at the page those picks imply instead of replaying.
        var vm = Create(new OnboardingState("Mazda", "ks_mazda_mx5_na", BirthdayDone: false));

        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void HotkeyCall_OpensMenuPageFromAnyPage()
    {
        // Ctrl+Shift+C is a call to the app like the wake word: the hidden ->
        // shown edge opens the menu page instead of resuming the page the
        // player was last on.
        var session = CreateSession(OnboardingState.Fresh with { BirthdayDone = true });
        var vm = session.Vm;
        session.Story.ChooseBrand("Honda");
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen)
            .ConfirmCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null);
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);

        vm.ToggleShowCommand.Execute(null); // hide
        vm.ToggleShowCommand.Execute(null); // call it back
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void ChooseBrand_ShowsVoiceConsent()
    {
        var session = CreateSession();
        var vm = session.Vm;
        session.Story.ChooseBrand("Mazda");
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void CarPage_ContinuePicksTheOfferAndEndsTheWalk()
    {
        var store = new FakeOnboardingStore();
        var session = CreateSession(onboarding: store);
        var vm = session.Vm;
        session.Story.ChooseBrand("Mazda");
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen)
            .ConfirmCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null);

        // The page is the picture of the car she hands over, one offer and one
        // CONTINUE that banks it; the pick is not a button per car any more.
        var car = Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("ks_mazda_mx5_na", car.Car.Id);
        Assert.False(car.HasImage);

        car.ContinueCommand.Execute(null);

        Assert.Equal("ks_mazda_mx5_na", store.State.GrandmaCarId);
        Assert.True(store.State.BirthdayDone);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void ChooseUnknownBrand_Ignored()
    {
        var session = CreateSession();
        var vm = session.Vm;
        ToBrandPage(vm);
        session.Story.ChooseBrand("Nope");
        Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void FullFlow_PersistsEveryStep()
    {
        var store = new FakeOnboardingStore();
        var clock = new FakeClock();
        // Same shell as the other onboarding tests: one shared service graph.
        var session = CreateSession(clock: clock, onboarding: store);
        var vm = session.Vm;

        session.Story.ChooseBrand("Honda");
        Assert.Equal("Honda", store.State.FavoriteBrand);

        var voice = Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
        voice.ConfirmCommand.Execute(null);
        Assert.True(store.State.VoiceConsent);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(Strings.Get("BirthdayTitle"), dialogue.Title);
        Assert.Equal("Grandma", dialogue.SpeakerName);

        dialogue.ContinueCommand.Execute(null);
        Assert.Equal("You", dialogue.SpeakerName);

        dialogue.ContinueCommand.Execute(null); // last page: grandma's car
        var car = Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen);
        car.ContinueCommand.Execute(null);
        Assert.Equal("ks_mazda_mx5_na", store.State.GrandmaCarId);

        // The finished flow lands on the menu; progress is persisted.
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        Assert.True(store.State.BirthdayDone);
    }

    [Fact]
    public void BrandPage_MouseOnly_VoiceOff()
    {
        // The brand page sits before the consent question, so it stays
        // mouse-only; the car page after the birthday does answer CONTINUE to
        // speech (the mic is live by then).
        var vm = Create();
        ToBrandPage(vm);
        var brands = Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
        brands.ChooseBrandCommand.Execute("Mazda");
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void VoiceConsentPage_AsksBeforeTheMicOpens()
    {
        // The consent page is only the permission ask; the lesson belongs to
        // the birthday page, where the mic is live. The ask must not sneak the
        // wake word in, and the page exposes no spoken buttons because there
        // is no open mic to answer it with.
        var session = CreateSession();
        var vm = session.Vm;
        session.Story.ChooseBrand("Mazda");
        var voice = Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(
            "ACEDrive can be voice controlled. If you want to activate that option, confirm here",
            Strings.Get("VoiceConsentIntro"));
        Assert.DoesNotContain("HEY", Strings.Get("VoiceConsentIntro"), StringComparison.Ordinal);
        Assert.False(typeof(IVoiceScreen).IsAssignableFrom(voice.GetType()));
    }

    [Fact]
    public void VoiceConsentConfirm_ShowsDialogueAndBanksThePermission()
    {
        var store = new FakeOnboardingStore();
        var session = CreateSession(onboarding: store);
        var vm = session.Vm;
        session.Story.ChooseBrand("Mazda");
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen)
            .ConfirmCommand.Execute(null);
        Assert.True(store.State.VoiceConsent);
        Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void VoiceConsentConfirm_BirthdayPageTeachesVoiceAboveContinue()
    {
        // The lesson moved off the consent page to the first birthday page:
        // that is where the mic is live and where the example button (CONTINUE)
        // actually sits. Every later page drops the hint.
        var session = CreateSession();
        var vm = session.Vm;
        session.Story.ChooseBrand("Mazda");
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen)
            .ConfirmCommand.Execute(null);

        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.True(dialogue.HasFirstPageHint);
        Assert.Contains("Control navigation with your voice.", dialogue.FirstPageHint, StringComparison.Ordinal);
        Assert.Contains("Say a button's text", dialogue.FirstPageHint, StringComparison.Ordinal);
        Assert.Contains("CONTINUE", dialogue.FirstPageHint, StringComparison.Ordinal);

        dialogue.ContinueCommand.Execute(null);
        Assert.False(dialogue.HasFirstPageHint);
    }

    [Fact]
    public void VoiceConsentDecline_ContinuesWithThePermissionOff()
    {
        // NO THANK YOU is not a dead end: the walk still continues, just with
        // the mic never opened.
        var store = new FakeOnboardingStore();
        var session = CreateSession(onboarding: store);
        var vm = session.Vm;
        session.Story.ChooseBrand("Mazda");
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen)
            .DeclineCommand.Execute(null);
        Assert.False(store.State.VoiceConsent);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.False(dialogue.HasFirstPageHint);
    }

    [Fact]
    public void DialogueVoiceContinue_GlowsThenAdvancesPages()
    {
        // Every dialogue page acknowledges the voice tap for the shared beat:
        // the teal text is shown before the page turns, not only on the last
        // page.
        var clock = new FakeClock();
        var session = CreateSession(clock: clock);
        var vm = session.Vm;
        session.Story.ChooseBrand("Honda");
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen)
            .ConfirmCommand.Execute(null);
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
                """[{"speaker":"MAX","text":"Easy job.","note":"Open JOBS."}]""");
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
            Assert.Equal("Open JOBS.", loaded.MaxDialogue[0].Note); // page aside, not speech
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
        // 2026-10-25 Datsun session: the model never once returned "datsun"
        // for this car's own brand. It said "hey dawson" x4, "hey nice so" x3
        // and "hey dotson" (which only opened through "dodson", one slip
        // away), so those attempts were silence until "hey nissan" or a lucky
        // partial landed. Both doors must keep answering for this car: its
        // brand as the model spells it, and "nissan" for the same car.
        Assert.Contains("dawson", loaded.Aliases["datsun"]);
        Assert.Contains("nice so", loaded.Aliases["datsun"]);
        Assert.True(VoiceMatch.IsWake("hey dawson", "datsun", loaded.Aliases["datsun"]));
        Assert.True(VoiceMatch.IsWake("hey nice so", "datsun", loaded.Aliases["datsun"]));
        Assert.True(VoiceMatch.IsWake("hey dotson", "datsun", loaded.Aliases["datsun"]));
        Assert.True(VoiceMatch.IsWake("hey datsun", "datsun", loaded.Aliases["datsun"]));
        Assert.True(VoiceMatch.IsWake("hey nissan", "datsun", loaded.Aliases["datsun"]));
        Assert.NotEmpty(loaded.PancakeDialogue); // home leg's dialogue ships with the build
        Assert.NotEmpty(loaded.CommunityCentreDialogue); // post-pancake leg's dialogue ships too
        Assert.NotEmpty(loaded.MaxDialogue); // first post-intro Max visit ships its offer
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void ContentLoader_ShippedData_KeepsTheLoggedBuyDoor()
    {
        // 2026-10-25 09:52 shop visit: five spoken "buy" attempts landed as
        // "my", "boy" x3 and "why", none reached the BUY button, while BACK
        // matched four seconds later; 2026-10-21 failed the same way on
        // "by" x2. Dropping these rows from the table silently brings the
        // miss back in-game, so this fails at the table instead.
        var log = new TestLog();
        OnboardingContent loaded = new JsonOnboardingContent(log).Load();
        Assert.Equal("buy", loaded.Confusions!["boy"]);
        Assert.Equal("buy", loaded.Confusions["by"]);
        Assert.Equal("buy", loaded.Confusions["my"]);
        Assert.Equal("buy", loaded.Confusions["why"]);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void ContentLoader_ShippedData_KeepsTheLoggedBackDoor()
    {
        // 2026-10-25 menu session: spoken "back" attempts landed as "buck" x2
        // and "burke"; the 2026-10-18/23 sessions read "buck"/"bucks" the same
        // way. BACK is a one-word label on every page, so dropping these rows
        // from the table silently brings the miss back in-game.
        var log = new TestLog();
        OnboardingContent loaded = new JsonOnboardingContent(log).Load();
        Assert.Equal("back", loaded.Confusions!["buck"]);
        Assert.Equal("back", loaded.Confusions["bucks"]);
        Assert.Equal("back", loaded.Confusions["burke"]);
        Assert.Equal("BACK", VoiceMatch.MatchLabel("burke", ["BACK", "JOBS"], loaded.Confusions));
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
        string file = TempSaveFile();
        try
        {
            var store = new JsonOnboardingStore(file, new TestLog());
            Assert.Equal(OnboardingState.Fresh, store.Load());
            var state = new OnboardingState(
                "Mazda", "ks_mazda_mx5_na", true,
                MaxStoreId: "max-grocery",
                MaxStoreAddress: new AddressLocation("Street 1", 12, HouseSide.Left),
                CommunityCentreAddress: new AddressLocation("Street 2", 7, HouseSide.Right),
                Stage: OnboardingStage.VoiceInfo);
            store.Save(state);
            Assert.Equal(state, new JsonOnboardingStore(file, new TestLog()).Load());
        }
        finally { DeleteSave(file); }
    }

    [Fact]
    public void OnboardingStore_InvalidFile_FallsBackToFresh()
    {
        string file = TempSaveFile();
        try
        {
            var log = new TestLog();
            File.WriteAllText(file, "{ not json");
            Assert.Equal(OnboardingState.Fresh, new JsonOnboardingStore(file, log).Load());
            // A file that exists but cannot be read is not a first run: the log
            // has to say why the introduction started from the first page.
            Assert.Contains(log.Warnings, w => w.Contains("first page", StringComparison.Ordinal));
        }
        finally { DeleteSave(file); }
    }

    [Fact]
    public void OnboardingStore_CachesTheLoad_AndSaveInvalidates()
    {
        string file = TempSaveFile();
        try
        {
            var store = new JsonOnboardingStore(file, new TestLog());
            Assert.Equal(OnboardingState.Fresh, store.Load());

            // The gate is asked from five places; only the first ask may read.
            File.Delete(file);
            Assert.Equal(OnboardingState.Fresh, store.Load());

            var state = new OnboardingState("Mazda", "ks_mazda_mx5_na", true);
            store.Save(state);
            Assert.Equal(state, store.Load());
        }
        finally { DeleteSave(file); }
    }

    [Fact]
    public void OnboardingStore_KeepsThePreviousSaveAsBackup()
    {
        string file = TempSaveFile();
        try
        {
            var log = new TestLog();
            var store = new JsonOnboardingStore(file, log);
            OnboardingState first = OnboardingState.Fresh with { FavoriteBrand = "Mazda" };
            OnboardingState second = first with { GrandmaCarId = "ks_mazda_mx5_na" };
            store.Save(first);
            store.Save(second);

            Assert.True(File.Exists(file + ".bak"));
            Assert.False(File.Exists(file + ".tmp"));
            Assert.Equal(first, new JsonOnboardingStore(file + ".bak", log).Load());
            Assert.Equal(second, new JsonOnboardingStore(file, log).Load());
        }
        finally { DeleteSave(file); }
    }

    [Fact]
    public void OnboardingStore_CorruptFile_ResumesTheLastGoodSave()
    {
        string file = TempSaveFile();
        try
        {
            var log = new TestLog();
            var store = new JsonOnboardingStore(file, log);
            OnboardingState first = OnboardingState.Fresh with
            {
                FavoriteBrand = "Mazda",
                Stage = OnboardingStage.Brand,
            };
            store.Save(first);
            store.Save(first with { GrandmaCarId = "ks_mazda_mx5_na" });
            // A crash in the middle of a write, or a filter that truncated the
            // file: the career must not rewind to the first page.
            File.WriteAllText(file, "{ truncated");

            Assert.Equal(first, new JsonOnboardingStore(file, log).Load());
            Assert.Contains(log.Warnings, w => w.Contains("last good save", StringComparison.Ordinal));
        }
        finally { DeleteSave(file); }
    }

    [Fact]
    public void OnboardingStore_DeletedFile_ResumesTheLastGoodSave()
    {
        string file = TempSaveFile();
        try
        {
            var log = new TestLog();
            var store = new JsonOnboardingStore(file, log);
            OnboardingState first = OnboardingState.Fresh with { FavoriteBrand = "Mazda" };
            store.Save(first);
            store.Save(first with { GrandmaCarId = "ks_mazda_mx5_na" });
            File.Delete(file);

            Assert.Equal(first, new JsonOnboardingStore(file, log).Load());
        }
        finally { DeleteSave(file); }
    }

    [Fact]
    public void OnboardingStore_BothCopiesUnreadable_StartsFreshAndWarns()
    {
        string file = TempSaveFile();
        try
        {
            var log = new TestLog();
            File.WriteAllText(file, "{ not json");
            File.WriteAllText(file + ".bak", "{ not json either");

            Assert.Equal(OnboardingState.Fresh, new JsonOnboardingStore(file, log).Load());
            Assert.NotEmpty(log.Warnings);
        }
        finally { DeleteSave(file); }
    }

    [Fact]
    public void OnboardingStore_MissingFile_StartsFreshWithoutAWarning()
    {
        string file = TempSaveFile();
        try
        {
            var log = new TestLog();

            Assert.Equal(OnboardingState.Fresh, new JsonOnboardingStore(file, log).Load());
            Assert.Empty(log.Warnings);
        }
        finally { DeleteSave(file); }
    }

    [Fact]
    public void OnboardingStore_LogsThePageItResumesOn()
    {
        string file = TempSaveFile();
        try
        {
            var log = new TestLog();
            new JsonOnboardingStore(file, log).Save(
                OnboardingState.Fresh with { Stage = OnboardingStage.VoiceInfo });

            _ = new JsonOnboardingStore(file, log).Load();

            // The boot log has to answer "did it resume or start over?", so the
            // page is part of the load line.
            Assert.Contains(log.Infos, i => i.Contains("stage=VoiceInfo", StringComparison.Ordinal));
        }
        finally { DeleteSave(file); }
    }

    [Fact]
    public void OnboardingStore_LockedFile_LogsTheFailedWriteAndKeepsTheSessionState()
    {
        string file = TempSaveFile();
        try
        {
            var log = new TestLog();
            var store = new JsonOnboardingStore(file, log);
            store.Save(OnboardingState.Fresh);
            var next = OnboardingState.Fresh with { Stage = OnboardingStage.Brand };
            using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                store.Save(next);
            }

            Assert.Contains(log.Warnings, w => w.Contains("unwritable", StringComparison.Ordinal));
            // The in-memory state is the live one even when the disk refused:
            // the next save retries with the page the player is on.
            Assert.Equal(next, store.Load());
        }
        finally { DeleteSave(file); }
    }

    private static string TempSaveFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

    // The JSON boundary writes .bak/.tmp companions beside every save, so the
    // cleanup removes them or the temp directory fills up between runs.
    private static void DeleteSave(string file)
    {
        File.Delete(file + ".bak");
        File.Delete(file + ".tmp");
        File.Delete(file);
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }
}
