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
        [],
        [],
        [],
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

    private static VoiceSession Create(
            bool voice = true, ILog? log = null, OnboardingState? progress = null,
            FakeTelemetry? telemetry = null)
    {
        var clock = new FakeClock();
        var mic = new FakeVoiceListener(voice);
        var car = new FakeCarBrandSource();
        // The voice suite's session: an empty career (no cars, no town) with
        // its own mic and car watcher, driven through the shared builder. The
        // walk itself is the story flow's, so the suite keeps the session.
        TestSessionResult session = MainViewModelTests.Create(
            clock: clock, voice: mic, carBrands: car, physics: telemetry,
            log: log, progress: progress, content: Content,
            cars: [], garage: new Garage([]),
            city: new CityEconomy([], CityRules.Default),
            roads: new RoadTestLedger([], seed: 0),
            shiftStandings: new OfflineShiftStandings(),
            rarity: new OfflineCarRarity(),
            telemetry: new OfflinePlayerTelemetry());
        return new VoiceSession(session, clock, mic, car);
    }

    // A player who finished the whole introduction: the walk and the guided
    // tour are banked, so the idle countdown runs on every page again.
    private static readonly OnboardingState Introduced = OnboardingState.Fresh with
    {
        BirthdayDone = true,
        MaxErrandDone = true,
        JustinPickedUp = true,
        AnnabellaPickedUp = true,
        PancakesDone = true,
        CommunityCentreDone = true,
    };

    private static VoiceSession CreateIntroduced(bool voice = true, ILog? log = null) =>
        Create(voice, log, Introduced);

    // A post-introduction control page: the call opens the menu, Jobs leads in.
    private static MainControlScreenViewModel ShowControl(MainViewModel vm)
    {
        vm.Show();
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen).JobsCommand.Execute(null);
        return Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    // Brand page -> voice info -> birthday dialogue -> grandma's car, the
    // spoken route onto the main page (shared by the tests below). The car
    // page answers speech too, but this helper takes the click path and lands
    // on the menu; the Jobs tile takes it onto the control page whose voice
    // buttons these tests exercise.
    private static MainControlScreenViewModel ToControlPage(VoiceSession session)
    {
        OnboardingFlow story = session.Story;
        MainViewModel vm = session.Vm;
        story.ChooseBrand("Honda");
        var voice = Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
        voice.ConfirmCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null); // page 2
        dialogue.ContinueCommand.Execute(null); // finishes: grandma's car page
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        vm.Show(); // the hand-over hid the panel; this keeps the built menu
        Assert.Same(menu, vm.CurrentScreen);
        menu.JobsCommand.Execute(null);
        return Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    // A call lands on the menu page; its Calendar tile opens the cached
    // calendar page, the same tap the player makes.
    private static CalendarScreenViewModel ShowCalendar(MainViewModel vm)
    {
        vm.Show();
        Assert.True(vm.IsShown);
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        menu.CalendarCommand.Execute(null);
        return Assert.IsType<CalendarScreenViewModel>(vm.CurrentScreen);
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
    public void Start_WithoutConsent_KeepsTheMicOff()
    {
        // The mic must not open before the player answered the voice page.
        var (vm, _, mic, _) = Create();
        vm.Start();
        Assert.False(mic.Started);
        vm.Dispose();
    }

    [Fact]
    public void Start_WithConsent_StartsListening()
    {
        var (vm, _, mic, _) = Create(
            progress: OnboardingState.Fresh with { VoiceConsent = true });
        vm.Start();
        Assert.True(mic.Started);
        vm.Dispose();
    }

    [Fact]
    public void Start_LegacyFinishedSave_StartsListening()
    {
        // A save written before the consent page existed never answered; it
        // keeps voice on so the update never silently mutes a profile.
        var (vm, _, mic, _) = Create(progress: Introduced);
        vm.Start();
        Assert.True(mic.Started);
        vm.Dispose();
    }

    [Fact]
    public void VoiceConsent_ConfirmStartsTheMic()
    {
        var session = Create();
        var (vm, _, mic, _) = session;
        session.Story.ChooseBrand("Honda");
        Assert.False(mic.Started);
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen)
            .ConfirmCommand.Execute(null);
        Assert.True(mic.Started);
        Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void VoiceConsent_DeclineKeepsTheMicOff()
    {
        var session = Create();
        var (vm, _, mic, _) = session;
        session.Story.ChooseBrand("Honda");
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen)
            .DeclineCommand.Execute(null);
        Assert.False(mic.Started);
        Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
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
        var (vm, clock, _, _) = CreateIntroduced();
        vm.ToggleShowCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(14.9));
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(0.2));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void WakeWord_ShowsFirstRunPage()
    {
        var (vm, _, mic, _) = Create();
        mic.Fire("hey mazda");
        Assert.True(vm.IsShown);
        Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);
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
        var (vm, clock, _, _) = CreateIntroduced();
        vm.Show();
        clock.Advance(TimeSpan.FromSeconds(14.9));
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(0.2));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void IntroWalk_IgnoresTheIdleCountdown()
    {
        // Hiding a walk page would skip the introduction (the next call opens
        // the menu, not that page), so the countdown never starts before the
        // birthday hand-over.
        var (vm, clock, _, _) = Create(voice: false);
        Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);

        clock.Advance(TimeSpan.FromMinutes(2));
        vm.Poll();

        Assert.True(vm.IsShown);
        Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void IntroTour_IgnoresTheIdleCountdown()
    {
        // The guided tour is still the introduction: a panel the player calls
        // up mid-tour waits for them instead of counting itself away.
        var (vm, clock, _, _) = Create(
            progress: OnboardingState.Fresh with { BirthdayDone = true });
        vm.Show();
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);

        clock.Advance(TimeSpan.FromSeconds(30));
        vm.Poll();

        Assert.True(vm.IsShown);
        Assert.Same(menu, vm.CurrentScreen);
    }

    [Fact]
    public void Activity_ResetsIdleTimer()
    {
        var (vm, clock, _, _) = CreateIntroduced();
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
        var (vm, clock, _, _) = CreateIntroduced();
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
        var (vm, clock, mic, _) = CreateIntroduced();
        ShowControl(vm); // a call lands on the menu, Jobs on the career page

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
        // repeating a button command never hides the window mid-use. The road
        // test is used because its row answers to the short label only (the car
        // suffix never reaches the mic), and an off-day tap warns instead of
        // navigating, so the calendar stays the shown page.
        var (vm, clock, mic, _) = CreateIntroduced();
        mic.Fire("hey mazda");
        var calendar = ShowCalendar(vm);

        clock.Advance(TimeSpan.FromSeconds(8));
        mic.Fire("road test"); // fires: activity at t=8
        CalendarRow tuesday = Assert.Single(calendar.Days, d => d.DayLabel == "Tuesday");
        Assert.True(tuesday.Flash);
        clock.Advance(TimeSpan.FromSeconds(8));
        mic.Fire("road test"); // muted repeat: no fire, still activity
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
        var (vm, clock, mic, _) = CreateIntroduced();
        mic.Fire("hey mazda");
        ShowControl(vm);
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
        var (vm, clock, mic, _) = CreateIntroduced();
        mic.Fire("hey mazda");
        ShowControl(vm);
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
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda");
        ToControlPage(session);
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
        var session = Create();
        var mic = session.Voice;
        mic.Fire("hey mazda");
        var control = ToControlPage(session);
        mic.Fire("start taxes shift");
        Assert.True(control.TaxiFlash);
    }

    // The 2026-10-18/22 sessions: "start" reached the model as "stuart" or
    // "starts", so the clean "start taxi" keyword (and the label slice) never
    // lined up. Each literal transcript has to tap the shift.
    [Theory]
    [InlineData("stuart ducks shift")]                 // 2026-10-22 15:22:59
    [InlineData("stuart taxes shift")]                 // 15:23:03
    [InlineData("stuart's taxes shift")]               // 15:48:12
    [InlineData("stuart nazi shift starts to shift")]  // 15:48:16
    [InlineData("stuart taxi shift")]                  // 15:48:19
    [InlineData("stuart ducks a shift")]               // 19:34:32
    [InlineData("starts taxes shift")]                 // 2026-10-18 22:09:39
    [InlineData("start decks shift")]                  // 2026-10-18 20:01:06
    public void Voice_LoggedStartTaxiGarbles_TapTheShift(string transcript)
    {
        var session = Create();
        var mic = session.Voice;
        mic.Fire("hey mazda");
        var control = ToControlPage(session);

        mic.Fire(transcript);

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
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda");
        ToControlPage(session);
        mic.Fire("continue");
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);

        mic.Fire("except");
        Assert.True(proposal.AcceptFlash);
        DrainBeat(vm, clock);
        Assert.True(vm.PickupCommand.CanExecute(null));
        Assert.Contains("To pickup", vm.JobLine, StringComparison.Ordinal);
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
        var session = Create();
        var (vm, clock, _, _) = session;
        vm.Show();
        session.Story.ChooseBrand("Honda");
        var voice = Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
        voice.ConfirmCommand.Execute(null);
        Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        clock.Advance(TimeSpan.FromSeconds(20));
        vm.Poll();
        Assert.True(vm.IsShown);
    }

    [Fact]
    public void FinishedDialogue_LeavesThePanelClosedForTheTour()
    {
        // The hand-over hides the panel and leaves the tour on the HUD; no
        // later poll may pop it back open.
        var session = Create();
        var (vm, clock, _, _) = session;
        vm.Show();
        session.Story.ChooseBrand("Honda");
        var voice = Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
        voice.ConfirmCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null); // page 2
        dialogue.ContinueCommand.Execute(null); // finishes: grandma's car page
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);
        Assert.True(dialogue.IsComplete);
        Assert.False(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(16));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void GrandmaCarContinue_VoiceFlashesThenEndsTheWalk()
    {
        // The car page sits after the consent answer, so its CONTINUE answers
        // to speech like the dialogue's did: teal acknowledgment, then the
        // hand-over at the end of the shared beat.
        var session = Create();
        var (vm, clock, mic, _) = session;
        vm.Show();
        session.Story.ChooseBrand("Honda");
        Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen)
            .ConfirmCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null); // page 2
        dialogue.ContinueCommand.Execute(null); // finishes: grandma's car page
        var car = Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen);

        mic.Fire("continue", isFinal: true);
        Assert.True(car.ContinueFlash);
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen); // still glowing

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        vm.Poll();
        Assert.False(car.ContinueFlash);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void Idle_HidesEvenWithoutTelemetry()
    {
        // Regression: the idle check must run before Poll's telemetry
        // early-return, so auto-hide works outside a live EVO session.
        var tel = new FakeTelemetry { PhysicsPresent = false };
        var (vm, clock, _, _) = Create(progress: Introduced, telemetry: tel);
        vm.Show();
        clock.Advance(TimeSpan.FromSeconds(15.1));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void VoiceButton_FiresOncePerUtterance()
    {
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda");
        session.Story.ChooseBrand("Honda");
        var voice = Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
        voice.ConfirmCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);

        mic.Fire("could you please continue");
        Assert.True(dialogue.ContinueFlash);
        Assert.Equal("Happy birthday!", dialogue.Body); // still glowing on page 1
        mic.Fire("continue"); // same utterance: muted
        mic.Fire("continue");
        Assert.Equal("Happy birthday!", dialogue.Body);
        mic.Fire("continue", isFinal: true); // endpoint re-arms, fires nothing
        Assert.Equal("Happy birthday!", dialogue.Body);

        DrainBeat(vm, clock); // the partial's tap turns the page after the glow
        Assert.False(dialogue.ContinueFlash);
        Assert.Equal("Thanks!", dialogue.Body);

        mic.Fire("continue"); // next utterance fires again -> finishes flow
        Assert.True(dialogue.ContinueFlash);
        Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen); // waits out the beat
        DrainBeat(vm, clock);
        // The finished dialogue lands on grandma's car page, no empty screen.
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen);
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
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda"); // default brand (no watcher prime in tests)
        Assert.True(vm.IsShown);
        session.Story.ChooseBrand("Honda"); // saved favorite must not drive the wake word
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
        // The first-run page has no voice labels, so the tail must land there
        // inert: no brand tap, no page change.
        var (vm, _, mic, _) = Create();
        mic.Fire("hey mazda"); // partial: wakes, shows the first-run page
        Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);
        mic.Fire("hey mazda", isFinal: true); // same utterance, must not select
        Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void WakeAgain_OpensMenuPage()
    {
        // Calling the app with the wake word opens the intro the first time;
        // the birthday hand-over closes the panel for the guided tour, and
        // the next wake opens the menu page. The tour is still the
        // introduction, so that page waits there instead of counting itself
        // away; the wake after the player hides it returns on the same page.
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda");
        Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);
        session.Story.ChooseBrand("Honda");
        var voice = Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
        voice.ConfirmCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null); // finishes: grandma's car page
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);
        Assert.False(vm.IsShown);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        clock.Advance(TimeSpan.FromSeconds(4)); // wake cooldown over
        mic.Fire("hey mazda");
        Assert.True(vm.IsShown);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        clock.Advance(TimeSpan.FromSeconds(16));
        vm.Poll(); // the tour still owns the intro: no idle-hide
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null); // the player parks it
        Assert.False(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(4)); // wake cooldown over
        mic.Fire("hey mazda");
        Assert.True(vm.IsShown);
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void VoiceJobs_OnMenu_OpensControlPage()
    {
        // The menu's Jobs tile answers the mic like a click, after the shared
        // teal acknowledgment: the tile glows for the beat, then the control
        // page with every job door opens.
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda");
        var control = ToControlPage(session);
        vm.ToggleShowCommand.Execute(null); // hide
        vm.ToggleShowCommand.Execute(null); // call it back: the menu page
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);

        mic.Fire("jobs");

        Assert.True(menu.JobsFlash);
        Assert.Same(menu, vm.CurrentScreen);
        DrainBeat(vm, clock);
        Assert.Same(control, vm.CurrentScreen);
    }

    [Fact]
    public void VoiceSettings_OnMenu_SaysBackAndReturnsToMenu()
    {
        // Settings reuses the first-run preferences page, but its action is
        // BACK and both the button and the mic return to the menu instead of
        // advancing the walk.
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda");
        ToControlPage(session);
        vm.ToggleShowCommand.Execute(null); // hide
        vm.ToggleShowCommand.Execute(null); // call it back: the menu page
        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);

        mic.Fire("settings");
        DrainBeat(vm, clock);

        var settings = Assert.IsType<PreferencesScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("BACK", settings.ContinueLabel);

        mic.Fire("back");

        Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void VoiceStartTaxi_ContinuesOnlyAfterFlash()
    {
        // The half-second acknowledgment glows on "START TAXI SHIFT"; the
        // button earns the CONTINUE name only when the glow is gone (the
        // shift itself starts immediately, behind the held-back label).
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda");
        var control = ToControlPage(session);
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
        var session = Create();
        var (vm, _, mic, _) = session;
        mic.Fire("hey mazda");
        var control = ToControlPage(session);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);

        mic.Fire("continue taxi shift");
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        Assert.True(control.TaxiFlash);

        proposal.AcceptCommand.Execute(null);
        Assert.True(vm.PickupCommand.CanExecute(null));
        Assert.Contains("To pickup", vm.JobLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_GarbledEventNames_AnswerToKeywords()
    {
        // "chant amelia" / "where is willie" are literal transcripts the
        // English model logged for "Cento Miglia" / "Where is Willy". Each
        // answers on its own game morning; every row is triggerable, so the
        // flash lands on the named row whichever day is highlighted.
        var (vm, clock, mic, _) = CreateIntroduced();
        mic.Fire("hey mazda");
        var calendar = ShowCalendar(vm);

        clock.Advance(TimeSpan.FromMinutes(106)); // ~2 game days: Wednesday
        vm.Poll();
        ShowCalendar(vm); // a call lands on the menu; open the calendar again
        mic.Fire("where is willie");
        // Willy is DayOfWeek 4: a Wednesday.
        CalendarRow willyDay = Assert.Single(calendar.Days, d => d.DayLabel == "Wednesday");
        Assert.True(willyDay.Flash);

        clock.Advance(TimeSpan.FromMinutes(212)); // ~4 more game days: Sunday
        vm.Poll();
        ShowCalendar(vm); // a call lands on the menu; open the calendar again
        mic.Fire("chant amelia");
        CalendarRow sunday = Assert.Single(calendar.Days, d => d.DayLabel == "Sunday");
        Assert.True(sunday.Flash);
    }

    [Fact]
    public void Voice_ContinueOneWord_TapsTaxiButton()
    {
        var session = Create();
        var (vm, _, mic, _) = session;
        mic.Fire("hey mazda");
        ToControlPage(session);

        mic.Fire("continue"); // the shortcut, not the full label
        Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void DialogueContinue_FinalLandsAfterNavigation_DoesNotFireTaxi()
    {
        // Regression for the logged 2026-10-14 surprise: "continue" closing
        // the birthday dialogue turned START TAXI SHIFT into CONTINUE without
        // the player ever asking. The utterance-level mute now swallows its
        // FINAL tail on grandma's car page (the same CONTINUE label), so it
        // cannot touch the control page's bare "continue" taxi keyword either.
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda");
        session.Story.ChooseBrand("Honda");
        var voice = Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
        voice.ConfirmCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null); // page 2 (last line)

        mic.Fire("continue"); // partial: finishes the dialogue
        clock.Advance(TimeSpan.FromSeconds(0.51));
        vm.Poll(); // beat drains: the car page is built, the panel stays up
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen);

        mic.Fire("continue", isFinal: true); // same utterance's tail
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen); // muted tail

        // The click path still works; the hand-over behind it lands on the
        // menu, whose words do not include "continue" either.
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        Assert.False(vm.IsShown);
        // Calling the app back puts the menu (not the control page) under it.
        vm.Show();
        Assert.True(vm.IsShown);

        // Jobs opens the untouched control page; the NEXT "continue" is a new
        // utterance and still taps the taxi entry.
        menu.JobsCommand.Execute(null);
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);
        mic.Fire("continue");
        Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void DialogueContinue_RescoredTail_OnNewPage_IsStillTheSameTap()
    {
        // Literal 2026-10-14 18:37:33/34 transcripts from the repro session:
        // the closing dialogue line answered to the partial "we continue",
        // and its final landed as "the continue" - Vosk re-scored the leading
        // word - after the page had changed. The hand-over now lands on the
        // menu, so that tail cannot reach the control page's bare "continue"
        // taxi keyword.
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda");
        session.Story.ChooseBrand("Honda");
        var voice = Assert.IsType<VoiceConsentScreenViewModel>(vm.CurrentScreen);
        voice.ConfirmCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null); // page 2 (last line)

        mic.Fire("we continue"); // partial: taps CONTINUE, waits out the beat
        clock.Advance(TimeSpan.FromSeconds(0.51));
        vm.Poll();
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen);

        // The car page's CONTINUE is the same label, so the utterance mute
        // swallows the re-scored tail before the player clicks it.
        mic.Fire("the continue", isFinal: true); // same utterance, re-scored
        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen);

        Assert.IsType<GrandmaCarScreenViewModel>(vm.CurrentScreen)
            .ContinueCommand.Execute(null);
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        Assert.False(vm.IsShown);
        // Call the menu back so a later tail has a page to land on.
        vm.Show();
        Assert.True(vm.IsShown);

        mic.Fire("the continue", isFinal: true); // same utterance, re-scored
        Assert.Same(menu, vm.CurrentScreen);

        // Jobs leaves the page untouched; a real next utterance still taps
        // through to the taxi proposal.
        menu.JobsCommand.Execute(null);
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);
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
        var session = Create(log: log);
        var mic = session.Voice;
        mic.Fire("hey mazda");
        ToControlPage(session);

        mic.Fire("continue"); // partial: shortcut fires
        mic.Fire("continue taxi shift", isFinal: true); // final: same utterance

        Assert.Equal(1, log.Infos.Count(m => m.StartsWith("voice button:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Voice_AliasPartialFullLabelFinal_FiresOnce()
    {
        // One utterance can hit the same row under two keys: the partial
        // hears the "where is" alias, the final the full "Where is Willy"
        // label. The shared action delegate keeps it a single tap on
        // Wednesday morning, when Willy's gate is open.
        var log = new TestLog();
        var (vm, clock, mic, _) = CreateIntroduced(log: log);
        mic.Fire("hey mazda");
        ShowCalendar(vm);

        clock.Advance(TimeSpan.FromMinutes(106)); // ~2 game days: Wednesday
        vm.Poll();
        ShowCalendar(vm); // a call lands on the menu; open the calendar again
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
        var session = Create();
        var (vm, clock, mic, _) = session;
        mic.Fire("hey mazda");
        ToControlPage(session);
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
