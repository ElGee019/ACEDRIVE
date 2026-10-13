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

    private static MainViewModel Create(OnboardingState? start = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var store = new FakeOnboardingStore();
        if (start is not null) store.Save(start);
        return new MainViewModel(new FakeTelemetry(), new FakeClock(), new TaxiShift(),
            new FakeStore(), new NullLogger(), FareTable.Default, store, Content,
            new FakeVoiceListener(available: false),
            new FakeCarBrandSource(), TodoTable.Default, DeliveryCatalog.Default);
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
    public void DoneBoot_ShowsControlPage()
    {
        // Past onboarding, home is the main control page (PORTPLAN item 54);
        // its date header replaces the onboarding titles.
        var vm = Create(OnboardingState.Fresh with { BirthdayDone = true });
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
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
        var vm = new MainViewModel(new FakeTelemetry(), new FakeClock(), new TaxiShift(),
            new FakeStore(), new NullLogger(), FareTable.Default, store, Content,
            new FakeVoiceListener(available: false),
            new FakeCarBrandSource(), TodoTable.Default, DeliveryCatalog.Default);

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
    public void VoiceInfoVoiceContinue_ShowsDialogue()
    {
        var vm = Create();
        vm.ChooseBrand("Mazda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        string? label = VoiceMatch.MatchLabel("could you please continue", voice.VoiceActions.Keys);
        Assert.Equal("CONTINUE", label);
        voice.VoiceActions[label!].Invoke();
        Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
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
            var log = new TestLog();
            OnboardingContent loaded = new JsonOnboardingContent(dir, log).Load();
            Assert.Equal(["Honda", "Mazda"], loaded.Brands); // sorted
            Assert.Equal(["lancer"], loaded.Aliases["lancia"]);
            Assert.Single(loaded.Cars);
            Assert.Null(loaded.Cars[0].ImagePath); // no Thumbs shipped in temp dir
            Assert.Single(loaded.Dialogue);
            Assert.Empty(log.Warnings);
        }
        finally { Directory.Delete(dir, true); }
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
        Assert.Equal(4, log.Warnings.Count);
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
