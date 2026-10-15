using System.Globalization;
using AceDrive.Core;
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
         new DialogueLine("YOU", "Thanks!")]);

    // Test-shell career: no hub content, just Max's gift balance.
    private static DeliveryCareer HubCareerShell() =>
        new(HubCatalog.Default, DeliveryCatalog.Default.StartCredP);

    private static MainViewModel Create(OnboardingState? start = null, FakeClock? clock = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var store = new FakeOnboardingStore();
        if (start is not null) store.Save(start);
        clock ??= new FakeClock();
        return new MainViewModel(new FakeTelemetry(), clock, new GameClock(clock, clock.UtcNow), new TaxiShift(),
            new FakeStore(), new NullLogger(), FareTable.Default,
            new TaxiJobProposer(NamePoolTable.Default, seed: 0),
            store, new FakeContentSource(Content),
            new FakeVoiceListener(available: false),
            new FakeCarBrandSource(), TodoTable.Default, DeliveryCatalog.Default, HubCareerShell(),
            new CityEconomy([], CityRules.Default), new FakeCityStore(),
            new RoadTestLedger([], seed: 0), new FakeRoadTestStore(),
            new FakeGameClockStore(), 1, new VoiceBeat(clock));
    }

    [Fact]
    public void FreshBoot_ShowsBrandScreen()
    {
        var vm = Create();
        Assert.True(vm.IsOnboarding);
        Assert.False(vm.JobsVisible);
        var brands = Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(["Honda", "Mazda"], brands.Brands);
    }

    [Fact]
    public void DoneBoot_ReplaysTheIntroThenEndsOnControlPage()
    {
        // Past onboarding, calling the app still opens the introduction: the
        // main control page (PORTPLAN item 54) is reached through the walk,
        // not around it, and the taps re-save the picks on the way.
        var vm = Create(OnboardingState.Fresh with { BirthdayDone = true });
        Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);

        vm.ChooseBrand("Mazda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null); // last page: the walk ends
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void HotkeyCall_ReopensTheIntroFromAnyPage()
    {
        // Ctrl+Shift+C is a call to the app like the wake word: the hidden ->
        // shown edge reopens the introduction instead of resuming the page the
        // player was last on.
        var vm = Create(OnboardingState.Fresh with { BirthdayDone = true });
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        vm.ToggleShowCommand.Execute(null); // hide
        vm.ToggleShowCommand.Execute(null); // call it back
        Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
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
        vm.ChooseBrand("Nope");
        Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void FullFlow_PersistsEveryStep()
    {
        var store = new FakeOnboardingStore();
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var vm = new MainViewModel(new FakeTelemetry(), clock, new GameClock(clock, clock.UtcNow), new TaxiShift(),
            new FakeStore(), new NullLogger(), FareTable.Default,
            new TaxiJobProposer(NamePoolTable.Default, seed: 0),
            store, new FakeContentSource(Content),
            new FakeVoiceListener(available: false),
            new FakeCarBrandSource(), TodoTable.Default, DeliveryCatalog.Default, HubCareerShell(),
            new CityEconomy([], CityRules.Default), new FakeCityStore(),
            new RoadTestLedger([], seed: 0), new FakeRoadTestStore(),
            new FakeGameClockStore(), 1, new VoiceBeat(clock));

        vm.ChooseBrand("Honda");
        Assert.Equal("Honda", store.State.FavoriteBrand);

        vm.ChooseCar("ks_datsun_240z_fairlady");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("ks_datsun_240z_fairlady", store.State.GrandmaCarId);

        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Grandma", dialogue.SpeakerName);

        dialogue.ContinueCommand.Execute(null);
        Assert.Equal("You", dialogue.SpeakerName);

        dialogue.ContinueCommand.Execute(null); // last page finishes
        // The finished flow lands on the main control page; progress is persisted.
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.True(store.State.BirthdayDone);
    }

    [Fact]
    public void BrandPage_MouseOnly_VoiceOff()
    {
        // Companion parity: brand/car pages expose no voice commands at all;
        // clicks drive them. Only the dialogue's Continue responds to speech.
        var vm = Create();
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
        Assert.Contains("HEY {BRAND}", voice.WakeText, StringComparison.Ordinal);
        Assert.Contains("HEY ALPINE", voice.WakeText, StringComparison.Ordinal);
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
    public void DialogueVoiceContinue_AdvancesPages()
    {
        var vm = Create();
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        string? label = VoiceMatch.MatchLabel("could you please continue", dialogue.VoiceActions.Keys);
        Assert.Equal("CONTINUE", label);
        dialogue.VoiceActions[label!].Invoke();
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
            File.WriteAllText(Path.Combine(dir, "voice_confusions.json"),
                """{"Your":"NEW","dear":"tier","two words":"x","y":"y","":"z"}""");
            var log = new TestLog();
            OnboardingContent loaded = new JsonOnboardingContent(dir, log).Load();
            Assert.Equal(["Honda", "Mazda"], loaded.Brands); // sorted
            Assert.Equal(["lancer"], loaded.Aliases["lancia"]);
            Assert.Single(loaded.Cars);
            Assert.Null(loaded.Cars[0].ImagePath); // no Thumbs shipped in temp dir
            Assert.Single(loaded.Dialogue);
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
        Assert.Equal("mazda", loaded.Confusions["him"]); // live 17:30:04 "the him"
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
        Assert.Empty(loaded.Confusions!); // loader always passes a (possibly empty) table
        Assert.Equal(5, log.Warnings.Count);
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
            var state = new OnboardingState("Mazda", "ks_mazda_mx5_na", true);
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
