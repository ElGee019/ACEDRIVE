using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Visibility + wake-word + voice buttons, driven by a fake mic.</summary>
public sealed class VoiceTests
{
    private static readonly OnboardingContent Content = new(
        ["Honda", "Mazda"],
        new Dictionary<string, string[]>
        {
            ["lancia"] = ["lancer"],
            ["audi"] = ["odie"],
            ["lotus"] = ["lord of"],
            ["maserati"] = ["majority", "massage therapy", "mother", "missouri", "marseilles variety"],
            ["mazda"] = ["muster", "muslim", "pasta", "many", "me some"],
            ["datsun"] = ["nissan", "dodson", "nathan", "henderson"],
        },
        [new GrandmaCar("ks_mazda_mx5_na", "Mazda MX-5 NA", 1990, 12000, null)],
        [new DialogueLine("GRANDMA", "Happy birthday!"),
         new DialogueLine("YOU", "Thanks!")],
        new Dictionary<string, string>
        {
            ["your"] = "new",
            ["dear"] = "tier",
            ["taxes"] = "taxi",
            ["except"] = "accept",
            ["hair"] = "hey",
            ["halo"] = "hey",
            ["a"] = "hey",
            ["have"] = "hey",
            ["him"] = "mazda",
        });

    // Test-shell career: no hub content, just Max's gift balance.
    private static DeliveryCareer HubCareerShell() =>
        new(HubCatalog.Default, DeliveryCatalog.Default.StartCredP);

    private static (MainViewModel Vm, FakeClock Clock, FakeVoiceListener Voice, FakeCarBrandSource Car)
        Create(bool voice = true, ILog? log = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var mic = new FakeVoiceListener(voice);
        var car = new FakeCarBrandSource();
        var vm = new MainViewModel(new FakeTelemetry(), clock, new GameClock(clock, clock.UtcNow), new TaxiShift(),
            new FakeStore(), log ?? new NullLogger(), FareTable.Default,
            new TaxiJobProposer(NamePoolTable.Default, seed: 0),
            new FakeOnboardingStore(), new FakeContentSource(Content), mic, car, TodoTable.Default,
            DeliveryCatalog.Default, HubCareerShell(),
            new CityEconomy([], CityRules.Default), new FakeCityStore(),
            new RoadTestLedger([], seed: 0),
            new FakeRoadTestStore(), new FakeGameClockStore(), 1, new VoiceBeat(clock), new DriverWallet(), new FakeDriverStore(), new FakeFriendshipStore(), []);
        return (vm, clock, mic, car);
    }

    // Brand page -> grandma's car -> voice info -> birthday dialogue, the
    // spoken route onto the main control page (shared by the tests below).
    private static MainControlScreenViewModel ToControlPage(MainViewModel vm)
    {
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null); // page 2
        dialogue.ContinueCommand.Execute(null); // finishes: control page shows
        return Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void WithVoice_StartsHidden()
    {
        var (vm, _, _, _) = Create();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void WithoutVoice_StartsVisible()
    {
        var (vm, _, _, _) = Create(voice: false);
        Assert.True(vm.IsShown);
    }

    [Fact]
    public void Start_BeginsListening()
    {
        var (vm, _, mic, _) = Create();
        vm.Start();
        Assert.True(mic.Started);
        vm.Dispose();
    }

    [Fact]
    public void ToggleShow_Flips()
    {
        var (vm, _, _, _) = Create();
        vm.ToggleShowCommand.Execute(null);
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void ToggleShow_ReschedulesIdleWindow()
    {
        // Regression: showing via hotkey must reset the idle timer (Show()
        // records activity), else the window vanishes on the next poll.
        var (vm, clock, _, _) = Create();
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(14.9));
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(0.2));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void WakeWord_ShowsOverlay()
    {
        var (vm, _, mic, _) = Create();
        mic.Fire("hey mazda");
        Assert.True(vm.IsShown);
        Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void BrandAlone_DoesNotWake()
    {
        var (vm, _, mic, _) = Create();
        mic.Fire("mazda");
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void Wake_CooldownBlocksImmediateRewake()
    {
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null); // hide at the same instant
        Assert.False(vm.IsShown);
        mic.Fire("hey mazda"); // still inside the 3 s cooldown
        Assert.False(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(4));
        vm.Poll();
        mic.Fire("hey mazda"); // cooldown over -> shows again
        Assert.True(vm.IsShown);
    }

    [Fact]
    public void Idle_HidesAfterFifteenSeconds()
    {
        var (vm, clock, _, _) = Create();
        vm.Show();
        clock.Advance(TimeSpan.FromSeconds(14.9));
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(0.2));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void Activity_ResetsIdleTimer()
    {
        var (vm, clock, _, _) = Create();
        vm.Show();
        clock.Advance(TimeSpan.FromSeconds(14));
        vm.RecordActivity();
        clock.Advance(TimeSpan.FromSeconds(14));
        vm.Poll();
        Assert.True(vm.IsShown);
    }

    [Fact]
    public void Resize_SuspendsIdleHide()
    {
        // Holding a resize drag (even motionless) is activity: the 15 s
        // auto-hide must not fire mid-drag, and the window restarts on drop.
        var (vm, clock, _, _) = Create();
        vm.Show();
        vm.SetResizing(true);
        clock.Advance(TimeSpan.FromSeconds(20));
        vm.Poll();
        Assert.True(vm.IsShown);
        vm.SetResizing(false);
        clock.Advance(TimeSpan.FromSeconds(14.9));
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(0.2));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void VoiceButton_ResetsIdleTimer()
    {
        // Voice control is user input: a fired command restarts the 15 s
        // auto-hide window, exactly like a click.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda"); // wake, activity at t=0
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null); // page 2
        dialogue.ContinueCommand.Execute(null); // finishes
        Assert.True(dialogue.IsComplete);

        clock.Advance(TimeSpan.FromSeconds(14));
        mic.Fire("start taxi shift"); // control-page voice button fires -> activity at t=14
        clock.Advance(TimeSpan.FromSeconds(14)); // 14 s since voice, < 15 s
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(2)); // 16 s since last input
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void MutedRepeat_StillResetsIdleTimer()
    {
        // Regression: a repeat of the same utterance stays muted (no double
        // fire) but still counts as presence: the 15 s auto-hide restarts, so
        // repeating a button command never hides the window mid-use. The day
        // event label is used because it never changes: the taxi button's
        // START/CONTINUE flip would re-target the next utterance anyway.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null); // finishes: control page shows
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        clock.Advance(TimeSpan.FromSeconds(8));
        mic.Fire("market valuation"); // fires: activity at t=8
        CalendarRow monday = Assert.Single(control.Days, d => d.DayLabel == "Monday");
        Assert.True(monday.Flash);
        clock.Advance(TimeSpan.FromSeconds(8));
        mic.Fire("market valuation"); // muted repeat: no fire, still activity
        clock.Advance(TimeSpan.FromSeconds(8)); // 8 s since the repeat, < 15 s
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(8)); // 16 s since last utterance
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void UnmatchedFinal_RestartsIdleWindow()
    {
        // A failed voice attempt is still the user being present: the logged
        // 2026-10-13 session hid mid-retry and the next phrase woke to home
        // instead of pressing the button. Unmatched finals restart the 15 s
        // window; the window then hides normally after silence.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        ToControlPage(vm);
        clock.Advance(TimeSpan.FromSeconds(8));
        mic.Fire("purple octopus", isFinal: true); // heard, matches nothing
        clock.Advance(TimeSpan.FromSeconds(8)); // 8 s since the attempt, < 15 s
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(8)); // 16 s since last utterance
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void UnmatchedPartial_DoesNotRestartIdleWindow()
    {
        // Only final transcripts count as presence, so background chatter
        // that never finalizes cannot keep the overlay up forever.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        ToControlPage(vm);
        clock.Advance(TimeSpan.FromSeconds(14));
        mic.Fire("purple octop"); // partial: in-flight hearing, no presence stamp
        clock.Advance(TimeSpan.FromSeconds(1.1)); // 15.1 s since wake
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void ConfusionTable_OpensHubFromLoggedGarble()
    {
        // The literal 2026-10-13 transcript: "open new career" reached the
        // app as "open your career"; the data table must still press the
        // OPEN NEW CAREER button on the delivery job page. Both taps wait
        // out the half-second acknowledgment beat before the page flips.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        ToControlPage(vm);
        mic.Fire("delivery job");
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen); // glow not yet drained
        DrainBeat(vm, clock);
        Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        mic.Fire("open your career");
        Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        DrainBeat(vm, clock);
        Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void ConfusionTable_StartsTaxiFromLoggedGarble()
    {
        // Literal 2026-10-14 transcripts: "start taxi shift" repeatedly
        // reached the app as "start taxes shift" and went unanswered ("voice
        // heard" with no "voice button"). The table's swap must resolve the
        // mishearing and still tap the taxi entry.
        var (vm, _, mic, _) = Create();
        mic.Fire("hey mazda");
        var control = ToControlPage(vm);
        mic.Fire("start taxes shift");
        Assert.True(control.TaxiFlash);
    }

    [Fact]
    public void ConfusionTable_AcceptsFromLoggedGarble()
    {
        // Literal 2026-10-14 18:47-18:48 transcripts: on the proposal page
        // "accept" reached the app as "except" eleven times in a row and
        // went unanswered ("voice heard" with no "voice button"); only the
        // one clean "accept" pressed the button. The table's swap must
        // resolve the mishearing and tap ACCEPT on the first retry.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        ToControlPage(vm);
        mic.Fire("continue");
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);

        mic.Fire("except");
        Assert.True(proposal.AcceptFlash);
        DrainBeat(vm, clock);
        Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void ConfusionTable_WakesFromLoggedInterjectionGarble()
    {
        // Literal 2026-10-14 wake miss: "hey lamborghini" heard as "hair
        // lamborghini", three steps past the interjection slack. The table's
        // second voice opens the overlay instead of silence.
        var (vm, _, mic, _) = Create();
        Assert.False(vm.IsShown);
        mic.Fire("hair mazda");
        Assert.True(vm.IsShown);
    }

    [Fact]
    public void ConfusionTable_WakesLotusFromLoggedGarbles()
    {
        // Literal 2026-10-14 Lotus session: "hey lotus" reached the app as
        // "the a lotus" and "the have lord of the have lotus" and stayed a
        // voice-heard line without ever waking. With the shipped doors the
        // interjection resolves to "hey" and the garbled brand answers to
        // its alias, so both retries open the overlay. The 16:04:16 and
        // 15:55:33 retries wake on the later doors: a clipped "hey" behind
        // the exact brand, and "halo" read for "hey".
        var (vm, clock, mic, car) = Create();
        car.Primed = "lotus"; // newest ACE log says a Lotus
        vm.Start();
        Assert.False(vm.IsShown);
        mic.Fire("the a lotus");
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null); // hide and retry the next garble
        clock.Advance(TimeSpan.FromSeconds(4)); // wake cooldown elapsed
        mic.Fire("the have lord of the have lotus");
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(4));
        mic.Fire("all said you're a massive the lotus"); // logged 16:04:16
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(4));
        mic.Fire("halo lotus"); // logged 15:55:33 retry class
        Assert.True(vm.IsShown);
        vm.Dispose();
    }

    [Fact]
    public void Aliases_WakeMaseratiFromLoggedGarbles()
    {
        // Literal 2026-10-14 Maserati session 16:34:33-16:34:49: five
        // "hey maserati" retries logged as "hey majority" (x2), "hey
        // massage therapy", "hey mother" and "hey missouri the" stayed
        // voice-heard lines; only the one exact hearing woke. The alias
        // table gives every logged retry the same answer.
        var (vm, clock, mic, car) = Create();
        car.Primed = "maserati"; // newest ACE log says a Maserati
        vm.Start();
        Assert.False(vm.IsShown);
        mic.Fire("hey majority");
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null); // hide and retry the next garble
        clock.Advance(TimeSpan.FromSeconds(4)); // wake cooldown elapsed
        mic.Fire("hey massage therapy");
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(4));
        mic.Fire("hey mother");
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(4));
        mic.Fire("hey missouri the");
        Assert.True(vm.IsShown);
        vm.Dispose();
    }

    [Fact]
    public void Aliases_WakeMazdaFromLoggedGarbles()
    {
        // Literal 2026-10-14 Mazda sessions: "hey mazda" reached the app
        // as "the pasta pasta", "the hey muster" and "... mister hey
        // muslim" (16:48-16:51) and as "the pay many"/"hey many" (17:13
        // live test) and stayed voice-heard lines; only the literal
        // "hey mazda" retries woke. The alias table gives every logged
        // garble the same answer as the clean hearing.
        var (vm, clock, mic, _) = Create();
        Assert.False(vm.IsShown);
        mic.Fire("the hey muster"); // logged 16:50:05
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null); // hide and retry the next garble
        clock.Advance(TimeSpan.FromSeconds(4)); // wake cooldown elapsed
        mic.Fire("the continual duck ship mister hey muslim"); // logged 16:51:03
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(4));
        mic.Fire("hey many"); // logged live 17:13:20
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(4));
        mic.Fire("the pay me some the have me some"); // logged live 17:17:34
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(4));
        mic.Fire("the him", isFinal: true); // logged live 17:30:04 — "the" + garble past alias reach
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(4));
        mic.Fire("pasta"); // partial alone: the utterance is still growing
        Assert.False(vm.IsShown);
        mic.Fire("pasta", isFinal: true); // logged live 17:30:07 — interjection died whole
        Assert.True(vm.IsShown);
        vm.Dispose();
    }

    [Fact]
    public void Aliases_WakeDatsunFromLoggedGarbles()
    {
        // Live 2026-10-14 Datsun session after the 17:13:42 brand switch:
        // "hey datsun" arrived as "hey nissan" (x4) and "... have dodson
        // ..." (17:14:08) and never woke. Same English-vocabulary swallow
        // as the Lotus and Maserati garbles, same alias-table cure.
        var (vm, clock, mic, car) = Create();
        car.Primed = "datsun"; // player switched favorite to Datsun
        vm.Start();
        Assert.False(vm.IsShown);
        mic.Fire("hey nissan"); // logged 17:13:55
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null); // hide and retry the next garble
        clock.Advance(TimeSpan.FromSeconds(4)); // wake cooldown elapsed
        mic.Fire("a nissan have dodson hey nissan"); // logged 17:14:08
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(4));
        mic.Fire("hey nathan"); // logged live 17:17:38
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(4));
        mic.Fire("henderson", isFinal: true); // logged live 17:17:36 — one fused word, interjection dead
        Assert.True(vm.IsShown);
        vm.Dispose();
    }

    // What the 10 Hz DispatcherTimer does in the app: wall time passes, the
    // poll drains the voice beat, pending navigations land.
    private static void DrainBeat(MainViewModel vm, FakeClock clock)
    {
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        vm.Poll();
    }

    [Fact]
    public void OpenDialogue_SuspendsIdleHide()
    {
        var (vm, clock, _, _) = Create();
        vm.Show();
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        clock.Advance(TimeSpan.FromSeconds(20));
        vm.Poll();
        Assert.True(vm.IsShown);
    }

    [Fact]
    public void FinishedDialogue_ResumesIdleHide()
    {
        var (vm, clock, _, _) = Create();
        vm.Show();
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null); // page 2
        dialogue.ContinueCommand.Execute(null); // finishes
        Assert.True(dialogue.IsComplete);
        clock.Advance(TimeSpan.FromSeconds(16));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void Idle_HidesEvenWithoutTelemetry()
    {
        // Regression: the idle check must run before Poll's telemetry
        // early-return, so auto-hide works outside a live EVO session.
        var clock = new FakeClock();
        var tel = new FakeTelemetry { PhysicsPresent = false };
        var vm = new MainViewModel(tel, clock, new GameClock(clock, clock.UtcNow), new TaxiShift(),
            new FakeStore(), new NullLogger(), FareTable.Default,
            new TaxiJobProposer(NamePoolTable.Default, seed: 0),
            new FakeOnboardingStore(), new FakeContentSource(Content), new FakeVoiceListener(),
            new FakeCarBrandSource(), TodoTable.Default, DeliveryCatalog.Default, HubCareerShell(),
            new CityEconomy([], CityRules.Default), new FakeCityStore(),
            new RoadTestLedger([], seed: 0), new FakeRoadTestStore(),
            new FakeGameClockStore(), 1, new VoiceBeat(clock), new DriverWallet(), new FakeDriverStore(), new FakeFriendshipStore(), []);
        vm.Show();
        clock.Advance(TimeSpan.FromSeconds(15.1));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void VoiceButton_FiresOncePerUtterance()
    {
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);

        mic.Fire("could you please continue");
        Assert.Equal("Thanks!", dialogue.Body);
        mic.Fire("continue"); // same utterance: muted
        mic.Fire("continue");
        Assert.Equal("Thanks!", dialogue.Body);
        mic.Fire("continue", isFinal: true); // endpoint re-arms, fires nothing
        Assert.Equal("Thanks!", dialogue.Body);
        mic.Fire("continue"); // next utterance fires again -> finishes flow
        Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen); // waits out the beat
        DrainBeat(vm, clock);
        // The finished flow lands on the main control page, no empty screen.
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void WakeWord_FollowsCarBrand_NotOtherBrands()
    {
        var (vm, _, mic, car) = Create();
        car.Fire("porsche"); // player switched to a Porsche
        mic.Fire("hey mazda");
        Assert.False(vm.IsShown);
        mic.Fire("hey porsche");
        Assert.True(vm.IsShown);
    }

    [Fact]
    public void WakeWord_IgnoresFavoriteBrand()
    {
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda"); // default brand (no watcher prime in tests)
        Assert.True(vm.IsShown);
        vm.ChooseBrand("Honda"); // saved favorite must not drive the wake word
        vm.ToggleShowCommand.Execute(null); // hide
        clock.Advance(TimeSpan.FromSeconds(4)); // cooldown elapsed
        mic.Fire("hey honda");
        Assert.False(vm.IsShown);
        mic.Fire("hey mazda");
        Assert.True(vm.IsShown);
    }

    [Fact]
    public void Start_PrimedCarBrandDrivesWake_WithAliases()
    {
        var (vm, _, mic, car) = Create();
        car.Primed = "lancia"; // newest ACE log says a Lancia
        vm.Start();
        mic.Fire("hey lancer"); // documented mishearing, alias from content
        Assert.True(vm.IsShown);
        vm.Dispose();
    }

    [Fact]
    public void WakeUtteranceFinal_DoesNotPickBrand()
    {
        // Regression: the wake phrase's FINAL result ("hey mazda") used to
        // arrive after the brand page appeared and fired the Mazda button.
        // Brand/car pages are mouse-only now, so it must be ignored.
        var (vm, _, mic, _) = Create();
        mic.Fire("hey mazda"); // partial: wakes, shows brand page
        Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
        mic.Fire("hey mazda", isFinal: true); // same utterance, must not select
        Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void WakeAgain_ReplaysTheIntro()
    {
        // Calling the app with the wake word walks the introduction again:
        // idle-hide parks the player on the control page, the next wake
        // reopens the brand page, and the chain leads back down to the
        // control page.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null); // finishes: control page shows
        clock.Advance(TimeSpan.FromSeconds(16));
        vm.Poll(); // idle hides
        Assert.False(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(4)); // wake cooldown over
        mic.Fire("hey mazda");
        Assert.True(vm.IsShown);
        Assert.IsType<BrandScreenViewModel>(vm.CurrentScreen);

        ToControlPage(vm); // the walk answers again
    }

    [Fact]
    public void VoiceStartTaxi_ContinuesOnlyAfterFlash()
    {
        // The half-second acknowledgment glows on "START TAXI SHIFT"; the
        // button earns the CONTINUE name only when the glow is gone (the
        // shift itself starts immediately, behind the held-back label).
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        var control = ToControlPage(vm);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);

        mic.Fire("start taxi shift");
        Assert.True(control.TaxiFlash);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);

        clock.Advance(TimeSpan.FromSeconds(0.51));
        vm.Poll();
        Assert.False(control.TaxiFlash);
        Assert.Equal("CONTINUE TAXI SHIFT", control.TaxiLabel);
    }

    [Fact]
    public void Voice_ContinueTaxiShift_OpensShiftWhileStartShown()
    {
        // Either shift name answers, whichever one the button displays.
        var (vm, _, mic, _) = Create();
        mic.Fire("hey mazda");
        var control = ToControlPage(vm);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);

        mic.Fire("continue taxi shift");
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        Assert.True(control.TaxiFlash);

        proposal.AcceptCommand.Execute(null);
        Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void Voice_GarbledEventNames_AnswerToKeywords()
    {
        // "chant amelia" / "where is willie" are literal transcripts the
        // English model logged for "Cento Miglia" / "Where is Willy". Each
        // answers on its own game morning: only the highlighted row is
        // triggerable, by click or by voice.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        var control = ToControlPage(vm);

        clock.Advance(TimeSpan.FromMinutes(106)); // ~2 game days: Wednesday
        vm.Poll();
        vm.Show(); // a call replays the intro...
        ToControlPage(vm); // ...so the walk answers the next utterance
        mic.Fire("where is willie");
        // Willy is DayOfWeek 4: a Wednesday.
        CalendarRow willyDay = Assert.Single(control.Days, d => d.DayLabel == "Wednesday");
        Assert.True(willyDay.Flash);

        clock.Advance(TimeSpan.FromMinutes(212)); // ~4 more game days: Sunday
        vm.Poll();
        vm.Show();
        ToControlPage(vm);
        mic.Fire("chant amelia");
        CalendarRow sunday = Assert.Single(control.Days, d => d.DayLabel == "Sunday");
        Assert.True(sunday.Flash);
    }

    [Fact]
    public void Voice_ContinueOneWord_TapsTaxiButton()
    {
        var (vm, _, mic, _) = Create();
        mic.Fire("hey mazda");
        ToControlPage(vm);

        mic.Fire("continue"); // the shortcut, not the full label
        Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void DialogueContinue_FinalLandsAfterNavigation_DoesNotFireTaxi()
    {
        // Regression for the logged 2026-10-14 surprise: "continue" closing
        // the birthday dialogue turned START TAXI SHIFT into CONTINUE without
        // the player ever asking. The page change waits out the beat, so the
        // utterance's FINAL fragment arrives while the control page is
        // already up — and its dictionary owns a bare "continue" taxi
        // keyword under a different label and delegate than the dialogue's.
        // The text mute must recognise the same utterance across screens.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null); // page 2 (last line)

        mic.Fire("continue"); // partial: finishes the dialogue
        clock.Advance(TimeSpan.FromSeconds(0.51));
        vm.Poll(); // beat drains: the control page is up
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        mic.Fire("continue", isFinal: true); // same utterance's tail: muted
        Assert.False(control.TaxiFlash);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        // The mute cleared with the final, so the NEXT "continue" is a new
        // utterance and still taps the taxi entry (the keyword stands).
        mic.Fire("continue");
        Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void DialogueContinue_RescoredTail_OnNewPage_IsStillTheSameTap()
    {
        // Literal 2026-10-14 18:37:33/34 transcripts from the repro session:
        // the closing dialogue line answered to the partial "we continue",
        // and the page change waited out the beat. The utterance's final
        // landed as "the continue" - Vosk re-scored the leading word - while
        // the control page was already up, where bare "continue" is a taxi
        // keyword. Exact-text mute missed the tail and the shift started
        // unasked. The fired label's word run survives the re-score, so the
        // tail must stay muted.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null); // page 2 (last line)

        mic.Fire("we continue"); // partial: taps CONTINUE, waits out the beat
        clock.Advance(TimeSpan.FromSeconds(0.51));
        vm.Poll();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        mic.Fire("the continue", isFinal: true); // same utterance, re-scored
        Assert.False(control.TaxiFlash);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        // Cleared with the final: a real next utterance still taps through.
        mic.Fire("the back"); // no "back" key on the control page: inert
        mic.Fire("continue");
        Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void Voice_ContinueThenFullLabel_FiresOnce()
    {
        // Partial "continue" and final "continue taxi shift" are one
        // utterance's fragments: the partial taps the taxi entry (and opens
        // the proposal page), the final must not add a second tap — on the
        // proposal page it answers to nothing, and the same text would be
        // muted anyway. One "voice button:" log, not two.
        var log = new TestLog();
        var (vm, _, mic, _) = Create(log: log);
        mic.Fire("hey mazda");
        ToControlPage(vm);

        mic.Fire("continue"); // partial: shortcut fires
        mic.Fire("continue taxi shift", isFinal: true); // final: same utterance

        Assert.Equal(1, log.Infos.Count(m => m.StartsWith("voice button:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Voice_AliasPartialFullLabelFinal_FiresOnce()
    {
        // One utterance can hit the same row under two keys: the partial
        // hears the "where is" alias, the final the full "Where is Willy"
        // label. The shared action delegate keeps it a single tap. Wednesday
        // morning: only the highlighted row answers.
        var log = new TestLog();
        var (vm, clock, mic, _) = Create(log: log);
        mic.Fire("hey mazda");
        ToControlPage(vm);

        clock.Advance(TimeSpan.FromMinutes(106)); // ~2 game days: Wednesday
        vm.Poll();
        vm.Show(); // a call replays the intro, so walk back to the control page
        ToControlPage(vm);
        mic.Fire("where is"); // partial: alias fires
        mic.Fire("where is willy", isFinal: true); // final: same tap, muted

        Assert.Equal(1, log.Infos.Count(m => m.StartsWith("voice button:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Voice_OpenNew_ShortFormFiresCareerButton()
    {
        // The delivery button prints "OPEN NEW CAREER"; its two leading
        // words answer too, anywhere in the sentence. Firing it lands on
        // the hub page (the real OPEN NEW CAREER screen, PORTPLAN 18) once
        // the half-second acknowledgment beat drains.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        ToControlPage(vm);
        mic.Fire("delivery job");
        DrainBeat(vm, clock);
        Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);

        mic.Fire("okay open new then");
        DrainBeat(vm, clock);
        Assert.IsType<DeliveryHubScreenViewModel>(vm.CurrentScreen);
        vm.Dispose();
    }

    [Fact]
    public void Voice_GarbledWakeInterjection_StillWakes()
    {
        // "pay alpine"-style transcripts: the model garbles the interjection
        // for non-native speakers, not (only) the brand word.
        var (vm, _, mic, car) = Create();
        car.Primed = "alpine";
        vm.Start();
        Assert.False(vm.IsShown);

        mic.Fire("pay alpine");
        Assert.True(vm.IsShown);
        vm.Dispose();
    }

    [Fact]
    public void Voice_AliasBeyondSlack_WakesFromData()
    {
        // "hey odie": three Damerau steps from "audi", so the core
        // tolerance stops short and the alias table carries it.
        var (vm, _, mic, car) = Create();
        car.Primed = "audi";
        vm.Start();
        Assert.False(vm.IsShown);

        mic.Fire("hey odie");
        Assert.True(vm.IsShown);
        vm.Dispose();
    }
}
