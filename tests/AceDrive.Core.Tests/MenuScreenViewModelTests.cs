using AceDrive.Core;
using AceDrive.Overlay.ViewModels;
using System.Globalization;

namespace AceDrive.Core.Tests;

/// <summary>The menu page: every tile is voice-detectable, Jobs, Calendar and Settings navigate.</summary>
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
        var vm = NewViewModel();

        foreach (string label in TileLabels)
        {
            Assert.True(vm.VoiceActions.ContainsKey(label), label);
        }
        Assert.True(vm.VoiceActions.ContainsKey("JOB"));
        Assert.Equal("WALLET", VoiceMatch.MatchLabel("open my wallet", vm.VoiceActions.Keys));
    }

    [Fact]
    public void VoiceJobs_OpensControlPage()
    {
        bool opened = false;
        var vm = NewViewModel(jobs: () => opened = true);

        vm.VoiceActions["JOBS"].Invoke();

        Assert.True(opened);
    }

    [Fact]
    public void VoiceCalendar_OpensCalendarPage()
    {
        bool opened = false;
        var vm = NewViewModel(calendar: () => opened = true);

        vm.VoiceActions["CALENDAR"].Invoke();

        Assert.True(opened);
    }

    [Fact]
    public void VoiceSettings_OpensSettingsPage()
    {
        bool opened = false;
        var vm = NewViewModel(settings: () => opened = true);

        vm.VoiceActions["SETTINGS"].Invoke();

        Assert.True(opened);
    }

    [Fact]
    public void VoicePlaceholder_StaysOnMenu()
    {
        bool jobs = false;
        bool calendar = false;
        bool settings = false;
        var vm = NewViewModel(() => jobs = true, () => calendar = true, () => settings = true);

        vm.VoiceActions["HOME"].Invoke();

        Assert.False(jobs);
        Assert.False(calendar);
        Assert.False(settings);
    }

    [Fact]
    public void Clock_ShowsGameTimeAndDisplayWeek()
    {
        var clock = new FakeClock();
        var vm = new MenuScreenViewModel(
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
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

    private static MenuScreenViewModel NewViewModel(
        Action? jobs = null, Action? calendar = null, Action? settings = null)
    {
        var clock = new FakeClock();
        return new(
            new RelayCommand(jobs ?? (() => { })),
            new RelayCommand(calendar ?? (() => { })),
            new RelayCommand(settings ?? (() => { })),
            new GameClock(clock, clock.UtcNow),
            1);
    }
}
