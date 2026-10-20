using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Main control page runs headless: date header, calendar strip,
/// todo lines and voice actions resolve from the clock and resx.</summary>
public sealed class MainControlScreenTests
{
    // FakeClock default: Monday 2026-10-12 12:00 UTC (inside the 8-20 window).
    private static MainControlScreenViewModel Create(
        FakeClock? clock = null, TodoTable? todos = null,
        bool deliveryOpen = false, TaxiShift? shift = null,
        bool taxiStartsShift = false, DateTime? gameStart = null,
        RoadTestLedger? roads = null, VoiceBeat? beat = null,
        Hunger? hunger = null, Garage? garage = null,
        ICommand? carWashCommand = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        FakeClock tick = clock ?? new FakeClock();
        // Game time starts on the wall clock unless a test pins another
        // morning, so the Monday-noon default keeps its header.
        var game = new GameClock(tick, gameStart ?? tick.UtcNow);
        TaxiShift shiftState = shift ?? new TaxiShift();
        return new MainControlScreenViewModel(
            beat ?? new VoiceBeat(tick), game, tick, roads ?? TestRoadCars.Ledger(),
            todos ?? TodoTable.Default, hunger ?? new Hunger(HungerRules.Default),
            garage ?? new Garage([]),
            CalendarStamp.Default,
            shiftState,
            taxiStartsShift
                ? new RelayCommand(() => shiftState.Start(tick.UtcNow))
                : new RelayCommand(() => { }),
            new RelayCommand(() => { }, () => deliveryOpen),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            carWashCommand ?? new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new ParameterizedCommand<int>(_ => { }, day => day == game.Today));
    }

    [Fact]
    public void DateHeader_ReplacesTitle()
    {
        var vm = Create();
        Assert.Equal("Monday 12:00 | Week 1", vm.DateHeader);
    }

    [Fact]
    public void Days_HighlightToday()
    {
        var vm = Create();
        Assert.Equal(7, vm.Days.Count);
        CalendarRow monday = Assert.Single(vm.Days, d => d.DayLabel == "Monday");
        Assert.True(monday.IsToday);
        Assert.True(monday.IsOpen);
        Assert.Equal("Market valuation", monday.EventLabel);
        // Monday opens the strip, Sunday closes it; Thursday carries no event.
        Assert.Equal("Monday", vm.Days[0].DayLabel);
        Assert.False(vm.Days[6].IsToday);
        Assert.Equal(string.Empty, vm.Days[3].EventLabel);
    }

    [Fact]
    public void RoadTestButton_NamesRunningCarInCaps()
    {
        // The car rides inside the button outline; the mic still answers to
        // short "road test" (see Voice_RoadTestAnswersShortOnly).
        var roads = TestRoadCars.Ledger();
        var vm = Create(roads: roads);
        var car = Assert.IsType<CarSpec>(roads.Current);
        CalendarRow tuesday = Assert.Single(vm.Days, d => d.DayLabel == "Tuesday");
        Assert.Equal(TestRoadCars.Button(car), tuesday.EventLabel);
    }

    [Fact]
    public void Voice_RoadTestAnswersShortOnly()
    {
        var roads = TestRoadCars.Ledger();
        var vm = Create(
            roads: roads,
            gameStart: new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc));
        var car = Assert.IsType<CarSpec>(roads.Current);
        string button = TestRoadCars.Button(car);
        Assert.Equal(button, Assert.Single(vm.Days, d => d.DayLabel == "Tuesday").EventLabel);
        Assert.True(vm.VoiceActions.ContainsKey("Road test"));
        Assert.DoesNotContain(button, vm.VoiceActions.Keys);
        vm.VoiceActions["Road test"].Invoke();
        Assert.True(Assert.Single(vm.Days, d => d.DayLabel == "Tuesday").Flash);
    }

    [Fact]
    public void RoadTestRow_SwitchesPickAfterTested()
    {
        // Completing the test is the only thing that changes the car.
        var roads = TestRoadCars.Ledger();
        var vm = Create(roads: roads);
        string first = Assert.Single(vm.Days, d => d.DayLabel == "Tuesday").EventLabel;
        Assert.StartsWith("Road test: ", first, StringComparison.Ordinal);
        roads.MarkTested();
        vm.Refresh();
        string second = Assert.Single(vm.Days, d => d.DayLabel == "Tuesday").EventLabel;
        Assert.StartsWith("Road test: ", second, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void WeekDisplay_FollowsDisplayWeek()
    {
        var vm = Create();
        vm.DisplayWeek = 3;
        vm.Refresh();
        Assert.Equal("Monday 12:00 | Week 3", vm.DateHeader);
    }

    [Fact]
    public void DayButtons_AllTappable_GateDecidesOnTap()
    {
        // Clicks are never swallowed by a disabled button: off-gate rows
        // answer with the warning text instead (Unity parity), so every
        // day's command can execute; TapDay decides what a tap means.
        var vm = Create();
        Assert.Equal(2, vm.Today);
        Assert.True(vm.DayCommand.CanExecute(2));
        Assert.True(vm.DayCommand.CanExecute(1));
        Assert.True(vm.DayCommand.CanExecute(7));
    }

    [Fact]
    public void Tap_OpenDay_GlowsTealForOneBeat()
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        vm.DayCommand.Execute(2); // Monday noon: inside the gate
        CalendarRow monday = Assert.Single(vm.Days, d => d.DayLabel == "Monday");
        Assert.True(monday.Flash);
        Assert.Equal("Market valuation", monday.EventLabel);

        clock.Advance(TimeSpan.FromSeconds(0.51));
        vm.UpdateFlash();
        Assert.All(vm.Days, d => Assert.False(d.Flash));
    }

    [Fact]
    public void Tap_TodayOutsideWindow_GlowsThenWarnsThenRestores()
    {
        // Monday 21:00: the row is today but past the 8pm gate. The tap
        // still acknowledges with the one-beat teal glow, then the COME
        // BACK hint swaps in at the beat's end and the original label
        // returns once Unity's three-second restore window has passed.
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var vm = Create(clock, beat: beat,
            gameStart: new DateTime(2026, 10, 12, 21, 0, 0, DateTimeKind.Utc));
        vm.DayCommand.Execute(2);
        CalendarRow monday = Assert.Single(vm.Days, d => d.DayLabel == "Monday");
        Assert.True(monday.Flash); // the tap lands first: glow on the old label
        Assert.Equal("Market valuation", monday.EventLabel);

        clock.Advance(TimeSpan.FromSeconds(0.51));
        beat.Poll(); // what the 10 Hz poll does at the beat's end
        vm.UpdateFlash();
        monday = Assert.Single(vm.Days, d => d.DayLabel == "Monday");
        Assert.Equal("COME BACK ON MONDAY BETWEEN 8am AND 8pm", monday.EventLabel);
        Assert.False(monday.Flash); // the glow ended with the beat

        clock.Advance(TimeSpan.FromSeconds(3.1));
        vm.UpdateFlash();
        monday = Assert.Single(vm.Days, d => d.DayLabel == "Monday");
        Assert.Equal("Market valuation", monday.EventLabel);
    }

    [Fact]
    public void Tap_OtherDay_GlowsThenWarnsWithItsOwnDayName()
    {
        // A locked row answers the same way as the gated today: glow for
        // the shared beat, then the hint naming its own day.
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var vm = Create(clock, beat: beat);
        vm.DayCommand.Execute(1); // Cento Miglia waits for Sunday
        CalendarRow sunday = Assert.Single(vm.Days, d => d.DayLabel == "Sunday");
        Assert.True(sunday.Flash);
        Assert.Equal("Cento Miglia", sunday.EventLabel);

        clock.Advance(TimeSpan.FromSeconds(0.51));
        beat.Poll();
        vm.UpdateFlash();
        sunday = Assert.Single(vm.Days, d => d.DayLabel == "Sunday");
        Assert.Equal("COME BACK ON SUNDAY BETWEEN 8am AND 8pm", sunday.EventLabel);
    }

    [Fact]
    public void DayHighlight_FollowsGameClock()
    {
        // Half speed: ~53 real minutes carry the game across midnight.
        var clock = new FakeClock();
        var vm = Create(clock);
        Assert.Equal(2, vm.Today);
        clock.Advance(TimeSpan.FromMinutes(53));
        vm.Refresh();
        Assert.Equal(3, vm.Today);
        CalendarRow tuesday = Assert.Single(vm.Days, d => d.DayLabel == "Tuesday");
        Assert.True(tuesday.IsToday);
    }

    [Fact]
    public void TodoLines_StartWithMaxGrocery()
    {
        var vm = Create();
        Assert.Equal(4, vm.TodoLines.Count);
        Assert.Contains("Max's", vm.TodoLines[0], StringComparison.Ordinal);
        Assert.Contains("3", vm.TodoLines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_IncludesAllButtons()
    {
        var vm = Create(deliveryOpen: false);
        Assert.True(vm.VoiceActions.ContainsKey("START TAXI SHIFT"));
        Assert.True(vm.VoiceActions.ContainsKey("DELIVERY JOB"));
        Assert.True(vm.VoiceActions.ContainsKey("CAR HAULING JOB"));
        Assert.True(vm.VoiceActions.ContainsKey("Market valuation"));
    }

    [Fact]
    public void VoiceFlash_ClearsAfterHalfSecond()
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        vm.VoiceActions["START TAXI SHIFT"].Invoke();
        Assert.True(vm.TaxiFlash);
        clock.Advance(TimeSpan.FromMilliseconds(499));
        vm.UpdateFlash();
        Assert.True(vm.TaxiFlash);
        clock.Advance(TimeSpan.FromMilliseconds(2));
        vm.UpdateFlash();
        Assert.False(vm.TaxiFlash);
    }

    [Fact]
    public void VoiceFlash_DayRow_ClearsAfterHalfSecond()
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        vm.VoiceActions["Market valuation"].Invoke();
        CalendarRow monday = Assert.Single(vm.Days, d => d.DayLabel == "Monday");
        Assert.True(monday.Flash);
        clock.Advance(TimeSpan.FromSeconds(0.51));
        vm.UpdateFlash();
        Assert.All(vm.Days, d => Assert.False(d.Flash));
    }

    [Fact]
    public void Voice_DeliveryTap_GlowsThenWaitsOutTheBeatBeforeNavigating()
    {
        // The page change waits so the teal acknowledgment stays visible:
        // the tap lights DELIVERY JOB for one half-second beat, and only
        // then does the injected command open the delivery career page.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool fired = false;
        var game = new GameClock(clock, clock.UtcNow);
        var vm = new MainControlScreenViewModel(
            beat, game, clock, TestRoadCars.Ledger(), TodoTable.Default,
            new Hunger(HungerRules.Default), new Garage([]),
            CalendarStamp.Default, new TaxiShift(),
            new RelayCommand(() => { }),
            new RelayCommand(() => fired = true),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new ParameterizedCommand<int>(_ => { }, day => day == game.Today));

        vm.VoiceActions["DELIVERY JOB"].Invoke();
        Assert.True(vm.DeliveryFlash);
        Assert.False(fired); // the page has not flipped yet

        clock.Advance(TimeSpan.FromMilliseconds(499));
        beat.Poll();
        Assert.False(fired); // still inside the beat

        clock.Advance(TimeSpan.FromMilliseconds(2));
        beat.Poll(); // what the 10 Hz poll does at the beat's end
        Assert.True(fired);
        Assert.False(vm.DeliveryFlash); // the glow ends with the tap
    }

    // The car-hauling entry is a placeholder (PORTPLAN 20 has no flow yet):
    // a tap only answers with the shared teal glow, so the glow is the whole
    // observable contract. Every spoken slice must reach the same button.
    [Fact]
    public void Voice_HaulTap_GlowsThenClearsAfterHalfSecond()
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        vm.VoiceActions["CAR HAULING JOB"].Invoke();
        Assert.True(vm.HaulFlash);
        clock.Advance(TimeSpan.FromSeconds(0.51));
        vm.UpdateFlash();
        Assert.False(vm.HaulFlash);
    }

    [Theory]
    [InlineData("car")]
    [InlineData("car hauling")]
    [InlineData("car hauling job")]
    public void Voice_HaulAnswersToEverySpokenSlice(string spoken)
    {
        var vm = Create();
        string? label = VoiceMatch.MatchLabel(spoken, vm.VoiceActions.Keys);
        Assert.NotNull(label);
        vm.VoiceActions[label].Invoke();
        Assert.True(vm.HaulFlash);
    }

    [Fact]
    public void Voice_OffersOpenDelivery()
    {
        var vm = Create(deliveryOpen: true);
        Assert.True(vm.VoiceActions.ContainsKey("DELIVERY JOB"));
    }

    [Fact]
    public void TaxiLabel_FollowsShiftState()
    {
        var shift = new TaxiShift();
        var vm = Create(shift: shift);
        Assert.Equal("START TAXI SHIFT", vm.TaxiLabel);
        shift.Start(new FakeClock().UtcNow);
        vm.Refresh();
        Assert.Equal("CONTINUE TAXI SHIFT", vm.TaxiLabel);
        Assert.True(vm.VoiceActions.ContainsKey("CONTINUE TAXI SHIFT"));
    }

    [Fact]
    public void Refresh_AdvancesDateHeader()
    {
        // Game time, not wall time: 2.2 real seconds carry one game minute.
        var clock = new FakeClock();
        var vm = Create(clock);
        clock.Advance(TimeSpan.FromSeconds(2.2));
        vm.Refresh();
        Assert.Equal("Monday 12:01 | Week 1", vm.DateHeader);
    }

    [Fact]
    public void Voice_TaxiTap_HoldsStartLabelUntilFlashClears()
    {
        var clock = new FakeClock();
        var vm = Create(clock, taxiStartsShift: true);
        vm.VoiceActions["START TAXI SHIFT"].Invoke();
        vm.Refresh(); // what MainViewModel.OpenTaxiShift does mid-command in the app
        Assert.True(vm.TaxiFlash);
        Assert.Equal("START TAXI SHIFT", vm.TaxiLabel); // glows, name unchanged

        clock.Advance(TimeSpan.FromSeconds(0.51)); // past the half-second beat
        vm.UpdateFlash();
        Assert.False(vm.TaxiFlash);
        Assert.Equal("CONTINUE TAXI SHIFT", vm.TaxiLabel);
    }

    [Fact]
    public void TaxiClick_NoFlash_FlipsLabelAtOnce()
    {
        var vm = Create(taxiStartsShift: true);
        vm.TaxiCommand.Execute(null);
        vm.Refresh(); // real wiring repaints inside the taxi tap
        Assert.Equal("CONTINUE TAXI SHIFT", vm.TaxiLabel);
    }

    [Fact]
    public void Voice_BothShiftLabels_AnswerInEitherState()
    {
        var vm = Create();
        Assert.True(vm.VoiceActions.ContainsKey("START TAXI SHIFT"));
        Assert.True(vm.VoiceActions.ContainsKey("CONTINUE TAXI SHIFT"));

        // "CONTINUE TAXI SHIFT" fires the taxi entry even while the
        // button still reads START (shift closed).
        var clock = new FakeClock();
        var shift = new TaxiShift();
        var vm2 = Create(clock, shift: shift, taxiStartsShift: true);
        vm2.VoiceActions["CONTINUE TAXI SHIFT"].Invoke();
        Assert.True(shift.IsStarted);
        Assert.True(vm2.TaxiFlash);
    }

    [Fact]
    public void Voice_StartTaxi_TapsTaxiEntry()
    {
        // The request from the logged 2026-10-14 sessions: two spoken words
        // fire what three words print, beside the existing triggers.
        var clock = new FakeClock();
        var shift = new TaxiShift();
        var vm = Create(clock, shift: shift, taxiStartsShift: true);
        Assert.True(vm.VoiceActions.ContainsKey("start taxi"));

        vm.VoiceActions["start taxi"].Invoke();
        Assert.True(shift.IsStarted);
        Assert.True(vm.TaxiFlash);
    }

    [Fact]
    public void Voice_EventKeywords_FlashTheirDayRows()
    {
        // Only the highlighted row answers, so each garbled name is heard
        // on its own game morning: Sunday for Cento, Wednesday for Willy.
        var sunday = Create(gameStart: new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc));
        sunday.VoiceActions["cento"].Invoke();
        CalendarRow sundayRow = Assert.Single(sunday.Days, d => d.DayLabel == "Sunday");
        Assert.True(sundayRow.Flash);
        Assert.False(sunday.VoiceActions.ContainsKey("where is"));

        var wednesday = Create(gameStart: new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
        wednesday.VoiceActions["where is"].Invoke();
        // Willy is DayOfWeek 4: a Wednesday.
        CalendarRow willyDay = Assert.Single(wednesday.Days, d => d.DayLabel == "Wednesday");
        Assert.True(willyDay.Flash);
        Assert.False(wednesday.VoiceActions.ContainsKey("cento"));
    }

    [Fact]
    public void Voice_GarbledUtterances_RescuedByKeywords()
    {
        // Actual transcripts the English model produced for "Cento Miglia",
        // matched on the Sunday page whose row owns those keywords.
        var sunday = Create(gameStart: new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal("chantal", VoiceMatch.MatchLabel("chantal", sunday.VoiceActions.Keys));
        Assert.Equal("gentlemen leah", VoiceMatch.MatchLabel("gentlemen leah", sunday.VoiceActions.Keys));
        Assert.Equal("sent media", VoiceMatch.MatchLabel("sent media", sunday.VoiceActions.Keys));

        var wednesday = Create(gameStart: new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal("where is", VoiceMatch.MatchLabel("where is willie", wednesday.VoiceActions.Keys));
    }

    [Fact]
    public void Voice_ContinueShortcut_FiresTaxi()
    {
        // One word, not the whole label: "continue" taps the taxi button.
        var clock = new FakeClock();
        var shift = new TaxiShift();
        var vm = Create(clock, shift: shift, taxiStartsShift: true);
        Assert.True(vm.VoiceActions.ContainsKey("continue"));
        vm.VoiceActions["continue"].Invoke();
        Assert.True(shift.IsStarted);
        Assert.True(vm.TaxiFlash);
    }

    [Fact]
    public void HungerLine_ShowsTheReadingWhereTheJobButtonsSit()
    {
        // Unity's map HUD prints HUNGER x/36 above the job entries; the port
        // prints it above the job buttons (PORTPLAN 26).
        var vm = Create();
        Assert.Equal("HUNGER 0/36", vm.HungerText);
        Assert.False(vm.HungerAmber);
    }

    [Fact]
    public void HungerLine_WearsTheAmberBand()
    {
        // Unity YouScreen colours the reading orange from h 16 to h 29.
        var clock = new FakeClock();
        DateTime start = clock.UtcNow;
        var hunger = new Hunger(HungerRules.Default);
        _ = hunger.AdvanceTo(start, onboarded: true, hasPancakes: false);
        for (int i = 0; i < 16; i++)
        {
            _ = hunger.AdvanceTo(start.AddHours(i + 1), onboarded: true, hasPancakes: false);
        }

        var vm = Create(clock, hunger: hunger);
        Assert.Equal("HUNGER 16/36", vm.HungerText);
        Assert.True(vm.HungerAmber);
    }

    [Fact]
    public void FilthLine_ShowsTheCurrentCarsDirt()
    {
        // Unity's MapScreen HUD prints FILTH x/25 beside the hunger line
        // (PORTPLAN 33); the reading is the car driven now.
        var vm = Create(garage: Dirty(0));
        Assert.Equal("FILTH 0/25", vm.FilthText);
        Assert.False(vm.FilthAmber);
        Assert.False(vm.FilthFullyDirty);
    }

    [Fact]
    public void FilthLine_WearsTheAmberBand()
    {
        // Unity MapScreen colours the reading orange from 15 up to the cap.
        var vm = Create(garage: Dirty(15));
        Assert.Equal("FILTH 15/25", vm.FilthText);
        Assert.True(vm.FilthAmber);
        Assert.False(vm.FilthFullyDirty);
    }

    [Fact]
    public void FilthLine_AtTheCapReadsFullyDirty()
    {
        // Unity paints the cap the shared mint instead of the amber.
        var vm = Create(garage: Dirty(25));
        Assert.Equal("FILTH 25/25", vm.FilthText);
        Assert.False(vm.FilthAmber);
        Assert.True(vm.FilthFullyDirty);
    }

    [Fact]
    public void Voice_CarWashAnswersItsLabelAndTheSpokenSlice()
    {
        // The temporary entry (PORTPLAN 33) answers to its text and to what
        // the mic hears short of it, like the bank/garage/dealer entries.
        bool opened = false;
        var vm = Create(carWashCommand: new RelayCommand(() => opened = true));
        Assert.True(vm.VoiceActions.ContainsKey("CAR WASH"));
        Assert.True(vm.VoiceActions.ContainsKey("wash"));
        vm.VoiceActions["wash"].Invoke();
        Assert.True(opened);
        Assert.True(vm.CarWashFlash);
    }

    private static Garage Dirty(int filth)
    {
        var car = new CarSpec("ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Passengers: 1);
        var garage = new Garage([car]);
        garage.Add(car, week: 1).Filth.Add(filth);
        return garage;
    }
}
