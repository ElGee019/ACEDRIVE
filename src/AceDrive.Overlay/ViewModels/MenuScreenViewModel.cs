using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The menu page: the entry a call (hotkey or wake word) opens now. The Jobs
/// tile leads into the career — the main control page with every job door —
/// and the Calendar tile opens the weekly strip, by click or by voice. Every
/// tile carries a voice entry; Jobs opens the control page, Calendar the
/// calendar page and Settings the first-run preferences form, and the other
/// six acknowledge the tap and stay put until their features land. Voice taps
/// glow the tile teal for the shared <see cref="VoiceBeat"/>; the navigating
/// ones wait out that beat before the flip, so the acknowledgment is visible,
/// while mouse clicks act at once like every other page. Marked as a jobs-side
/// screen so the onboarding gate treats it like every other post-introduction
/// page. The header repeats the design's clock (ontwerpdesign.psd): the game
/// time over the abbreviated weekday and the display week.
/// </summary>
public sealed class MenuScreenViewModel : ViewModelBase, IJobsScreen, IVoiceScreen
{
    // The words are baked into the tile artwork, so the spoken keys are the
    // drawn English labels, not resx copy: a translated page would need new
    // artwork and its own keywords. Listed in the view's row order.
    // The drawn English labels double as the flash targets, so the tile names
    // live once: the array feeds the voice table and each check below.
    private const string WalletTile = "WALLET";
    private const string JobsTile = "JOBS";
    private const string CalendarTile = "CALENDAR";
    private const string TrunkTile = "TRUNK";
    private const string EmployeesTile = "EMPLOYEES";
    private const string HomeTile = "HOME";
    private const string HungerTile = "HUNGER";
    private const string FilthTile = "FILTH";
    private const string SettingsTile = "SETTINGS";

    private static readonly string[] TileLabels =
    [
        WalletTile, JobsTile, CalendarTile,
        TrunkTile, EmployeesTile, HomeTile,
        HungerTile, FilthTile, SettingsTile,
    ];

    // "job" covers the mic's singular reading; MatchLabel still answers any
    // phrase containing the whole label.
    private static readonly string[] JobsKeywords = ["JOBS", "JOB"];

    private readonly VoiceBeat _beat;
    private readonly GameClock _game;
    private int _week;
    private int _lastMinute = -1;
    private int _lastWeek = -1;

    // Voice acknowledgment state: the tile currently glowing and the beat's
    // deadline on the injected clock, never a tick count.
    private string? _flashTile;
    private DateTime _flashUntil;

    public MenuScreenViewModel(
        ICommand jobs, ICommand calendar, ICommand settings,
        VoiceBeat beat, GameClock game, int displayWeek)
    {
        _beat = beat;
        _game = game;
        _week = displayWeek;
        // The tile commands wrap the injected ones so a click is newer input
        // than a pending voice tap: it drops the deferred navigation instead
        // of letting one utterance flip the page after the player moved on.
        JobsCommand = new RelayCommand(() => Tap(jobs));
        CalendarCommand = new RelayCommand(() => Tap(calendar));
        SettingsCommand = new RelayCommand(() => Tap(settings));
        var actions = new Dictionary<string, Action>();
        foreach (string label in TileLabels)
        {
            // Every tile answers its drawn label. The three navigating ones
            // are rebound below; the six features still to land acknowledge
            // the voice tap with the teal glow and stay on the page.
            actions[label] = () => Flash(label);
        }
        foreach (string keyword in JobsKeywords)
        {
            actions[keyword] = OpenJobs;
        }
        actions[CalendarTile] = OpenCalendar;
        actions[SettingsTile] = OpenSettings;
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

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool WalletFlash { get => _walletFlash; private set => SetField(ref _walletFlash, value); }
    private bool _walletFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool JobsFlash { get => _jobsFlash; private set => SetField(ref _jobsFlash, value); }
    private bool _jobsFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool CalendarFlash { get => _calendarFlash; private set => SetField(ref _calendarFlash, value); }
    private bool _calendarFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool TrunkFlash { get => _trunkFlash; private set => SetField(ref _trunkFlash, value); }
    private bool _trunkFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool EmployeesFlash { get => _employeesFlash; private set => SetField(ref _employeesFlash, value); }
    private bool _employeesFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool HomeFlash { get => _homeFlash; private set => SetField(ref _homeFlash, value); }
    private bool _homeFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool HungerFlash { get => _hungerFlash; private set => SetField(ref _hungerFlash, value); }
    private bool _hungerFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool FilthFlash { get => _filthFlash; private set => SetField(ref _filthFlash, value); }
    private bool _filthFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool SettingsFlash { get => _settingsFlash; private set => SetField(ref _settingsFlash, value); }
    private bool _settingsFlash;

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

    // Voice taps glow first and wait out the shared beat, so the
    // acknowledgment is visible before the page flips.
    private void OpenJobs() => FireVoice(JobsTile, JobsCommand);

    private void OpenCalendar() => FireVoice(CalendarTile, CalendarCommand);

    private void OpenSettings() => FireVoice(SettingsTile, SettingsCommand);

    // A click bypasses the beat (companion parity): it acts at once, and it
    // supersedes a voice tap still waiting, so one utterance cannot flip the
    // page after the player already moved on.
    private void Tap(ICommand command)
    {
        _beat.Cancel();
        ClearFlash();
        command.Execute(null);
    }

    private void FireVoice(string tile, ICommand command)
    {
        Flash(tile);
        _beat.Defer(() =>
        {
            // Clear on the way out so the cached menu never shows a stale
            // glow when the player comes back (bank-page precedent).
            ClearFlash();
            command.Execute(null);
        });
    }

    private void Flash(string tile)
    {
        WalletFlash = tile == WalletTile;
        JobsFlash = tile == JobsTile;
        CalendarFlash = tile == CalendarTile;
        TrunkFlash = tile == TrunkTile;
        EmployeesFlash = tile == EmployeesTile;
        HomeFlash = tile == HomeTile;
        HungerFlash = tile == HungerTile;
        FilthFlash = tile == FilthTile;
        SettingsFlash = tile == SettingsTile;
        _flashTile = tile;
        _flashUntil = _beat.Deadline();
    }

    /// <summary>
    /// Clears the expired voice-flash. Called from the 10 Hz poll (the refresh
    /// guard would swallow a same-minute expiry): one DateTime compare per
    /// tick, allocations only on the glow transition.
    /// </summary>
    public void UpdateFlash()
    {
        if (_flashTile is not null && _beat.IsDue(_flashUntil))
        {
            ClearFlash();
        }
    }

    // Glow off on every tile at once: the deferred navigation clears here on
    // the way out, the poll clears here when the beat expires.
    private void ClearFlash()
    {
        _flashTile = null;
        WalletFlash = false;
        JobsFlash = false;
        CalendarFlash = false;
        TrunkFlash = false;
        EmployeesFlash = false;
        HomeFlash = false;
        HungerFlash = false;
        FilthFlash = false;
        SettingsFlash = false;
    }
}
