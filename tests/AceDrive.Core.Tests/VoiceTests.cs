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
        new Dictionary<string, string[]> { ["lancia"] = ["lancer"] },
        [new GrandmaCar("ks_mazda_mx5_na", "Mazda MX-5 NA", 1990, 12000, null)],
        [new DialogueLine("GRANDMA", "Happy birthday!"),
         new DialogueLine("YOU", "Thanks!")]);

    private static (MainViewModel Vm, FakeClock Clock, FakeVoiceListener Voice, FakeCarBrandSource Car)
        Create(bool voice = true, ILog? log = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var mic = new FakeVoiceListener(voice);
        var car = new FakeCarBrandSource();
        var vm = new MainViewModel(new FakeTelemetry(), clock, new TaxiShift(),
            new FakeStore(), log ?? new NullLogger(), FareTable.Default,
            new FakeOnboardingStore(), Content, mic, car, TodoTable.Default, DeliveryCatalog.Default);
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
        clock.Advance(TimeSpan.FromSeconds(8.9));
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
    public void Idle_HidesAfterNineSeconds()
    {
        var (vm, clock, _, _) = Create();
        vm.Show();
        clock.Advance(TimeSpan.FromSeconds(8.9));
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
        clock.Advance(TimeSpan.FromSeconds(8));
        vm.RecordActivity();
        clock.Advance(TimeSpan.FromSeconds(8));
        vm.Poll();
        Assert.True(vm.IsShown);
    }

    [Fact]
    public void Resize_SuspendsIdleHide()
    {
        // Holding a resize drag (even motionless) is activity: the 9 s
        // auto-hide must not fire mid-drag, and the window restarts on drop.
        var (vm, clock, _, _) = Create();
        vm.Show();
        vm.SetResizing(true);
        clock.Advance(TimeSpan.FromSeconds(14));
        vm.Poll();
        Assert.True(vm.IsShown);
        vm.SetResizing(false);
        clock.Advance(TimeSpan.FromSeconds(8.9));
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(0.2));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void VoiceButton_ResetsIdleTimer()
    {
        // Voice control is user input: a fired command restarts the 9 s
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

        clock.Advance(TimeSpan.FromSeconds(8));
        mic.Fire("start taxi shift"); // control-page voice button fires -> activity at t=8
        clock.Advance(TimeSpan.FromSeconds(8)); // 8 s since voice, < 9 s
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(2)); // 10 s since last input
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void MutedRepeat_StillResetsIdleTimer()
    {
        // Regression: a repeat of the same utterance stays muted (no double
        // fire) but still counts as presence: the 9 s auto-hide restarts, so
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
        clock.Advance(TimeSpan.FromSeconds(8)); // 8 s since the repeat, < 9 s
        vm.Poll();
        Assert.True(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(2)); // 10 s since last utterance
        vm.Poll();
        Assert.False(vm.IsShown);
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
        clock.Advance(TimeSpan.FromSeconds(10));
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
        var vm = new MainViewModel(tel, clock, new TaxiShift(),
            new FakeStore(), new NullLogger(), FareTable.Default,
            new FakeOnboardingStore(), Content, new FakeVoiceListener(),
            new FakeCarBrandSource(), TodoTable.Default, DeliveryCatalog.Default);
        vm.Show();
        clock.Advance(TimeSpan.FromSeconds(9.1));
        vm.Poll();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void VoiceButton_FiresOncePerUtterance()
    {
        var (vm, _, mic, _) = Create();
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
    public void WakeAgain_ReshowsControlHome()
    {
        // Full flow -> idle-hide -> wake lands back on the control home,
        // exactly like the companion shell returns to its home screen.
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        vm.ChooseBrand("Honda");
        vm.ChooseCar("ks_mazda_mx5_na");
        var voice = Assert.IsType<VoiceInfoScreenViewModel>(vm.CurrentScreen);
        voice.ContinueCommand.Execute(null);
        var dialogue = Assert.IsType<DialogueScreenViewModel>(vm.CurrentScreen);
        dialogue.ContinueCommand.Execute(null);
        dialogue.ContinueCommand.Execute(null); // finishes: control page shows
        clock.Advance(TimeSpan.FromSeconds(10));
        vm.Poll(); // idle hides
        Assert.False(vm.IsShown);
        clock.Advance(TimeSpan.FromSeconds(4)); // wake cooldown over
        mic.Fire("hey mazda");
        Assert.True(vm.IsShown);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void VoiceStartTaxi_ContinuesOnlyAfterFlash()
    {
        // The 1 s acknowledgment glows on "START TAXI SHIFT"; the button
        // earns the CONTINUE name only when the glow is gone (the shift
        // itself starts immediately, behind the held-back label).
        var (vm, clock, mic, _) = Create();
        mic.Fire("hey mazda");
        var control = ToControlPage(vm);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);

        mic.Fire("start taxi shift");
        Assert.True(control.TaxiFlash);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);

        clock.Advance(TimeSpan.FromSeconds(1.1));
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
        Assert.Contains("Proposed", vm.JobLine, StringComparison.Ordinal);
        Assert.True(control.TaxiFlash);
    }

    [Fact]
    public void Voice_GarbledEventNames_AnswerToKeywords()
    {
        // "chant amelia" / "where is willie" are literal transcripts the
        // English model logged for "Cento Miglia" / "Where is Willy".
        var (vm, _, mic, _) = Create();
        mic.Fire("hey mazda");
        var control = ToControlPage(vm);

        mic.Fire("chant amelia");
        CalendarRow sunday = Assert.Single(control.Days, d => d.DayLabel == "Sunday");
        Assert.True(sunday.Flash);

        mic.Fire("where is willie");
        // Willy is DayOfWeek 4 in the Sunday-first strip: a Wednesday.
        CalendarRow willyDay = Assert.Single(control.Days, d => d.DayLabel == "Wednesday");
        Assert.True(willyDay.Flash);
    }

    [Fact]
    public void Voice_ContinueOneWord_TapsTaxiButton()
    {
        var (vm, _, mic, _) = Create();
        mic.Fire("hey mazda");
        ToControlPage(vm);

        mic.Fire("continue"); // the shortcut, not the full label
        Assert.Contains("Proposed", vm.JobLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_ContinueThenFullLabel_FiresOnce()
    {
        // Partial "continue" and final "continue taxi shift" are the same
        // tap through two keys: one NewJob, not two.
        var log = new TestLog();
        var (vm, _, mic, _) = Create(log: log);
        mic.Fire("hey mazda");
        ToControlPage(vm);

        mic.Fire("continue"); // partial: shortcut fires
        mic.Fire("continue taxi shift", isFinal: true); // final: same tap, muted

        Assert.Equal(1, log.Infos.Count(m => m.StartsWith("voice button:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Voice_AliasPartialFullLabelFinal_FiresOnce()
    {
        // One utterance can hit the same row under two keys: the partial
        // hears the "where is" alias, the final the full "Where is Willy"
        // label. The shared action delegate keeps it a single tap.
        var log = new TestLog();
        var (vm, _, mic, _) = Create(log: log);
        mic.Fire("hey mazda");
        ToControlPage(vm);

        mic.Fire("where is"); // partial: alias fires
        mic.Fire("where is willy", isFinal: true); // final: same tap, muted

        Assert.Equal(1, log.Infos.Count(m => m.StartsWith("voice button:", StringComparison.Ordinal)));
    }
}
