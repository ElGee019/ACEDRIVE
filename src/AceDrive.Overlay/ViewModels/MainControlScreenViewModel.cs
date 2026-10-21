using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Main control page (PORTPLAN item 54, port of JobSelectionScreen): the date
/// header replaces the title, then the taxi/delivery/car-hauling entries, the
/// weekly calendar strip and Grandma's ToDo list, in the same order as the
/// Unity page. Day rows render as buttons like the Unity day entries: the
/// highlighted (current game) day wears the teal outline, and every event
/// row stays tappable. A tap inside its 8am-8pm gate glows the text teal for
/// one beat and runs the host action; a tap outside it glows the same beat
/// first, then swaps the text for the COME BACK warning for three real
/// seconds and restores (Unity RestoreButtonText parity). The Tuesday
/// road-test button names the
/// week's car inside its outline (PORTPLAN item 49). The delivery entry
/// opens the delivery career page; the car-hauling entry (PORTPLAN item 20)
/// has no flow yet, so it only answers to taps with the teal glow. The bank,
/// garage, car-dealer and car-wash entries (PORTPLAN items 48/29/30/33) are
/// temporary doors until the marker network (items 24/59) gives them a place
/// in the world.
/// The shared <see cref="VoiceBeat"/> times the teal acknowledgment glow;
/// game time flows through <see cref="GameClock"/> (date header + calendar
/// highlight), never tick counts.
/// </summary>
public sealed class MainControlScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    // resx keys for Day 1=Sunday..7=Saturday: indexed by weekday number, so
    // the labels are independent of the strip's Monday-first display order.
    private static readonly string[] DaySuffixes = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    // Spoken shortcuts that re-tap the taxi button. Players say one or two
    // words, not the full three-word label: logged 2026-10-14 sessions show
    // "start taxi" attempted on its own (and the model garbling the tail
    // into "start taxes shift", where the slice never lines up). Bare
    // "continue" stays: collision-free by routing, it lives on the dialogue
    // page's own dictionary, never here.
    // Accepted trade-off: small talk containing "continue" while the page
    // is open proposes a job, exactly like a stray click would.
    private static readonly string[] TaxiKeywords = ["continue", "start taxi"];

    // Spoken shortcuts that re-tap the car-hauling button. The label already
    // answers whole ("CAR HAULING JOB") and as its two-word leading slice, so
    // these cover what the mic hears short of the full phrase: a bare "car"
    // never reaches the label match (single-word slices are rejected).
    // Accepted trade-off: small talk containing "car" while the page is open
    // lights the button, exactly like a stray "continue" does for the taxi.
    private static readonly string[] HaulKeywords = ["car", "car hauling"];

    // Spoken shortcut for the savings bank's temporary entry (PORTPLAN 48).
    // Same accepted trade-off as "car": a stray "bank" in small talk lights
    // the button while the page is open — Unity answers mouse taps only,
    // the port answers the mic, one keyword until the real door lands.
    private static readonly string[] BankKeywords = ["bank"];

    // Spoken shortcuts for the garage and the car dealer (PORTPLAN 29/30),
    // temporary entries like the bank's: both are POI doors in Unity and move
    // behind the marker network (items 24/59) when it lands. "dealer" and
    // "car dealer" both reach the dealer page; the garage keeps its one word.
    private static readonly string[] GarageKeywords = ["garage"];

    private static readonly string[] DealerKeywords = ["dealer", "car dealer"];

    // Spoken shortcut for the car wash's temporary entry (PORTPLAN 33), the
    // same accepted trade-off as "bank": a stray "wash" in small talk lights
    // the button while the page is open. The wash is a POI door in Unity and
    // moves behind the marker network with the rest.
    private static readonly string[] CarWashKeywords = ["wash", "car wash"];

    // Spoken shortcut for the mechanic's temporary entry (PORTPLAN items
    // 31/32), the same accepted trade-off as "bank": a stray "mechanic" in
    // small talk lights the button while the page is open. The mechanic is a
    // POI door in Unity and moves behind the marker network with the rest.
    private static readonly string[] MechanicKeywords = ["mechanic", "car mechanic"];

    // Spoken shortcut for the insurance broker's temporary entry (PORTPLAN 34),
    // the same accepted trade-off as "bank". The broker is a POI door in Unity
    // and moves behind the marker network with the rest.
    private static readonly string[] InsuranceKeywords = ["insurance", "insure"];

    private readonly VoiceBeat _beat;
    private readonly GameClock _game;
    private readonly IClock _clock;
    private readonly RoadTestLedger _roads;
    private readonly TodoTable _todos;
    private readonly TaxiShift _shift;
    private readonly ICommand _openDayAction;
    private readonly Hunger _hunger;
    // The fleet: the filth HUD line reads the current car's reading (PORTPLAN
    // item 33).
    private readonly Garage _garage;
    private int _lastMinute = -1;
    private bool _deliveryOffered;
    private bool _shiftOffered;
    private string? _lastPick;
    private int _lastWeek = -1;
    private int _lastHunger = -1;
    private int _lastFilth = -1;
    private int _week;

    public MainControlScreenViewModel(
        VoiceBeat beat, GameClock game, IClock clock, RoadTestLedger roads, TodoTable todos,
        Hunger hunger, Garage garage,
        CalendarStamp stamp, TaxiShift shift, ControlActions actions)
    {
        _beat = beat;
        _game = game;
        _clock = clock;
        _roads = roads;
        _todos = todos;
        _hunger = hunger;
        _garage = garage;
        // The stamp is the persisted display week (PORTPLAN item 51); the
        // year stays data-only, never shown in the overlay.
        _week = stamp.Week;
        _shift = shift;
        TaxiCommand = actions.Taxi;
        DeliveryCommand = actions.Delivery;
        HaulCommand = actions.Haul;
        BankCommand = actions.Bank;
        GarageCommand = actions.Garage;
        DealerCommand = actions.Dealer;
        CarWashCommand = actions.CarWash;
        MechanicCommand = actions.Mechanic;
        InsuranceCommand = actions.Insurance;
        // The strip's one tap entry (click or voice): the page itself gates
        // taps, so off-gate rows answer with the warning instead of muting.
        DayCommand = new ParameterizedCommand<int>(TapDay);
        _openDayAction = actions.OpenDay;
        DeliveryLabel = Strings.Get("ControlDelivery");
        HaulLabel = Strings.Get("ControlHaul");
        BankLabel = Strings.Get("ControlBank");
        GarageLabel = Strings.Get("ControlGarage");
        DealerLabel = Strings.Get("ControlDealer");
        CarWashLabel = Strings.Get("ControlCarWash");
        MechanicLabel = Strings.Get("ControlMechanic");
        InsuranceLabel = Strings.Get("ControlInsurance");
        Refresh();
    }

    public string DateHeader { get => _dateHeader; private set => SetField(ref _dateHeader, value); }
    private string _dateHeader = "";

    /// <summary>
    /// Display week in the date header. The week rolls on the Sunday-midnight
    /// crossing (pushed by the screen controller, which owns the boundary);
    /// starts as the persisted stamp week.
    /// </summary>
    public int DisplayWeek { get => _week; set => SetField(ref _week, value); }

    public IReadOnlyList<CalendarRow> Days { get => _days; private set => SetField(ref _days, value); }
    private IReadOnlyList<CalendarRow> _days = [];

    public IReadOnlyList<string> TodoLines { get => _todoLines; private set => SetField(ref _todoLines, value); }
    private IReadOnlyList<string> _todoLines = [];

    public string TaxiLabel { get => _taxiLabel; private set => SetField(ref _taxiLabel, value); }
    private string _taxiLabel = "";
    public string DeliveryLabel { get; }
    public string HaulLabel { get; }
    public string BankLabel { get; }
    public string GarageLabel { get; }
    public string DealerLabel { get; }
    public string CarWashLabel { get; }
    public string MechanicLabel { get; }
    public string InsuranceLabel { get; }
    public ICommand TaxiCommand { get; }
    public ICommand DeliveryCommand { get; }
    public ICommand HaulCommand { get; }

    /// <summary>Temporary bank entry (PORTPLAN 48): the real door moves to the
    /// bank POI marker (items 24/59), then the button follows it.</summary>
    public ICommand BankCommand { get; }

    /// <summary>Temporary garage entry (PORTPLAN 30): a You-menu page in Unity.</summary>
    public ICommand GarageCommand { get; }

    /// <summary>Temporary dealer entry (PORTPLAN 29): a POI in Unity.</summary>
    public ICommand DealerCommand { get; }

    /// <summary>Temporary car-wash entry (PORTPLAN 33): a POI door in Unity.</summary>
    public ICommand CarWashCommand { get; }

    /// <summary>Temporary mechanic entry (PORTPLAN items 31/32): a POI door.</summary>
    public ICommand MechanicCommand { get; }

    /// <summary>Temporary insurance-broker entry (PORTPLAN 34): a POI door.</summary>
    public ICommand InsuranceCommand { get; }

    /// <summary>The meter line Unity's map HUD carries ("HUNGER 2/36"):
    /// the reading is data from Core; the copy is resx (HungerValue).</summary>
    public string HungerText { get => _hungerText; private set => SetField(ref _hungerText, value); }
    private string _hungerText = "";

    /// <summary>Whether the reading sits in Unity's amber band (YouScreen
    /// colours h 16-29): the line's colour follows, teal elsewhere.</summary>
    public bool HungerAmber { get => _hungerAmber; private set => SetField(ref _hungerAmber, value); }
    private bool _hungerAmber;

    /// <summary>The dirt line Unity's map HUD carries ("FILTH 2/25"): the
    /// current car's reading is data from Core; the copy is resx (FilthValue).</summary>
    public string FilthText { get => _filthText; private set => SetField(ref _filthText, value); }
    private string _filthText = "";

    /// <summary>Whether the reading sits in Unity's amber HUD band (MapScreen
    /// colours 15..cap orange).</summary>
    public bool FilthAmber { get => _filthAmber; private set => SetField(ref _filthAmber, value); }
    private bool _filthAmber;

    /// <summary>Whether the car is at the cap: MapScreen paints that reading
    /// the shared mint instead of the amber.</summary>
    public bool FilthFullyDirty { get => _filthFullyDirty; private set => SetField(ref _filthFullyDirty, value); }
    private bool _filthFullyDirty;

    /// <summary>The day rows' single tap entry: gates the tap itself, so a
    /// click is never swallowed by a disabled button (see <c>TapDay</c>).</summary>
    public ICommand DayCommand { get; }

    /// <summary>Highlighted calendar day, 1=Sunday..7=Saturday: the row the
    /// teal outline marks. Its gate opens on the clock, never on CanExecute.</summary>
    public int Today => _game.Today;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool TaxiFlash { get => _taxiFlash; private set => SetField(ref _taxiFlash, value); }
    private bool _taxiFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool DeliveryFlash { get => _deliveryFlash; private set => SetField(ref _deliveryFlash, value); }
    private bool _deliveryFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool HaulFlash { get => _haulFlash; private set => SetField(ref _haulFlash, value); }
    private bool _haulFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool BankFlash { get => _bankFlash; private set => SetField(ref _bankFlash, value); }
    private bool _bankFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool GarageFlash { get => _garageFlash; private set => SetField(ref _garageFlash, value); }
    private bool _garageFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool DealerFlash { get => _dealerFlash; private set => SetField(ref _dealerFlash, value); }
    private bool _dealerFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool CarWashFlash { get => _carWashFlash; private set => SetField(ref _carWashFlash, value); }
    private bool _carWashFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool MechanicFlash { get => _mechanicFlash; private set => SetField(ref _mechanicFlash, value); }
    private bool _mechanicFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool InsuranceFlash { get => _insuranceFlash; private set => SetField(ref _insuranceFlash, value); }
    private bool _insuranceFlash;

    // Voice-activation acknowledgment: the tapped button's text glows teal
    // for one shared beat (placeholder until the freeroam map lands and
    // buttons grow real actions). Clock-driven expiry via UpdateFlash so
    // FakeClock tests it deterministically.
    private string? _flashTarget;
    private DateTime _flashUntil;

    // Gate warning slot (latest tap wins, like VoiceBeat): the tapped row's
    // text carries the warning until its deadline, one slot for the whole
    // strip. Three real seconds, Unity RestoreButtonText's WaitForSeconds(3f).
    private const double GateWarningSeconds = 3;
    private int _warnIndex = -1;
    private string _warnText = string.Empty;
    private DateTime _warnUntil;

    /// <summary>Spoken text -&gt; action. Every button on the page answers to
    /// its text; the highlighted day's event also answers to its recognition
    /// keywords and the taxi entry to both shift names. Locked ones only
    /// flash teal, never fire real actions. Past days never answer to voice:
    /// only the highlighted row registers, and every tap — click or voice —
    /// is decided by the day's gate (see <c>TapDay</c>).</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// Repaints the date, calendar and todo lines. Called from the 10 Hz poll;
    /// the minute guard keeps it allocation-free between game minutes
    /// (hot-path discipline), the CanExecute check re-offers delivery once
    /// unlocked. The taxi label follows the shift: START while closed,
    /// CONTINUE while open (Unity JobSelectionScreen rule), so the shift
    /// state joins the guard and a start/end repaints even inside the same
    /// game minute. A game-day flip repaints too: the highlight moves even
    /// when the flip lands inside the same wall minute. A road-test switch
    /// (manual tap or week flip) repaints the same way: the pick joins the
    /// guard, otherwise the minute check would swallow it. Same for the
    /// display week, which the controller pushes on the Sunday boundary.
    /// </summary>
    public void Refresh()
    {
        DateTime now = _game.GameNow;
        bool deliveryOpen = DeliveryCommand.CanExecute(null);
        bool shiftStarted = _shift.IsStarted;
        string? pick = _roads.Current?.Id;
        int hunger = _hunger.Value;
        int filth = _garage.Current?.Filth.Value ?? 0;
        if (now.Hour * 60 + now.Minute == _lastMinute
            && deliveryOpen == _deliveryOffered
            && shiftStarted == _shiftOffered
            && pick == _lastPick
            && _week == _lastWeek
            && hunger == _lastHunger
            && filth == _lastFilth)
        {
            return;
        }
        _lastMinute = now.Hour * 60 + now.Minute;
        _deliveryOffered = deliveryOpen;
        _shiftOffered = shiftStarted;
        _lastPick = pick;
        _lastWeek = _week;
        _lastHunger = hunger;
        _lastFilth = filth;

        int today = ControlCalendar.ToDayOfWeek(now);
        // The year stays data-only (PORTPLAN item 51 persist): irrelevant in
        // the overlay context, so the header shows the display week alone.
        DateHeader = Strings.Format("ControlDate",
            Strings.Get("Day" + DaySuffixes[today - 1]),
            now.Hour, now.Minute, _week);
        Days = BuildDays(now, today);
        TodoLines = BuildTodos();
        SyncTaxiLabel();
        // Unity's map HUD prints the reading where the job buttons sit; the
        // amber band rides along so the colour follows the same value.
        HungerText = Strings.Format("HungerValue", hunger, _hunger.Max);
        HungerAmber = _hunger.IsAmber;
        // The dirt line beside it (Unity MapScreen HUD): the current car's
        // reading, amber in Unity's 15..cap band and mint at the cap.
        FilthText = Strings.Format("FilthValue", filth, _garage.FilthRules.MaxFilth);
        FilthAmber = _garage.FilthRules.IsAmber(filth);
        FilthFullyDirty = _garage.FilthRules.RefusesPickup(filth);
        RebuildVoice();
    }

    private static string ShiftLabel(bool started) =>
        Strings.Get(started ? "ControlTaxiContinue" : "ControlTaxi");

    // The button name follows the shift (Unity JobSelectionScreen rule),
    // but a voice tap must finish its acknowledgment glow on the old name
    // first: while the flash targets the taxi button the flip is held back,
    // and UpdateFlash lands it when the glow clears. A mouse click (no
    // flash) flips straight away.
    private void SyncTaxiLabel()
    {
        if (_flashTarget == "taxi") return;
        TaxiLabel = ShiftLabel(_shift.IsStarted);
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
        // Taxi and delivery answer to their text, even while locked: a locked
        // tap only flashes teal (plus the existing log line), never fires
        // a real action. The taxi entry answers to BOTH shift
        // names in either state: the shown label trails the shift by the
        // acknowledgment glow, and "CONTINUE TAXI SHIFT" must already
        // work while START is still lit. One shared delegate per button so
        // the caller's mute recognises either name as the same tap.
        Action taxi = () => FireVoice("taxi", TaxiCommand);
        Action haul = () => FireVoice("haul", HaulCommand);
        Action bank = () => FireVoice("bank", BankCommand);
        Action garage = () => FireVoice("garage", GarageCommand);
        Action dealer = () => FireVoice("dealer", DealerCommand);
        Action carWash = () => FireVoice("carwash", CarWashCommand);
        Action mechanic = () => FireVoice("mechanic", MechanicCommand);
        Action insurance = () => FireVoice("insurance", InsuranceCommand);
        var actions = new Dictionary<string, Action>
        {
            [ShiftLabel(started: false)] = taxi,
            [ShiftLabel(started: true)] = taxi,
            [DeliveryLabel] = () => FireVoiceDelayed("delivery", DeliveryCommand),
            [HaulLabel] = haul,
            [BankLabel] = bank,
            [GarageLabel] = garage,
            [DealerLabel] = dealer,
            [CarWashLabel] = carWash,
            [MechanicLabel] = mechanic,
            [InsuranceLabel] = insurance,
        };
        foreach (string keyword in TaxiKeywords)
        {
            actions[keyword] = taxi;
        }
        foreach (string keyword in HaulKeywords)
        {
            actions[keyword] = haul;
        }
        foreach (string keyword in BankKeywords)
        {
            actions[keyword] = bank;
        }
        foreach (string keyword in GarageKeywords)
        {
            actions[keyword] = garage;
        }
        foreach (string keyword in DealerKeywords)
        {
            actions[keyword] = dealer;
        }
        foreach (string keyword in CarWashKeywords)
        {
            actions[keyword] = carWash;
        }
        foreach (string keyword in MechanicKeywords)
        {
            actions[keyword] = mechanic;
        }
        foreach (string keyword in InsuranceKeywords)
        {
            actions[keyword] = insurance;
        }
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

    // The day rows' one entry, by click or voice. Every tap gets the same
    // one-beat teal acknowledgment. Inside the gate (the highlighted day,
    // 8am-8pm) the host action runs and the glow lands after any mid-execute
    // refresh like the voice taps' did. Outside it the glow runs first and
    // the COME BACK warning swaps in when the beat is due — the beat's own
    // defer pattern from the navigating delivery tap — so the player sees
    // their tap land before the hint takes over. Unity JobSelectionScreen's
    // gate checks stand.
    private void TapDay(int day)
    {
        int index = IndexOfDay(day);
        if (index < 0)
        {
            return;
        }
        if (day != Today || !ControlCalendar.IsEventWindow(_game.GameNow.Hour))
        {
            Flash("day", index);
            _beat.Defer(() => WarnDay(index));
            return;
        }
        _openDayAction.Execute(day);
        Flash("day", index);
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

    private void FireVoice(string target, ICommand command)
    {
        // Acknowledgment starts before the action runs: the taxi command
        // refreshes this page mid-execute, and while the flash targets
        // "taxi" the button name is held back until the glow clears.
        Flash(target);
        command.Execute(null);
    }

    // Delivery is the page's one navigating voice tap: the screen waits out
    // the shared beat so the teal glow shows before the flip. Taxi and day
    // taps stay on this page, so they act at once like a click.
    private void FireVoiceDelayed(string target, ICommand command)
    {
        Flash(target);
        _beat.Defer(() =>
        {
            ClearFlash();
            command.Execute(null);
        });
    }

    // Voice day taps enter through the same command as clicks: the gate,
    // the glow and the warning all live in TapDay, one path for both.
    private void FireVoiceDay(int index) => DayCommand.Execute(Days[index].DayOfWeek);

    private void Flash(string target, int dayIndex = -1)
    {
        TaxiFlash = target == "taxi";
        DeliveryFlash = target == "delivery";
        HaulFlash = target == "haul";
        BankFlash = target == "bank";
        GarageFlash = target == "garage";
        DealerFlash = target == "dealer";
        CarWashFlash = target == "carwash";
        MechanicFlash = target == "mechanic";
        InsuranceFlash = target == "insurance";
        FlagDay(dayIndex);
        _flashTarget = target;
        _flashUntil = _beat.Deadline();
    }

    /// <summary>
    /// Clears expired tap visuals: the voice-flash and the gate warning, each
    /// on its own deadline. Called from the 10 Hz poll (the minute guard in
    /// Refresh would swallow a same-minute expiry): two DateTime compares per
    /// tick, allocations only on transitions. When the flash targeted the
    /// taxi button, the shift-label flip it held back (START glows while the
    /// shift already runs) lands here.
    /// </summary>
    public void UpdateFlash()
    {
        if (_flashTarget is not null && _beat.IsDue(_flashUntil))
        {
            ClearFlash();
            SyncTaxiLabel();
        }

        if (_warnIndex >= 0 && _clock.UtcNow >= _warnUntil)
        {
            ClearWarning();
        }
    }

    // Glow off on every button at once: the deferred delivery tap clears
    // here when it fires, the poll clears here when the beat expires.
    private void ClearFlash()
    {
        _flashTarget = null;
        TaxiFlash = false;
        DeliveryFlash = false;
        HaulFlash = false;
        BankFlash = false;
        GarageFlash = false;
        DealerFlash = false;
        CarWashFlash = false;
        MechanicFlash = false;
        InsuranceFlash = false;
        FlagDay(-1);
    }

    private void FlagDay(int index)
    {
        var rows = new CalendarRow[Days.Count];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = Days[i] with { Flash = i == index };
        Days = rows;
    }
}
