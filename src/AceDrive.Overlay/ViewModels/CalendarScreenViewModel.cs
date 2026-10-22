using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Calendar page: the weekly strip moved off the jobs page, opened from the
/// menu's CALENDAR tile. The header repeats the menu's clock (time over the
/// abbreviated weekday and display week), then the Monday-first day rows
/// render their event. Day rows stay tappable like the
/// Unity day entries: the highlighted (current game) day wears the teal
/// outline, and a tap inside its 8am-8pm gate glows the text teal for one
/// beat and runs the host action; a tap outside it glows the same beat first,
/// then swaps the text for the COME BACK warning for three real seconds and
/// restores (Unity RestoreButtonText parity). The Tuesday road-test button
/// names the week's car inside its outline (PORTPLAN item 49).
/// The shared <see cref="VoiceBeat"/> times the teal acknowledgment glow;
/// game time flows through <see cref="GameClock"/> (clock header + calendar
/// highlight), never tick counts.
/// </summary>
public sealed class CalendarScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    // resx keys for Day 1=Sunday..7=Saturday: indexed by weekday number, so
    // the labels are independent of the strip's Monday-first display order.
    private static readonly string[] DaySuffixes = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    private readonly VoiceBeat _beat;
    private readonly GameClock _game;
    private readonly IClock _clock;
    private readonly RoadTestLedger _roads;
    private readonly ICommand _openDayAction;
    private readonly ICommand _backCommand;
    private int _lastMinute = -1;
    private string? _lastPick;
    private int _lastWeek = -1;
    private int _week;

    public CalendarScreenViewModel(
        VoiceBeat beat, GameClock game, IClock clock, RoadTestLedger roads,
        CalendarStamp stamp, ICommand openDay, ICommand back)
    {
        _beat = beat;
        _game = game;
        _clock = clock;
        _roads = roads;
        // The stamp is the persisted display week (PORTPLAN item 51); the
        // year stays data-only, never shown in the overlay.
        _week = stamp.Week;
        _openDayAction = openDay;
        _backCommand = back;
        // The strip's one tap entry (click or voice): the page itself gates
        // taps, so off-gate rows answer with the warning instead of muting.
        DayCommand = new ParameterizedCommand<int>(TapDay);
        BackCommand = new RelayCommand(Back);
        Refresh();
    }

    public string ClockText { get => _clockText; private set => SetField(ref _clockText, value); }
    private string _clockText = "";

    /// <summary>Weekday + week line under the clock, same copy as the menu.</summary>
    public string DayWeekText { get => _dayWeekText; private set => SetField(ref _dayWeekText, value); }
    private string _dayWeekText = "";

    /// <summary>
    /// Display week in the clock header. The week rolls on the Sunday-midnight
    /// crossing (pushed by the screen controller, which owns the boundary);
    /// starts as the persisted stamp week.
    /// </summary>
    public int DisplayWeek { get => _week; set => SetField(ref _week, value); }

    public IReadOnlyList<CalendarRow> Days { get => _days; private set => SetField(ref _days, value); }
    private IReadOnlyList<CalendarRow> _days = [];

    /// <summary>The day rows' single tap entry: gates the tap itself, so a
    /// click is never swallowed by a disabled button (see <c>TapDay</c>).</summary>
    public ICommand DayCommand { get; }

    /// <summary>The page's way back to the menu; a voice tap glows first and
    /// waits out the shared beat like every navigating button.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    /// <summary>Highlighted calendar day, 1=Sunday..7=Saturday: the row the
    /// teal outline marks. Its gate opens on the clock, never on CanExecute.</summary>
    public int Today => _game.Today;

    // Voice-activation acknowledgment: the tapped row's text glows teal for
    // one shared beat. Clock-driven expiry via UpdateFlash so FakeClock tests
    // it deterministically.
    private int _flashIndex = -1;
    private DateTime _flashUntil;

    // Gate warning slot (latest tap wins, like VoiceBeat): the tapped row's
    // text carries the warning until its deadline, one slot for the whole
    // strip. Three real seconds, Unity RestoreButtonText's WaitForSeconds(3f).
    private const double GateWarningSeconds = 3;
    private int _warnIndex = -1;
    private string _warnText = string.Empty;
    private DateTime _warnUntil;

    /// <summary>Spoken text -&gt; action. Every row answers to its event label
    /// (the road test to the short label only); only the highlighted row
    /// registers, and BACK navigates. Past days never answer to voice. Every
    /// tap — click or voice — is decided by the day's gate (see
    /// <c>TapDay</c>).</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// Repaints the date header and the strip. Called from the 10 Hz poll; the
    /// minute guard keeps it allocation-free between game minutes (hot-path
    /// discipline). A game-day flip repaints too: the highlight moves even
    /// when the flip lands inside the same wall minute. A road-test switch
    /// (manual tap or week flip) repaints the same way: the pick joins the
    /// guard, otherwise the minute check would swallow it. Same for the
    /// display week, which the controller pushes on the Sunday boundary.
    /// </summary>
    public void Refresh()
    {
        DateTime now = _game.GameNow;
        string? pick = _roads.Current?.Id;
        if (now.Hour * 60 + now.Minute == _lastMinute
            && pick == _lastPick
            && _week == _lastWeek)
        {
            return;
        }
        _lastMinute = now.Hour * 60 + now.Minute;
        _lastPick = pick;
        _lastWeek = _week;

        int today = ControlCalendar.ToDayOfWeek(now);
        // The year stays data-only (PORTPLAN item 51 persist): irrelevant in
        // the overlay context, so the header shows the display week alone.
        ClockText = GameClockText.Time(now);
        DayWeekText = GameClockText.DayWeek(today, _week);
        Days = BuildDays(now, today);
        RebuildVoice();
    }

    private CalendarRow[] BuildDays(DateTime now, int today)
    {
        var rows = new CalendarRow[ControlCalendar.Week.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            CalendarDay day = ControlCalendar.Week[i];
            bool isToday = day.DayOfWeek == today;
            rows[i] = new CalendarRow(
                Strings.Get("Day" + DaySuffixes[day.DayOfWeek - 1]),
                DisplayLabel(i, day),
                day.DayOfWeek,
                isToday,
                isToday && ControlCalendar.IsEventWindow(now.Hour));
        }
        return rows;
    }

    // An active gate warning outranks the event label until its deadline, so
    // a mid-warning repaint (game minute, road-test switch) keeps the hint.
    private string DisplayLabel(int index, CalendarDay day) =>
        index == _warnIndex && _clock.UtcNow < _warnUntil ? _warnText : EventLabel(day);

    // Button copy per day: the resx label, except the road-test row names the
    // running car inside the button ("Road test: 1990 MAZDA MX-5 NA"). The car
    // suffix is data, all caps; the resx prefix keeps its localization.
    // Empty when the row has no event or the pool failed to load.
    private string EventLabel(CalendarDay day)
    {
        if (day.EventId is null)
        {
            return string.Empty;
        }
        string label = Strings.Get("Event" + day.EventId);
        string detail = RoadTestDetail(day);
        return detail.Length == 0 ? label : label + ": " + detail;
    }

    // The running car, all caps; empty on every other row and when the pool
    // failed to load. Pure data, never sentences.
    private string RoadTestDetail(CalendarDay day)
    {
        if (day.EventId != ControlCalendar.RoadTest || _roads.Current is not { } car)
        {
            return string.Empty;
        }
        return CarTitles.Of(car);
    }

    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>
        {
            // Back navigates: glow first, act at the end of the shared beat.
            [Strings.Get("DeliveryBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
        for (int i = 0; i < Days.Count; i++)
        {
            if (Days[i].EventLabel.Length == 0) continue;
            // Only the highlighted row answers to voice: other days carry no
            // voice key, their taps are click-only and end in the warning.
            if (!Days[i].IsToday) continue;
            int index = i;
            Action day = () => FireVoiceDay(index);
            // The road-test button names the week's car, but the mic only
            // ever hears "road test": register the short resx label, never
            // the car suffix. Other rows answer to their full label.
            string? eventId = ControlCalendar.Week[i].EventId;
            actions[eventId == ControlCalendar.RoadTest
                ? Strings.Get("Event" + ControlCalendar.RoadTest)
                : Days[i].EventLabel] = day;
            // Vosk's English model mangles "Miglia" and "Willy" but still
            // returns the reliable fragments of their names; those answer
            // for the same row, keyed by what the mic hears, not what the
            // page shows (ControlCalendar.EventKeywords).
            foreach (string keyword in ControlCalendar.KeywordsFor(ControlCalendar.Week[i].EventId))
            {
                actions[keyword] = day;
            }
        }
        VoiceActions = actions;
    }

    // The rows' one entry, by click or voice. Every tap gets the same one-beat
    // teal acknowledgment. Inside the gate (the highlighted day, 8am-8pm) the
    // host action runs and the glow lands after any mid-execute refresh like
    // the voice taps' did. Outside it the glow runs first and the COME BACK
    // warning swaps in when the beat is due — the beat's own defer pattern
    // from the navigating delivery tap — so the player sees their tap land
    // before the hint takes over. Unity JobSelectionScreen's gate checks stand.
    private void TapDay(int day)
    {
        int index = IndexOfDay(day);
        if (index < 0)
        {
            return;
        }
        if (day != Today || !ControlCalendar.IsEventWindow(_game.GameNow.Hour))
        {
            Flash(index);
            _beat.Defer(() => WarnDay(index));
            return;
        }
        _openDayAction.Execute(day);
        Flash(index);
    }

    private int IndexOfDay(int day)
    {
        for (int i = 0; i < Days.Count; i++)
        {
            if (Days[i].DayOfWeek == day)
            {
                return i;
            }
        }
        return -1;
    }

    // Warning swap: the tapped row shows the resx hint with its own day name
    // for three real seconds; everything else repaints from the calendar
    // data. Flash flags survive — a fresh tap never clears an older glow.
    private void WarnDay(int index)
    {
        _warnIndex = index;
        _warnText = Strings.Format("ControlGateWarning", Days[index].DayLabel.ToUpperInvariant());
        _warnUntil = _clock.UtcNow + TimeSpan.FromSeconds(GateWarningSeconds);
        RepaintRows();
    }

    private void ClearWarning()
    {
        _warnIndex = -1;
        _warnText = string.Empty;
        RepaintRows();
    }

    private void RepaintRows()
    {
        var rows = new CalendarRow[Days.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = Days[i] with { EventLabel = DisplayLabel(i, ControlCalendar.Week[i]) };
        }
        Days = rows;
    }

    // Voice day taps enter through the same command as clicks: the gate, the
    // glow and the warning all live in TapDay, one path for both.
    private void FireVoiceDay(int index) => DayCommand.Execute(Days[index].DayOfWeek);

    private void Flash(int dayIndex)
    {
        _flashIndex = dayIndex;
        _flashUntil = _beat.Deadline();
        FlagDay(dayIndex);
    }

    /// <summary>
    /// Clears expired tap visuals: the voice-flash and the gate warning, each
    /// on its own deadline. Called from the 10 Hz poll (the minute guard in
    /// Refresh would swallow a same-minute expiry): two DateTime compares per
    /// tick, allocations only on transitions.
    /// </summary>
    public void UpdateFlash()
    {
        if (_flashIndex >= 0 && _beat.IsDue(_flashUntil))
        {
            _flashIndex = -1;
            FlagDay(-1);
        }

        if (_warnIndex >= 0 && _clock.UtcNow >= _warnUntil)
        {
            ClearWarning();
        }
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out — a click during a pending
    // voice tap never leaves a stale teal behind (bank-page precedent).
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void FlagDay(int index)
    {
        var rows = new CalendarRow[Days.Count];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = Days[i] with { Flash = i == index };
        Days = rows;
    }
}
