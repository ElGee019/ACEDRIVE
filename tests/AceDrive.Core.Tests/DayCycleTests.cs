using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The session calendar on its own: the day flip and the week it rolls (with
/// the clock save that keeps the moment honest), the second-guarded shift
/// countdown and the idle shift timeout with the record and page it opens.
/// The coordinator is a fake, so each test sees only this collaborator's own
/// decisions.
/// </summary>
public sealed class DayCycleTests
{
    private const int StartWeek = 2;

    // The FakeClock opens on Monday 12:00; a game clock started late on Sunday
    // crosses into Monday one game minute later (2.2 real seconds).
    private static readonly DateTime MondayNoon = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SundayLate = new(2026, 10, 11, 23, 59, 0, DateTimeKind.Utc);
    private static readonly DateTime TuesdayLate = new(2026, 10, 13, 23, 59, 0, DateTimeKind.Utc);
    private static readonly TimeSpan OneGameMinute = TimeSpan.FromSeconds(GameClock.RealSecondsPerGameMinute);

    private sealed record Fixture(
        DayCycle Cycle, FakeDayCycleHost Host, FakeClock Clock, TaxiShift Shift,
        ShiftRecord Records, FakeGameClockStore Clocks, FakeDriverStore Drivers,
        FakeShiftStore Shifts, FakePlayerTelemetry Online, FakeShiftStandings Board,
        TestLog Log);

    public DayCycleTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static Fixture Build(DateTime gameStart, TaxiShift? shift = null)
    {
        var clock = new FakeClock();
        var game = new GameClock(clock, gameStart);
        var shiftInstance = shift ?? new TaxiShift();
        var records = new ShiftRecord(null);
        var board = new FakeShiftStandings();
        var online = new FakePlayerTelemetry();
        var wallet = new DriverWallet();
        var garage = new Garage([]);
        var clockStore = new FakeGameClockStore();
        var driverStore = new FakeDriverStore();
        var shiftStore = new FakeShiftStore();
        // One instance per service graph: the saver banks the same shift and
        // record the cycle holds, so the assertions read what a player would
        // find on disk.
        var saver = new GameStateSaver(
            clock, game, clockStore, driverStore, new FakeGarageStore(),
            new FakeCityStore(), new FakeHungerStore(), new FakeRoadTestStore(),
            shiftStore, wallet, new DailyStreak(clock, null),
            new OperatingZone(null, seed: 0), new Trunk(), garage,
            new CityEconomy([], CityRules.Default), new Hunger(HungerRules.Default),
            new RoadTestLedger([], seed: 0), shiftInstance, records, new JobLedger(), new DestinationPins(), new VisitLedger());
        var log = new TestLog();
        var cycle = new DayCycle(
            game, shiftInstance, records, board, online,
            new FakeProfileStore(new PlayerProfile("Ace", "Germany", "Brands", "street-1", 1)),
            new FakeRivals(), saver, new VoiceBeat(clock), StartWeek, log);
        var host = new FakeDayCycleHost();
        cycle.Attach(host);
        return new Fixture(
            cycle, host, clock, shiftInstance, records, clockStore, driverStore,
            shiftStore, online, board, log);
    }

    [Fact]
    public void Tick_SameDay_ReportsNothing()
    {
        var fixture = Build(MondayNoon);

        fixture.Cycle.Tick(fixture.Clock.UtcNow, jobInProgress: false, legActive: false);

        Assert.Empty(fixture.Host.Weeks);
        Assert.Equal(0, fixture.Host.Readouts);
        Assert.Null(fixture.Host.Page);
        // Nothing flipped, so the moment was already banked: no write per tick.
        Assert.Empty(fixture.Clocks.Saved);
    }

    [Fact]
    public void Tick_MidweekFlip_BanksTheMomentWithoutRollingTheWeek()
    {
        var fixture = Build(TuesdayLate);

        fixture.Clock.Advance(OneGameMinute);
        fixture.Cycle.Tick(fixture.Clock.UtcNow, jobInProgress: false, legActive: false);

        Assert.Empty(fixture.Host.Weeks);
        Assert.Equal(StartWeek, fixture.Cycle.DisplayWeek);
        GameClockState banked = Assert.Single(fixture.Clocks.Saved);
        Assert.Equal(StartWeek, banked.Week);
        Assert.Contains(
            fixture.Log.Infos,
            line => line.StartsWith("calendar day changed:", StringComparison.Ordinal));
    }

    [Fact]
    public void Tick_SundayMidnight_RollsTheWeekAndRepaintsThePages()
    {
        var fixture = Build(SundayLate);

        fixture.Clock.Advance(OneGameMinute);
        fixture.Cycle.Tick(fixture.Clock.UtcNow, jobInProgress: false, legActive: false);

        Assert.Equal(StartWeek + 1, fixture.Cycle.DisplayWeek);
        Assert.Equal([StartWeek + 1], fixture.Host.Weeks);
        GameClockState banked = Assert.Single(fixture.Clocks.Saved);
        Assert.Equal(StartWeek + 1, banked.Week);
    }

    [Fact]
    public void Tick_ShiftPastItsIdleWindow_OpensTheEndedPageAndBanksTheRecord()
    {
        var shift = new TaxiShift(new TaxiShiftSnapshot(
            IsStarted: true, StartedAt: MondayNoon.AddMinutes(-55),
            LastJobEnd: MondayNoon.AddMinutes(-50), JobsDone: 1, Earned: 20.0));
        var fixture = Build(MondayNoon, shift);

        fixture.Cycle.Tick(MondayNoon, jobInProgress: false, legActive: false);

        Assert.False(fixture.Shift.IsStarted);
        Assert.Contains(fixture.Shifts.Saved, snapshot => !snapshot.IsStarted);
        Assert.Equal(1, fixture.Drivers.Saved[^1].ShiftsCompleted);
        var page = Assert.IsType<ShiftEndedScreenViewModel>(fixture.Host.Page);
        Assert.Equal("Jobs completed: 1", page.JobsLine);
        Assert.Equal(1, fixture.Host.Readouts);
        // The record and the world board were refreshed before the page read
        // them, and the best shift was pushed under the confirmed name.
        Assert.Single(fixture.Board.Refreshes);
        (double earnings, _, string name) = Assert.Single(fixture.Online.ShiftPushes);
        Assert.Equal(20.0, earnings);
        Assert.Equal("Ace", name);
    }

    [Fact]
    public void Tick_ShiftPastItsIdleWindow_WithAJobLive_KeepsItOpen()
    {
        var shift = new TaxiShift(new TaxiShiftSnapshot(
            IsStarted: true, StartedAt: MondayNoon.AddMinutes(-55),
            LastJobEnd: MondayNoon.AddMinutes(-50), JobsDone: 1, Earned: 20.0));
        var fixture = Build(MondayNoon, shift);

        fixture.Cycle.Tick(MondayNoon, jobInProgress: true, legActive: true);

        Assert.True(fixture.Shift.IsStarted);
        Assert.Null(fixture.Host.Page);
        Assert.Empty(fixture.Online.ShiftPushes);
    }

    [Fact]
    public void Tick_OpenShift_RepaintsTheCountdownOncePerSecond()
    {
        var shift = new TaxiShift();
        shift.Start(MondayNoon);
        var fixture = Build(MondayNoon, shift);

        fixture.Cycle.Tick(MondayNoon, jobInProgress: false, legActive: false);
        fixture.Cycle.Tick(MondayNoon, jobInProgress: false, legActive: false);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        fixture.Cycle.Tick(fixture.Clock.UtcNow, jobInProgress: false, legActive: false);
        // A live leg paints the readouts by itself, so the guard stays quiet.
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        fixture.Cycle.Tick(fixture.Clock.UtcNow, jobInProgress: true, legActive: true);

        Assert.Equal(2, fixture.Host.Readouts);
    }

    [Fact]
    public void EndShift_ClosesTheShiftAndThePageContinuesToTheControlPage()
    {
        var fixture = Build(MondayNoon);

        fixture.Cycle.EndShift(TimeSpan.FromMinutes(TaxiShift.TimeoutMinutes));

        var page = Assert.IsType<ShiftEndedScreenViewModel>(fixture.Host.Page);
        page.ContinueCommand.Execute(null);
        Assert.Equal(1, fixture.Host.ControlPages);
    }
}
