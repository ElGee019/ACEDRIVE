using AceDrive.Core;
using AceDrive.Overlay.ViewModels;
using System.Globalization;

namespace AceDrive.Core.Tests;

/// <summary>
/// The menu page: every tile is voice-detectable and flashes the shared teal
/// for the voice beat, Jobs, Calendar, Trunk, Home and Settings then navigate,
/// the rest stay put.
/// </summary>
public sealed class MenuScreenViewModelTests
{
    public MenuScreenViewModelTests()
    {
        // The header copy follows the UI culture; tests pin en-US.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
    }

    private static readonly string[] TileLabels =
    [
        "WALLET", "JOBS", "CALENDAR",
        "TRUNK", "EMPLOYEES", "HOME",
        "HUNGER", "FILTH", "SETTINGS",
    ];

    [Fact]
    public void VoiceActions_ExposeEveryTile()
    {
        var (vm, _, _) = NewViewModel();

        foreach (string label in TileLabels)
        {
            Assert.True(vm.VoiceActions.ContainsKey(label), label);
        }
        Assert.True(vm.VoiceActions.ContainsKey("JOB"));
        Assert.Equal("WALLET", VoiceMatch.MatchLabel("open my wallet", vm.VoiceActions.Keys));
    }

    [Fact]
    public void VoiceActions_FlashEachTileTeal()
    {
        // Every tile acknowledges the tap the same way: its own flash flag up
        // for the beat, no other tile lit.
        var (vm, _, _) = NewViewModel();
        var flashes = new Dictionary<string, Func<bool>>
        {
            ["WALLET"] = () => vm.WalletFlash,
            ["JOBS"] = () => vm.JobsFlash,
            ["CALENDAR"] = () => vm.CalendarFlash,
            ["TRUNK"] = () => vm.TrunkFlash,
            ["EMPLOYEES"] = () => vm.EmployeesFlash,
            ["HOME"] = () => vm.HomeFlash,
            ["HUNGER"] = () => vm.HungerFlash,
            ["FILTH"] = () => vm.FilthFlash,
            ["SETTINGS"] = () => vm.SettingsFlash,
        };

        foreach (string label in TileLabels)
        {
            vm.VoiceActions[label].Invoke();

            foreach ((string tile, Func<bool> lit) in flashes)
            {
                Assert.Equal(tile == label, lit());
            }
        }
    }

    [Fact]
    public void VoiceWallet_GlowsThenOpensWalletPage()
    {
        bool opened = false;
        var (vm, clock, beat) = NewViewModel(wallet: () => opened = true);

        vm.VoiceActions["WALLET"].Invoke();

        Assert.True(vm.WalletFlash);
        Assert.False(opened);
        Drain(clock, beat);
        Assert.True(opened);
    }

    [Fact]
    public void VoiceJobs_GlowsThenOpensControlPage()
    {
        bool opened = false;
        var (vm, clock, beat) = NewViewModel(jobs: () => opened = true);

        vm.VoiceActions["JOBS"].Invoke();

        // The acknowledgment owns the beat: the page waits, then flips.
        Assert.True(vm.JobsFlash);
        Assert.False(opened);
        Drain(clock, beat);
        Assert.True(opened);
    }

    [Fact]
    public void VoiceCalendar_GlowsThenOpensCalendarPage()
    {
        bool opened = false;
        var (vm, clock, beat) = NewViewModel(calendar: () => opened = true);

        vm.VoiceActions["CALENDAR"].Invoke();

        Assert.True(vm.CalendarFlash);
        Assert.False(opened);
        Drain(clock, beat);
        Assert.True(opened);
    }

    [Fact]
    public void VoiceSettings_GlowsThenOpensSettingsPage()
    {
        bool opened = false;
        var (vm, clock, beat) = NewViewModel(settings: () => opened = true);

        vm.VoiceActions["SETTINGS"].Invoke();

        Assert.True(vm.SettingsFlash);
        Assert.False(opened);
        Drain(clock, beat);
        Assert.True(opened);
    }

    [Fact]
    public void VoiceTrunk_GlowsThenOpensTrunkPage()
    {
        bool opened = false;
        var (vm, clock, beat) = NewViewModel(trunk: () => opened = true);

        vm.VoiceActions["TRUNK"].Invoke();

        Assert.True(vm.TrunkFlash);
        Assert.False(opened);
        Drain(clock, beat);
        Assert.True(opened);
    }

    [Fact]
    public void VoiceHome_GlowsThenOpensHomePage()
    {
        bool home = false;
        var (vm, clock, beat) = NewViewModel(home: () => home = true);

        vm.VoiceActions["HOME"].Invoke();

        Assert.True(vm.HomeFlash);
        Assert.False(home);
        Drain(clock, beat);
        Assert.True(home);
        Assert.False(vm.HomeFlash);
    }

    [Fact]
    public void Click_ActsAtOnceAndDropsAPendingVoiceTap()
    {
        // A click is newer input: it navigates now and the waiting voice tap
        // never fires, so one utterance cannot flip the page twice.
        int opened = 0;
        var (vm, clock, beat) = NewViewModel(jobs: () => opened++);

        vm.VoiceActions["JOBS"].Invoke();
        vm.JobsCommand.Execute(null);

        Assert.Equal(1, opened);
        Drain(clock, beat);
        Assert.Equal(1, opened);
    }

    [Fact]
    public void Clock_ShowsGameTimeAndDisplayWeek()
    {
        var clock = new FakeClock();
        var vm = new MenuScreenViewModel(
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new Hunger(HungerRules.Default), new Garage([]),
            new VoiceBeat(clock),
            new GameClock(clock, clock.UtcNow),
            clock,
            3);

        Assert.Equal("12:00", vm.ClockText);
        Assert.Equal("MON / WEEK 3", vm.DayWeekText);

        // One game hour: 60 minutes at the game clock's real-time rate, with a
        // second of slack for the double division (59.999 would stay 12:59).
        clock.Advance(TimeSpan.FromSeconds((GameClock.RealSecondsPerGameMinute * 60) + 1));
        vm.Refresh();
        Assert.Equal("13:00", vm.ClockText);

        vm.DisplayWeek = 4;
        Assert.Equal("MON / WEEK 4", vm.DayWeekText);
    }

    [Fact]
    public void BatteryFill_StartsEmpty()
    {
        // A fed driver and a clean car read empty: the mask reveals the
        // gradient from the bottom, so 0 points shows no colour at all.
        var (vm, _, _) = NewViewModel();
        Assert.Equal(0.0, vm.HungerFill);
        Assert.Equal(0.0, vm.FilthFill);
    }

    [Fact]
    public void BatteryFill_WalksTheHungerSteps()
    {
        // One game hour is one of hunger's 36 points, so half the meter is
        // half the gradient: the tile clips to this fraction.
        var clock = new FakeClock();
        var hunger = new Hunger(HungerRules.Default);
        _ = hunger.AdvanceTo(clock.UtcNow, onboarded: true, hasPancakes: false);
        for (int hour = 1; hour <= 18; hour++)
        {
            _ = hunger.AdvanceTo(clock.UtcNow.AddHours(hour), onboarded: true, hasPancakes: false);
        }

        var (vm, _, _) = NewViewModel(hunger: hunger);
        Assert.Equal(0.5, vm.HungerFill);
    }

    [Fact]
    public void BatteryFill_FollowsTheCurrentCarsFilth()
    {
        // The FILTH tile reads the car driven now, over the wash rules' 25.
        var (vm, _, _) = NewViewModel(garage: Dirty(10));
        Assert.Equal(0.4, vm.FilthFill);
    }

    [Fact]
    public void BatteryFill_RepaintsWhenACarGetsDirtier()
    {
        // The fill guard must not wait for the next game minute: a settled job
        // raises the dirt, and the poll's Refresh shows it at once.
        var garage = Dirty(0);
        var (vm, _, _) = NewViewModel(garage: garage);
        garage.Current!.Filth.Add(5);
        vm.Refresh();
        Assert.Equal(0.2, vm.FilthFill);
    }

    [Fact]
    public void Reveal_TapSwapsTheBatteryForTheReading()
    {
        // Half the hunger steps reads 50 %: the tile drops the battery and
        // shows the number instead.
        var (vm, _, _) = NewViewModel(hunger: Hungry(18));

        vm.RevealHungerCommand.Execute(null);

        Assert.True(vm.IsHungerRevealed);
        Assert.False(vm.IsFilthRevealed);
        Assert.Equal("50%", vm.HungerPercentText);
    }

    [Fact]
    public void Reveal_RevertsToTheBatteryWhenTheBeatIsUp()
    {
        // Three seconds is the whole reveal: a tick short of it still shows,
        // the tick after puts the battery back.
        var (vm, clock, _) = NewViewModel();
        vm.RevealHungerCommand.Execute(null);

        clock.Advance(MenuScreenViewModel.RevealDuration - TimeSpan.FromMilliseconds(1));
        vm.UpdateReveal();
        Assert.True(vm.IsHungerRevealed);

        clock.Advance(TimeSpan.FromMilliseconds(2));
        vm.UpdateReveal();
        Assert.False(vm.IsHungerRevealed);
    }

    [Fact]
    public void Reveal_LatestTapWins()
    {
        // One slot: tapping the other meter swaps the reading over instead of
        // leaving two tiles open.
        var (vm, _, _) = NewViewModel(garage: Dirty(10));

        vm.RevealHungerCommand.Execute(null);
        vm.RevealFilthCommand.Execute(null);

        Assert.False(vm.IsHungerRevealed);
        Assert.True(vm.IsFilthRevealed);
        Assert.Equal("40%", vm.FilthPercentText);
    }

    [Fact]
    public void Reveal_ReadsOutOnAVoiceTapToo()
    {
        // A spoken label selects the tile like a click: the glow and the
        // reading land together, and no page flips.
        var (vm, _, _) = NewViewModel(garage: Dirty(25));

        vm.VoiceActions["FILTH"].Invoke();

        Assert.True(vm.FilthFlash);
        Assert.True(vm.IsFilthRevealed);
        Assert.Equal("100%", vm.FilthPercentText);
    }

    [Fact]
    public void Reveal_LeavingThePagePutsTheBatteryBack()
    {
        // The menu is cached, so a reveal must not survive a trip to another
        // page and greet the player on the way back.
        var (vm, _, _) = NewViewModel();
        vm.RevealHungerCommand.Execute(null);

        vm.JobsCommand.Execute(null);

        Assert.False(vm.IsHungerRevealed);
    }

    [Fact]
    public void Reveal_PaintsReadingsPastEightyFiveRed()
    {
        // 31 of 36 steps is 86 %: inside the danger band. 30 steps is 83 %,
        // which stays calm.
        var (hot, _, _) = NewViewModel(hunger: Hungry(31));
        Assert.Equal("86%", hot.HungerPercentText);
        Assert.True(hot.HungerPercentHot);

        var (calm, _, _) = NewViewModel(hunger: Hungry(30));
        Assert.Equal("83%", calm.HungerPercentText);
        Assert.False(calm.HungerPercentHot);
    }

    [Fact]
    public void Reveal_BandIsStrictlyOverEightyFive()
    {
        // 17 of 20 steps is exactly 85 %: still calm, because the rule reads
        // "over 85". One step more crosses it.
        var rules = new HungerRules(MaxHunger: 20, PerHour: 1, PancakeThreshold: 14);
        var (edge, _, _) = NewViewModel(hunger: Hungry(17, rules));
        Assert.Equal("85%", edge.HungerPercentText);
        Assert.False(edge.HungerPercentHot);

        var (over, _, _) = NewViewModel(hunger: Hungry(18, rules));
        Assert.Equal("90%", over.HungerPercentText);
        Assert.True(over.HungerPercentHot);
    }

    [Fact]
    public void Reveal_TracksTheFilthReadingOnItsOwnCap()
    {
        // 22 of the car-wash rules' 25 steps is 88 %, 21 steps is 84 %.
        var (hot, _, _) = NewViewModel(garage: Dirty(22));
        Assert.Equal("88%", hot.FilthPercentText);
        Assert.True(hot.FilthPercentHot);

        var (calm, _, _) = NewViewModel(garage: Dirty(21));
        Assert.Equal("84%", calm.FilthPercentText);
        Assert.False(calm.FilthPercentHot);
    }

    [Fact]
    public void Reveal_ShowsALiveReading()
    {
        // A wash while the number is up must move it: the poll's Refresh
        // feeds the same text the reveal paints.
        var garage = Dirty(20);
        var (vm, _, _) = NewViewModel(garage: garage);
        vm.RevealFilthCommand.Execute(null);
        Assert.Equal("80%", vm.FilthPercentText);

        garage.Current!.Filth.Wash();
        vm.Refresh();

        Assert.True(vm.IsFilthRevealed);
        Assert.Equal("0%", vm.FilthPercentText);
        Assert.False(vm.FilthPercentHot);
    }

    private static Garage Dirty(int filth)
    {
        var car = new CarSpec("ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Passengers: 1);
        var garage = new Garage([car]);
        garage.Add(car, week: 1).Filth.Add(filth);
        return garage;
    }

    // A hunger reading read back from a save, the way the app resumes one.
    private static Hunger Hungry(int points, HungerRules? rules = null)
    {
        HungerRules table = rules ?? HungerRules.Default;
        return new Hunger(table, new HungerState(points, ThresholdTriggered: false));
    }

    // What the 10 Hz poll does in the app: wall time passes and the beat drains.
    private static void Drain(FakeClock clock, VoiceBeat beat)
    {
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
    }

    private static (MenuScreenViewModel Vm, FakeClock Clock, VoiceBeat Beat) NewViewModel(
        Action? wallet = null, Action? jobs = null, Action? calendar = null, Action? trunk = null,
        Action? settings = null, Action? home = null,
        Hunger? hunger = null, Garage? garage = null)
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        MenuScreenViewModel vm = new(
            new RelayCommand(wallet ?? (() => { })),
            new RelayCommand(jobs ?? (() => { })),
            new RelayCommand(calendar ?? (() => { })),
            new RelayCommand(trunk ?? (() => { })),
            new RelayCommand(settings ?? (() => { })),
            new RelayCommand(home ?? (() => { })),
            hunger ?? new Hunger(HungerRules.Default),
            garage ?? new Garage([]),
            beat,
            new GameClock(clock, clock.UtcNow),
            clock,
            1);
        return (vm, clock, beat);
    }
}
