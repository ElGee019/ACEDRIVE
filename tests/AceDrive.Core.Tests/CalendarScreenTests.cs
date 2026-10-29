using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Calendar page runs headless: clock header, weekly strip, gate taps
/// and voice actions resolve from the clock and resx.</summary>
public sealed class CalendarScreenTests
{
    // FakeClock default: Monday 2026-10-12 12:00 UTC (inside the 8-20 window).
    private static CalendarScreenViewModel Create(
        FakeClock? clock = null, RoadTestLedger? roads = null, VoiceBeat? beat = null,
        DateTime? gameStart = null, int week = 1,
        Action<int>? openDay = null, Action? back = null,
        WhereIsWillyLedger? willy = null, bool coffeeCareer = true,
        bool ignoreGates = false)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        FakeClock tick = clock ?? new FakeClock();
        // Game time starts on the wall clock unless a test pins another
        // morning, so the Monday-noon default keeps its header.
        var game = new GameClock(tick, gameStart ?? tick.UtcNow);
        return new CalendarScreenViewModel(
            beat ?? new VoiceBeat(tick), game, tick, roads ?? TestRoadCars.Ledger(),
            CalendarStamp.Default with { Week = week },
            new ParameterizedCommand<int>(day => openDay?.Invoke(day)),
            new RelayCommand(() => back?.Invoke()),
            willy ?? new WhereIsWillyLedger(), () => coffeeCareer, ignoreGates);
    }

    [Fact]
    public void Clock_ShowsGameTimeAndDayWeek()
    {
        var vm = Create();
        Assert.Equal("12:00", vm.ClockText);
        Assert.Equal("MON / WEEK 1", vm.DayWeekText);
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
        Assert.Equal("MON", vm.Days[0].ShortLabel);
        Assert.Equal("SUN", vm.Days[6].ShortLabel);
        Assert.False(vm.Days[6].IsToday);
        Assert.Equal(string.Empty, vm.Days[3].EventLabel);
    }

    [Fact]
    public void SaturdayButton_ReadsTouristenfahrtenDeliveries()
    {
        // The Saturday row owns the Touristenfahrten run, so the strip renders
        // its button between Grandma's pancakes and Cento Miglia.
        var vm = Create();
        CalendarRow saturday = Assert.Single(vm.Days, d => d.DayLabel == "Saturday");
        Assert.Equal(7, saturday.DayOfWeek);
        Assert.Equal("Touristenfahrten Deliveries", saturday.EventLabel);
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
        Assert.Equal("MON / WEEK 3", vm.DayWeekText);
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
        int opened = 0;
        var vm = Create(clock, openDay: day => opened = day);
        vm.DayCommand.Execute(2); // Monday noon: inside the gate
        Assert.Equal(2, opened);
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
    public void Refresh_AdvancesClock()
    {
        // Game time, not wall time: 2.2 real seconds carry one game minute.
        var clock = new FakeClock();
        var vm = Create(clock);
        clock.Advance(TimeSpan.FromSeconds(2.2));
        vm.Refresh();
        Assert.Equal("12:01", vm.ClockText);
        Assert.Equal("MON / WEEK 1", vm.DayWeekText);
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
    public void Voice_EventKeywords_FlashTheirDayRows()
    {
        // Every event row answers, not just the highlighted one: each garbled
        // name reaches its own row, and the gate — not the voice table —
        // decides whether the tap opens or warns. Sunday page: Cento is
        // today, Willy is an off-day row.
        var sunday = Create(gameStart: new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc));
        sunday.VoiceActions["cento"].Invoke();
        CalendarRow sundayRow = Assert.Single(sunday.Days, d => d.DayLabel == "Sunday");
        Assert.True(sundayRow.Flash);

        sunday.VoiceActions["where is"].Invoke();
        // Willy is DayOfWeek 4: a Wednesday, still an answerable row here.
        CalendarRow offDayWilly = Assert.Single(sunday.Days, d => d.DayLabel == "Wednesday");
        Assert.True(offDayWilly.Flash);

        var wednesday = Create(gameStart: new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
        wednesday.VoiceActions["where is"].Invoke();
        // Willy is DayOfWeek 4: a Wednesday.
        CalendarRow willyDay = Assert.Single(wednesday.Days, d => d.DayLabel == "Wednesday");
        Assert.True(willyDay.Flash);
        Assert.True(wednesday.VoiceActions.ContainsKey("cento"));
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
    public void WillyRow_CompletedToday_ReadsCompletedAndWarnsWithoutOpening()
    {
        // Wednesday, and the run on this game date already paid: the row wears
        // the completed label, grey in the view, and its tap only hints.
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        int opened = -1;
        var vm = Create(
            clock, beat: beat,
            gameStart: new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc),
            openDay: day => opened = day,
            willy: new WhereIsWillyLedger(DriverState.Fresh with { LastWillyDate = "2026-10-14" }));
        CalendarRow willy = Assert.Single(vm.Days, d => d.DayOfWeek == 4);
        Assert.True(willy.Completed);
        Assert.False(willy.Locked);
        Assert.Equal("Where is Willy (Completed)", willy.EventLabel);

        vm.DayCommand.Execute(4);
        Assert.Equal(-1, opened);
        Assert.True(Assert.Single(vm.Days, d => d.DayOfWeek == 4).Flash);

        clock.Advance(TimeSpan.FromSeconds(0.51));
        beat.Poll();
        vm.UpdateFlash();
        Assert.Equal(
            "SEE YOU NEXT WEDNESDAY",
            Assert.Single(vm.Days, d => d.DayOfWeek == 4).EventLabel);

        clock.Advance(TimeSpan.FromSeconds(3.1));
        vm.UpdateFlash();
        Assert.Equal(
            "Where is Willy (Completed)",
            Assert.Single(vm.Days, d => d.DayOfWeek == 4).EventLabel);
    }

    [Fact]
    public void WillyRow_CoffeeCareerClosed_ReadsLockedAndWarnsWithoutOpening()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        int opened = -1;
        var vm = Create(
            clock, beat: beat,
            gameStart: new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc),
            openDay: day => opened = day,
            coffeeCareer: false);
        CalendarRow willy = Assert.Single(vm.Days, d => d.DayOfWeek == 4);
        Assert.True(willy.Locked);
        Assert.False(willy.Completed);
        Assert.Equal("UNLOCK COFFEE BEANS CAREER FIRST", willy.EventLabel);

        vm.DayCommand.Execute(4);
        Assert.Equal(-1, opened);

        clock.Advance(TimeSpan.FromSeconds(0.51));
        beat.Poll();
        vm.UpdateFlash();
        Assert.Equal(
            "UNLOCK COFFEE BEANS CAREER FIRST",
            Assert.Single(vm.Days, d => d.DayOfWeek == 4).EventLabel);
    }

    [Fact]
    public void Tap_WithGatesIgnored_OpensAnyDayAtAnyHour()
    {
        // TEMPORARY (testing): the tester switch the app passes, so every row
        // answers: Sunday's Cento Miglia opens on a Monday night.
        int opened = -1;
        var vm = Create(
            gameStart: new DateTime(2026, 10, 12, 21, 0, 0, DateTimeKind.Utc),
            openDay: day => opened = day,
            ignoreGates: true);

        vm.DayCommand.Execute(1);

        Assert.Equal(1, opened);
        CalendarRow sunday = Assert.Single(vm.Days, d => d.DayLabel == "Sunday");
        Assert.True(sunday.IsOpen);
        Assert.True(sunday.Flash);
        Assert.Equal("Cento Miglia", sunday.EventLabel);
    }

    [Fact]
    public void WillyRow_WithGatesIgnored_ReadsOpenDespiteTheStampAndTheCareer()
    {
        // TEMPORARY (testing): the stamp and the closed coffee career must not
        // grey the row out while the gates are open.
        var vm = Create(
            gameStart: new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc),
            willy: new WhereIsWillyLedger(
                DriverState.Fresh with { LastWillyDate = "2026-10-14" }),
            coffeeCareer: false,
            ignoreGates: true);

        CalendarRow willy = Assert.Single(vm.Days, d => d.DayOfWeek == 4);

        Assert.False(willy.Completed);
        Assert.False(willy.Locked);
        Assert.Equal("Where is Willy", willy.EventLabel);
    }

    [Fact]
    public void Voice_BackGlowsThenNavigates()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool back = false;
        var vm = Create(clock, beat: beat, back: () => back = true);

        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(back); // the page waits out the glow

        clock.Advance(TimeSpan.FromSeconds(0.51));
        beat.Poll();
        Assert.True(back);
        Assert.False(vm.BackFlash); // the glow ends with the navigation
    }
}
