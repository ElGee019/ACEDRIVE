using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>One calendar row, preformatted: the view stays dumb.</summary>
public sealed record CalendarRow(string DayLabel, string EventLabel, bool IsToday, bool IsOpen, bool Flash = false);

/// <summary>
/// Main control page (PORTPLAN item 54, port of JobSelectionScreen): the date
/// header replaces the title, then the taxi/delivery entries, the weekly
/// calendar strip and Grandma's ToDo list, in the same order as the Unity
/// page. Day rows render as buttons like the Unity day entries but stay
/// disabled until the freeroam map lands (no destinations to drive to yet);
/// the delivery entry opens the delivery career page.
/// Time flows through <see cref="IClock"/>, never tick counts.
/// </summary>
public sealed class MainControlScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    // resx keys for Day 1=Sunday..7=Saturday, Sunday-first like the Unity strip.
    private static readonly string[] DaySuffixes = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    // Spoken shortcuts that re-tap the taxi button. Players say one word,
    // not the full three-word label. Collision-free by routing: bare
    // "continue" lives on the dialogue page's own dictionary, never here.
    // Accepted trade-off: small talk containing "continue" while the page
    // is open proposes a job, exactly like a stray click would.
    private static readonly string[] TaxiKeywords = ["continue"];

    private readonly IClock _clock;
    private readonly TodoTable _todos;
    private readonly CalendarStamp _stamp;
    private readonly TaxiShift _shift;
    private int _lastMinute = -1;
    private bool _deliveryOffered;
    private bool _shiftOffered;

    public MainControlScreenViewModel(
        IClock clock, TodoTable todos, CalendarStamp stamp,
        TaxiShift shift, ICommand taxiCommand, ICommand deliveryCommand,
        ICommand dayCommand)
    {
        _clock = clock;
        _todos = todos;
        _stamp = stamp;
        _shift = shift;
        TaxiCommand = taxiCommand;
        DeliveryCommand = deliveryCommand;
        DayCommand = dayCommand;
        DeliveryLabel = Strings.Get("ControlDelivery");
        Refresh();
    }

    public string DateHeader { get => _dateHeader; private set => SetField(ref _dateHeader, value); }
    private string _dateHeader = "";

    public IReadOnlyList<CalendarRow> Days { get => _days; private set => SetField(ref _days, value); }
    private IReadOnlyList<CalendarRow> _days = [];

    public IReadOnlyList<string> TodoLines { get => _todoLines; private set => SetField(ref _todoLines, value); }
    private IReadOnlyList<string> _todoLines = [];

    public string TaxiLabel { get => _taxiLabel; private set => SetField(ref _taxiLabel, value); }
    private string _taxiLabel = "";
    public string DeliveryLabel { get; }
    public ICommand TaxiCommand { get; }
    public ICommand DeliveryCommand { get; }
    public ICommand DayCommand { get; }

    /// <summary>Yellow-glow acknowledgment for voice taps, one per button.</summary>
    public bool TaxiFlash { get => _taxiFlash; private set => SetField(ref _taxiFlash, value); }
    private bool _taxiFlash;

    /// <summary>Yellow-glow acknowledgment for voice taps, one per button.</summary>
    public bool DeliveryFlash { get => _deliveryFlash; private set => SetField(ref _deliveryFlash, value); }
    private bool _deliveryFlash;

    // Voice-activation acknowledgment: the tapped button's text glows yellow
    // for one second (placeholder until the freeroam map lands and buttons
    // grow real actions). Clock-driven expiry via UpdateFlash so FakeClock
    // tests it deterministically.
    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(1);
    private string? _flashTarget;
    private DateTime _flashUntil;

    /// <summary>Spoken text -&gt; action. Every button on the page answers to
    /// its text; event days also answer to their recognition keywords and
    /// the taxi entry to both shift names. Locked ones only flash yellow,
    /// never fire real actions.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// Repaints the date, calendar and todo lines. Called from the 10 Hz poll;
    /// the minute guard keeps it allocation-free between minutes (hot-path
    /// discipline), the CanExecute check re-offers delivery once unlocked.
    /// The taxi label follows the shift: START while closed, CONTINUE while
    /// open (Unity JobSelectionScreen rule), so the shift state joins the
    /// guard and a start/end repaints even inside the same minute.
    /// </summary>
    public void Refresh()
    {
        DateTime now = _clock.UtcNow;
        bool deliveryOpen = DeliveryCommand.CanExecute(null);
        bool shiftStarted = _shift.IsStarted;
        if (now.Hour * 60 + now.Minute == _lastMinute
            && deliveryOpen == _deliveryOffered
            && shiftStarted == _shiftOffered)
        {
            return;
        }
        _lastMinute = now.Hour * 60 + now.Minute;
        _deliveryOffered = deliveryOpen;
        _shiftOffered = shiftStarted;

        int today = ControlCalendar.ToDayOfWeek(now);
        // The year stays in CalendarStamp as data (PORTPLAN item 51 persist)
        // but is deliberately not displayed: irrelevant in the overlay context.
        DateHeader = Strings.Format("ControlDate",
            Strings.Get("Day" + DaySuffixes[today - 1]),
            now.Hour, now.Minute, _stamp.Week);
        Days = BuildDays(now, today);
        TodoLines = BuildTodos();
        SyncTaxiLabel();
        RebuildVoice();
    }

    private static string ShiftLabel(bool started) =>
        Strings.Get(started ? "ControlTaxiContinue" : "ControlTaxi");

    // The button name follows the shift (Unity JobSelectionScreen rule),
    // but a voice tap must finish its 1 s acknowledgment glow on the old
    // name first: while the flash targets the taxi button the flip is held
    // back, and UpdateFlash lands it when the glow clears. A mouse click
    // (no flash) flips straight away.
    private void SyncTaxiLabel()
    {
        if (_flashTarget == "taxi") return;
        TaxiLabel = ShiftLabel(_shift.IsStarted);
    }

    private static CalendarRow[] BuildDays(DateTime now, int today)
    {
        var rows = new CalendarRow[ControlCalendar.Week.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            CalendarDay day = ControlCalendar.Week[i];
            bool isToday = day.DayOfWeek == today;
            rows[i] = new CalendarRow(
                Strings.Get("Day" + DaySuffixes[day.DayOfWeek - 1]),
                day.EventId is null ? string.Empty : Strings.Get("Event" + day.EventId),
                isToday,
                isToday && ControlCalendar.IsEventWindow(now.Hour));
        }
        return rows;
    }

    private string[] BuildTodos()
    {
        var lines = new string[_todos.Entries.Count];
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = FormatTodo(_todos.Entries[i]);
        }
        return lines;
    }

    // Progress reads 0 until the persistent save lands (PORTPLAN item 51);
    // the top slot is reserved for the grandma quest tip, whose addresses
    // need the Eifel table (PORTPLAN Step 1).
    private static string FormatTodo(TodoEntry entry) => entry.SubjectId switch
    {
        GrandmaTodo.MaxGrocery => Strings.Get("TodoMaxGrocery"),
        GrandmaTodo.VisitBank => Strings.Get("TodoVisitBank"),
        GrandmaTodo.TaxiJobs => Strings.Format("TodoTaxiJobs", entry.Target, 0),
        GrandmaTodo.EarnCr => Strings.Format("TodoEarnCr", entry.Target, 0),
        _ => entry.SubjectId,
    };

    private void RebuildVoice()
    {
        // Every button on the page answers to its text, even while locked:
        // a locked tap only flashes yellow (plus the existing log line),
        // never fires a real action. The taxi entry answers to BOTH shift
        // names in either state: the shown label trails the shift by the
        // 1 s acknowledgment glow, and "CONTINUE TAXI SHIFT" must already
        // work while START is still lit. One shared delegate per button so
        // the caller's mute recognises either name as the same tap.
        Action taxi = () => FireVoice("taxi", TaxiCommand);
        var actions = new Dictionary<string, Action>
        {
            [ShiftLabel(started: false)] = taxi,
            [ShiftLabel(started: true)] = taxi,
            [DeliveryLabel] = () => FireVoice("delivery", DeliveryCommand),
        };
        foreach (string keyword in TaxiKeywords)
        {
            actions[keyword] = taxi;
        }
        for (int i = 0; i < Days.Count; i++)
        {
            if (Days[i].EventLabel.Length == 0) continue;
            int index = i;
            Action day = () => FireVoiceDay(index);
            actions[Days[i].EventLabel] = day;
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

    private void FireVoice(string target, ICommand command)
    {
        // Acknowledgment starts before the action runs: the taxi command
        // refreshes this page mid-execute, and while the flash targets
        // "taxi" the button name is held back until the glow clears.
        Flash(target);
        command.Execute(null);
    }

    private void FireVoiceDay(int index)
    {
        DayCommand.Execute(null);
        Flash("day", index);
    }

    private void Flash(string target, int dayIndex = -1)
    {
        TaxiFlash = target == "taxi";
        DeliveryFlash = target == "delivery";
        FlagDay(dayIndex);
        _flashTarget = target;
        _flashUntil = _clock.UtcNow + FlashDuration;
    }

    /// <summary>
    /// Clears an expired 1 s voice-flash. Called from the 10 Hz poll (the
    /// minute guard in Refresh would swallow a same-minute expiry): one
    /// DateTime compare per tick, allocations only on transitions. When the
    /// flash targeted the taxi button, the shift-label flip it held back
    /// (START glows while the shift already runs) lands here.
    /// </summary>
    public void UpdateFlash()
    {
        if (_flashTarget is null || _clock.UtcNow < _flashUntil) return;
        _flashTarget = null;
        TaxiFlash = false;
        DeliveryFlash = false;
        FlagDay(-1);
        SyncTaxiLabel();
    }

    private void FlagDay(int index)
    {
        var rows = new CalendarRow[Days.Count];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = Days[i] with { Flash = i == index };
        Days = rows;
    }
}
