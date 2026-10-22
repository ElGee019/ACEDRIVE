using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The menu page: the entry a call (hotkey or wake word) opens now. The Jobs
/// tile leads into the career — the main control page with every job door —
/// and the Calendar tile opens the weekly strip, by click or by voice. Every
/// tile carries a voice entry; Jobs opens the control page, Calendar the
/// calendar page and Settings the first-run preferences form, the other six
/// answer with a no-op until their features land. Marked as a jobs-side screen
/// so the onboarding gate treats it like every other post-introduction page.
/// The header repeats the design's clock (ontwerpdesign.psd): the game time
/// over the abbreviated weekday and the display week.
/// </summary>
public sealed class MenuScreenViewModel : ViewModelBase, IJobsScreen, IVoiceScreen
{
    // The words are baked into the tile artwork, so the spoken keys are the
    // drawn English labels, not resx copy: a translated page would need new
    // artwork and its own keywords. Listed in the view's row order.
    private static readonly string[] TileLabels =
    [
        "WALLET", "JOBS", "CALENDAR",
        "TRUNK", "EMPLOYEES", "HOME",
        "HUNGER", "FILTH", "SETTINGS",
    ];

    // "job" covers the mic's singular reading; MatchLabel still answers any
    // phrase containing the whole label.
    private static readonly string[] JobsKeywords = ["JOBS", "JOB"];

    private readonly GameClock _game;
    private int _week;
    private int _lastMinute = -1;
    private int _lastWeek = -1;

    public MenuScreenViewModel(
        ICommand jobs, ICommand calendar, ICommand settings, GameClock game, int displayWeek)
    {
        JobsCommand = jobs;
        CalendarCommand = calendar;
        SettingsCommand = settings;
        _game = game;
        _week = displayWeek;
        var actions = new Dictionary<string, Action>();
        foreach (string label in TileLabels)
        {
            actions[label] = NoAction;
        }
        foreach (string keyword in JobsKeywords)
        {
            actions[keyword] = OpenJobs;
        }
        actions["CALENDAR"] = OpenCalendar;
        actions["SETTINGS"] = OpenSettings;
        VoiceActions = actions;
        Refresh();
    }

    /// <summary>Opens the control page (all job doors).</summary>
    public ICommand JobsCommand { get; }

    /// <summary>Opens the calendar page (the weekly strip).</summary>
    public ICommand CalendarCommand { get; }

    /// <summary>The menu's settings tile: the first-run preferences form
    /// until a fuller settings screen lands.</summary>
    public ICommand SettingsCommand { get; }

    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    /// <summary>Design clock line, 24-hour and culture-invariant (Tourney's
    /// digits are the design's face; the locale never reshapes "21:45").</summary>
    public string ClockText { get => _clockText; private set => SetField(ref _clockText, value); }
    private string _clockText = "";

    /// <summary>Weekday + week line under the clock ("THU / WEEK 1").</summary>
    public string DayWeekText { get => _dayWeekText; private set => SetField(ref _dayWeekText, value); }
    private string _dayWeekText = "";

    /// <summary>Display week from the screen controller: the menu is built
    /// once per session, so the Sunday rollover pushes the new number in.</summary>
    public int DisplayWeek
    {
        get => _week;
        set
        {
            if (SetField(ref _week, value)) Refresh();
        }
    }

    /// <summary>
    /// Repaints the header. Called from the 10 Hz poll; the minute/week guard
    /// keeps it allocation-free between game minutes (hot-path discipline).
    /// </summary>
    public void Refresh()
    {
        DateTime now = _game.GameNow;
        int minute = now.Hour * 60 + now.Minute;
        if (minute == _lastMinute && _week == _lastWeek) return;
        _lastMinute = minute;
        _lastWeek = _week;
        ClockText = GameClockText.Time(now);
        DayWeekText = GameClockText.DayWeek(_game.Today, _week);
    }

    private void OpenJobs() => JobsCommand.Execute(null);

    private void OpenCalendar() => CalendarCommand.Execute(null);

    private void OpenSettings() => SettingsCommand.Execute(null);

    // The six tiles whose pages arrive later still register their words so
    // the mic routes them now instead of treating the page as closed.
    private static void NoAction()
    {
    }
}
