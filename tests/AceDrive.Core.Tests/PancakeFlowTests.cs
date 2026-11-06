using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// Friday's pancake visit (PORTPLAN items 26/52): the briefing the calendar row
/// opens, the drive to grandma's house, the plate that comes off the meter and
/// the stack that keeps the appetite's one-shot event alive. The coordinator is
/// a fake host here, so each test reads only the flow's own decisions: which
/// page it presented, what it pinned, which saves it ran and what the stamp is.
/// </summary>
public sealed class PancakeFlowTests
{
    // Friday 16 October 2026, noon: inside the strip's 8am-8pm window. The game
    // clock's seeded career starts on Saturday 10 October, so this is the first
    // Friday it can reach.
    private static readonly DateTime FridayNoon = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    private sealed record Fixture(
        PancakeFlow Flow, FakePancakeHost Host, FakeTelemetryHud Hud, FakeClock Clock,
        Hunger Hunger, Trunk Trunk, WeeklyEventLedger Weekly, FakeDriverStore Drivers,
        FakeHungerStore HungerStore, TestLog Log);

    public PancakeFlowTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static Fixture Build(
        DateTime? start = null, int hunger = 20, bool stamped = false,
        AddressLocation? home = null, bool hasHome = true)
    {
        DateTime moment = start ?? FridayNoon;
        var clock = new FakeClock { Now = moment };
        var game = new GameClock(clock, moment);
        var beat = new VoiceBeat(clock);
        var log = new TestLog();
        var trunk = new Trunk();
        var appetite = new Hunger(
            HungerRules.Default, new HungerState(hunger, ThresholdTriggered: false));
        var hud = new FakeTelemetryHud();
        var rivals = new FakeRivals();
        var driverStore = new FakeDriverStore();
        var hungerStore = new FakeHungerStore();
        var weekly = new WeeklyEventLedger();
        AddressLocation door = home ?? new AddressLocation("Test Street", 4, HouseSide.Right);
        if (stamped)
        {
            weekly.MarkTaken(ControlCalendar.Pancakes, moment);
        }
        var saver = new GameStateSaver(
            clock, game, new FakeGameClockStore(), driverStore, new FakeGarageStore(),
            new FakeCityStore(), hungerStore, new FakeRoadTestStore(),
            new FakeShiftStore(), new DriverWallet(), new DailyStreak(clock, null),
            new OperatingZone(null, seed: 0, AddressMap.Default), trunk,
            new Garage([]), new CityEconomy([], CityRules.Default), appetite,
            new RoadTestLedger([], seed: 0), new TaxiShift(), new ShiftRecord(null),
            new JobLedger(), new VisitLedger(),
            new ValuationLedger(), new WhereIsWillyLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()),
            weekly, new CarePackageLedger());
        var flow = new PancakeFlow(
            game, () => hasHome ? door : null, trunk, appetite, StoreCatalog.Default,
            hud, saver, rivals, weekly, beat, log);
        var host = new FakePancakeHost();
        flow.Attach(host);
        return new Fixture(
            flow, host, hud, clock, appetite, trunk, weekly, driverStore, hungerStore, log);
    }

    [Fact]
    public void Begin_OnFriday_PresentsTheBriefing()
    {
        var f = Build();

        f.Flow.Begin();

        var page = Assert.IsType<PancakeBriefingScreenViewModel>(f.Host.Page);
        Assert.Equal(PancakePhase.Briefing, f.Flow.Phase);
        Assert.Contains("Test Street", page.VenueLine);
        Assert.Contains("HUNGER 20/36", page.HungerLine);
        Assert.Contains(
            f.Log.Infos, line => line.StartsWith("pancake friday opened:", StringComparison.Ordinal));
    }

    [Fact]
    public void Begin_OnAnotherDay_Refuses()
    {
        // Saturday is the Touristenfahrten row, so the flow refuses even when a
        // cached page or a voice tap reaches it.
        var f = Build(start: FridayNoon.AddDays(1));

        f.Flow.Begin();

        Assert.Null(f.Host.Page);
        Assert.Equal(PancakePhase.Idle, f.Flow.Phase);
        Assert.Contains(
            f.Log.Infos, line => line.StartsWith("pancake visit ignored: today", StringComparison.Ordinal));
    }

    [Fact]
    public void Begin_OutsideTheWindow_Refuses()
    {
        var f = Build(start: FridayNoon.Date.AddHours(21));

        f.Flow.Begin();

        Assert.Null(f.Host.Page);
        Assert.Contains(
            f.Log.Infos, line => line.StartsWith("pancake visit ignored: outside", StringComparison.Ordinal));
    }

    [Fact]
    public void Begin_OnceTheWeekIsStamped_Refuses()
    {
        var f = Build(stamped: true);

        f.Flow.Begin();

        Assert.Null(f.Host.Page);
        Assert.Contains(
            f.Log.Infos, line => line.StartsWith("pancake visit ignored: this week", StringComparison.Ordinal));
    }

    [Fact]
    public void Begin_WithoutAHomeAddress_Refuses()
    {
        // A save that names no resolvable street must not pin a house that does
        // not exist, exactly like the introduction's pancake leg.
        var f = Build(hasHome: false);

        f.Flow.Begin();

        Assert.Null(f.Host.Page);
        Assert.Equal(PancakePhase.Idle, f.Flow.Phase);
        Assert.Single(f.Log.Warnings, line =>
            line.StartsWith("pancake visit not opened:", StringComparison.Ordinal));
    }

    [Fact]
    public void Drive_PinsGrandmasHouseAndHidesThePanel()
    {
        var f = Build();
        f.Flow.Begin();
        var page = Assert.IsType<PancakeBriefingScreenViewModel>(f.Host.Page);

        page.StartCommand.Execute(null);

        Assert.Equal(PancakePhase.Driving, f.Flow.Phase);
        Assert.Equal(1, f.Host.HidPanel);
        Assert.NotNull(f.Hud.Destination);
        Assert.Equal("Test Street", f.Hud.Destination!.Address.StreetName);
        Assert.Equal("HudPancakeDrive", f.Hud.Copy?.DriveKey);
        Assert.Equal("HudPancakeArrived", f.Hud.Copy?.ArrivedKey);
        Assert.Equal("HudPancakes", f.Hud.Copy?.ActionKey);
    }

    [Fact]
    public void Back_BeforeTheDrive_HandsTheCalendarBackAndKeepsTheWeekOpen()
    {
        var f = Build();
        f.Flow.Begin();
        var page = Assert.IsType<PancakeBriefingScreenViewModel>(f.Host.Page);

        page.BackCommand.Execute(null);

        Assert.Equal(PancakePhase.Idle, f.Flow.Phase);
        Assert.Equal(1, f.Host.CalendarShown);
        Assert.False(f.Weekly.TakenOn(ControlCalendar.Pancakes, f.Clock.UtcNow));
        Assert.Null(f.Hud.Destination);
    }

    [Fact]
    public void TryEnter_AtHerDoor_ServesThePlatePacksTheStackAndStampsTheWeek()
    {
        var f = Build(hunger: 20);
        f.Flow.Begin();
        ((PancakeBriefingScreenViewModel)f.Host.Page!).StartCommand.Execute(null);
        f.Hud.AtDestination = true;

        bool consumed = f.Flow.TryEnter();

        Assert.True(consumed);
        // The plate comes off the catalogue row's hunger value, and she packs a
        // stack for the road: that stack is what the one-shot event asks for.
        Assert.Equal(6, f.Hunger.Value);
        Assert.True(PancakeVisit.IsCarried(f.Trunk));
        // The week is stamped on the visit, not on the page, and both files are
        // banked together: a kill after the meal must not reopen the day.
        Assert.True(f.Weekly.TakenOn(ControlCalendar.Pancakes, f.Clock.UtcNow));
        Assert.Single(f.Drivers.Saved);
        Assert.Single(f.HungerStore.Saved);
        Assert.Equal(6, f.HungerStore.Saved[0].Hunger);
        // The companion releases the door and the receipt prints the numbers.
        Assert.Null(f.Hud.Destination);
        var receipt = Assert.IsType<PancakeReceiptScreenViewModel>(f.Host.Page);
        Assert.Equal(PancakePhase.Served, f.Flow.Phase);
        Assert.Contains("-14 (6/36)", receipt.MealLine);
        Assert.NotEmpty(receipt.PackedLine);
    }

    [Fact]
    public void TryEnter_NotParkedAtTheDoor_FallsThrough()
    {
        var f = Build();
        f.Flow.Begin();
        ((PancakeBriefingScreenViewModel)f.Host.Page!).StartCommand.Execute(null);
        f.Hud.AtDestination = false;

        Assert.False(f.Flow.TryEnter());
        Assert.Equal(20, f.Hunger.Value);
        Assert.False(PancakeVisit.IsCarried(f.Trunk));
        Assert.False(f.Weekly.TakenOn(ControlCalendar.Pancakes, f.Clock.UtcNow));
    }

    [Fact]
    public void TryEnter_AtAnotherDoor_FallsThrough()
    {
        // A tap somewhere else in town is never swallowed: the door answers like
        // any other, and the visit keeps its own pinned house.
        var f = Build();
        f.Flow.Begin();
        ((PancakeBriefingScreenViewModel)f.Host.Page!).StartCommand.Execute(null);
        f.Hud.SetDestination(new PointOfInterest(
            "shop", "Kiosk", new AddressLocation("Other Street", 9, HouseSide.Left)));
        f.Hud.AtDestination = true;

        Assert.False(f.Flow.TryEnter());
        Assert.Equal(20, f.Hunger.Value);
        Assert.Equal(PancakePhase.Driving, f.Flow.Phase);
    }

    [Fact]
    public void TryEnter_BeforeTheDrive_DoesNothing()
    {
        var f = Build();
        f.Flow.Begin();
        f.Hud.AtDestination = true;

        Assert.False(f.Flow.TryEnter());
        Assert.Equal(PancakePhase.Briefing, f.Flow.Phase);
    }

    [Fact]
    public void Continue_AfterTheReceipt_ClosesOntoTheMenu()
    {
        var f = Build();
        f.Flow.Begin();
        ((PancakeBriefingScreenViewModel)f.Host.Page!).StartCommand.Execute(null);
        f.Hud.AtDestination = true;
        _ = f.Flow.TryEnter();
        var receipt = Assert.IsType<PancakeReceiptScreenViewModel>(f.Host.Page);

        receipt.ContinueCommand.Execute(null);

        Assert.Equal(PancakePhase.Idle, f.Flow.Phase);
        Assert.Equal(1, f.Host.MenuShown);
    }

    [Fact]
    public void Begin_WhileTheReceiptIsUp_Refuses()
    {
        // The served week is stamped, so the row refuses before it can present
        // anything: a second plate on the same day is what the stamp exists for.
        var f = Build();
        f.Flow.Begin();
        ((PancakeBriefingScreenViewModel)f.Host.Page!).StartCommand.Execute(null);
        f.Hud.AtDestination = true;
        _ = f.Flow.TryEnter();
        int presented = f.Host.Presented;

        f.Flow.Begin();

        Assert.Equal(presented, f.Host.Presented);
        Assert.Contains(
            f.Log.Infos, line => line.StartsWith("pancake visit ignored: this week", StringComparison.Ordinal));
    }

    [Fact]
    public void Begin_WhileDriving_RepinsTheDoorInsteadOfDrawingAgain()
    {
        // The Friday row is how a stalled guide comes back: the live leg re-pins
        // the same house and hides the panel, with no second page and no second
        // draw, so the player is one tap from driving again.
        var f = Build();
        f.Flow.Begin();
        ((PancakeBriefingScreenViewModel)f.Host.Page!).StartCommand.Execute(null);
        // A stalled leg: the guide lost its door between two looks.
        f.Hud.SetDestination(null);
        int presented = f.Host.Presented;

        f.Flow.Begin();

        Assert.Equal(presented, f.Host.Presented);
        Assert.Equal(PancakePhase.Driving, f.Flow.Phase);
        Assert.Equal("Test Street", f.Hud.Destination!.Address.StreetName);
        Assert.Equal(2, f.Host.HidPanel);
    }
}
