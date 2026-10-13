using System.Globalization;
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
        bool deliveryOpen = false, TaxiShift? shift = null, bool dayOpen = false,
        bool taxiStartsShift = false)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        FakeClock tick = clock ?? new FakeClock();
        TaxiShift shiftState = shift ?? new TaxiShift();
        return new MainControlScreenViewModel(
            tick,
            todos ?? TodoTable.Default,
            CalendarStamp.Default,
            shiftState,
            taxiStartsShift
                ? new RelayCommand(() => shiftState.Start(tick.UtcNow))
                : new RelayCommand(() => { }),
            new RelayCommand(() => { }, () => deliveryOpen),
            new RelayCommand(() => { }, () => dayOpen));
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
        Assert.False(vm.Days[0].IsToday);
        Assert.Equal(string.Empty, vm.Days[4].EventLabel);
    }

    [Fact]
    public void DayButtons_StayDisabledUntilMap()
    {
        var vm = Create();
        Assert.False(vm.DayCommand.CanExecute(null));
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
        Assert.True(vm.VoiceActions.ContainsKey("Market valuation"));
    }

    [Fact]
    public void VoiceFlash_ClearsAfterOneSecond()
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        vm.VoiceActions["START TAXI SHIFT"].Invoke();
        Assert.True(vm.TaxiFlash);
        clock.Advance(TimeSpan.FromMilliseconds(999));
        vm.UpdateFlash();
        Assert.True(vm.TaxiFlash);
        clock.Advance(TimeSpan.FromMilliseconds(2));
        vm.UpdateFlash();
        Assert.False(vm.TaxiFlash);
    }

    [Fact]
    public void VoiceFlash_DayRow_ClearsAfterOneSecond()
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        vm.VoiceActions["Market valuation"].Invoke();
        CalendarRow monday = Assert.Single(vm.Days, d => d.DayLabel == "Monday");
        Assert.True(monday.Flash);
        clock.Advance(TimeSpan.FromSeconds(1.1));
        vm.UpdateFlash();
        Assert.All(vm.Days, d => Assert.False(d.Flash));
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
        var clock = new FakeClock();
        var vm = Create(clock);
        clock.Advance(TimeSpan.FromHours(2));
        vm.Refresh();
        Assert.Equal("Monday 14:00 | Week 1", vm.DateHeader);
    }

    [Fact]
    public void Voice_TaxiTap_HoldsStartLabelUntilFlashClears()
    {
        var clock = new FakeClock();
        var vm = Create(clock, taxiStartsShift: true);
        vm.VoiceActions["START TAXI SHIFT"].Invoke();
        vm.Refresh(); // what MainViewModel.NewJob does mid-command in the app
        Assert.True(vm.TaxiFlash);
        Assert.Equal("START TAXI SHIFT", vm.TaxiLabel); // glows, name unchanged

        clock.Advance(TimeSpan.FromSeconds(1.1));
        vm.UpdateFlash();
        Assert.False(vm.TaxiFlash);
        Assert.Equal("CONTINUE TAXI SHIFT", vm.TaxiLabel);
    }

    [Fact]
    public void TaxiClick_NoFlash_FlipsLabelAtOnce()
    {
        var vm = Create(taxiStartsShift: true);
        vm.TaxiCommand.Execute(null);
        vm.Refresh(); // real wiring repaints inside NewJob
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
    public void Voice_EventKeywords_FlashTheirDayRows()
    {
        var vm = Create();
        vm.VoiceActions["cento"].Invoke();
        CalendarRow sunday = Assert.Single(vm.Days, d => d.DayLabel == "Sunday");
        Assert.True(sunday.Flash);

        vm.VoiceActions["where is"].Invoke();
        // Willy is DayOfWeek 4 in the Sunday-first strip: a Wednesday.
        CalendarRow willyDay = Assert.Single(vm.Days, d => d.DayLabel == "Wednesday");
        Assert.True(willyDay.Flash);
    }

    [Fact]
    public void Voice_GarbledUtterances_RescuedByKeywords()
    {
        var vm = Create();
        // Actual transcripts the English model produced for "Cento Miglia".
        Assert.Equal("chantal", VoiceMatch.MatchLabel("chantal", vm.VoiceActions.Keys));
        Assert.Equal("gentlemen leah", VoiceMatch.MatchLabel("gentlemen leah", vm.VoiceActions.Keys));
        Assert.Equal("sent media", VoiceMatch.MatchLabel("sent media", vm.VoiceActions.Keys));
        Assert.Equal("where is", VoiceMatch.MatchLabel("where is willie", vm.VoiceActions.Keys));
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
}
