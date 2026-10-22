using AceDrive.Core;
using AceDrive.Overlay.ViewModels;
using System.Globalization;

namespace AceDrive.Core.Tests;

/// <summary>
/// The menu page: every tile is voice-detectable and flashes the shared teal
/// for the voice beat, Jobs, Calendar and Settings then navigate, the rest
/// stay put.
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
    public void VoicePlaceholder_GlowsAndStaysOnMenu()
    {
        bool jobs = false;
        bool calendar = false;
        bool settings = false;
        var (vm, clock, _) = NewViewModel(
            () => jobs = true, () => calendar = true, () => settings = true);

        vm.VoiceActions["HOME"].Invoke();

        Assert.True(vm.HomeFlash);
        Assert.False(jobs);
        Assert.False(calendar);
        Assert.False(settings);

        // The glow is one shared beat, then the poll clears it.
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        vm.UpdateFlash();
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
            new VoiceBeat(clock),
            new GameClock(clock, clock.UtcNow),
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

    // What the 10 Hz poll does in the app: wall time passes and the beat drains.
    private static void Drain(FakeClock clock, VoiceBeat beat)
    {
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
    }

    private static (MenuScreenViewModel Vm, FakeClock Clock, VoiceBeat Beat) NewViewModel(
        Action? jobs = null, Action? calendar = null, Action? settings = null)
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        MenuScreenViewModel vm = new(
            new RelayCommand(jobs ?? (() => { })),
            new RelayCommand(calendar ?? (() => { })),
            new RelayCommand(settings ?? (() => { })),
            beat,
            new GameClock(clock, clock.UtcNow),
            1);
        return (vm, clock, beat);
    }
}
