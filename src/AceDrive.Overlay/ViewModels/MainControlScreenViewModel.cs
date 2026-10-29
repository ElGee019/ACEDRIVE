using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Main control page (PORTPLAN item 54, port of JobSelectionScreen): the
/// taxi/delivery/car-hauling entries and the police placeholder, the career
/// ToDo list (PORTPLAN 46) and the completed block that offers DELIVER CARE
/// PACKAGES once five kits wait, in the same order as the Unity page. The
/// weekly calendar strip lives on its own page
/// (<see cref="CalendarScreenViewModel"/>), opened from the menu's CALENDAR
/// tile. The delivery entry opens the delivery career page; the car-hauling
/// entry settles the haul flow's run and shows its receipt. Every business
/// (bank, dealer, car wash, mechanic, insurance) is now a POI on the addresses
/// page, not a job button (PORTPLAN 24/59); the garage lives on the home page,
/// where Unity's You menu keeps it.
/// The police entry is a placeholder until PORTPLAN 41's job flow lands: a tap
/// swaps the button's own text for COMING SOON and restores it three real
/// seconds later, so the button is live and speakable without pretending to
/// run a job it cannot settle yet.
/// The shared <see cref="VoiceBeat"/> times the teal acknowledgment glow on
/// the injected clock, never tick counts.
/// </summary>
public sealed class MainControlScreenViewModel : ViewModelBase, IVoiceScreen
{
    // Spoken shortcuts that re-tap the taxi button. Players say one or two
    // words, not the full three-word label: logged 2026-10-14 sessions show
    // "start taxi" attempted on its own (and the model garbling the tail
    // into "start taxes shift", where the slice never lines up). The
    // 2026-10-18/22 sessions added the other half: the model hears "start"
    // as "stuart" or "starts" ("stuart ducks a shift", "stuart's taxes
    // shift", "starts taxes shift"), so each logged two-word run gets its
    // own keyword. WHY never the bare "stuart"/"starts": background speech
    // says those words far too often, and an accidental tap starts a shift.
    // Normalize splits the possessive into "stuart s" ("stuart's taxi").
    // Bare "continue" stays: collision-free by routing, it lives on the
    // dialogue page's own dictionary, never here.
    // Accepted trade-off: small talk containing "continue" while the page
    // is open proposes a job, exactly like a stray click would.
    private static readonly string[] TaxiKeywords =
    [
        "continue",
        "start taxi",
        "stuart taxi",
        "stuart taxes",
        "stuart ducks",
        "stuart s taxi",
        "stuart s taxes",
        "stuart nazi",
        "starts taxi",
        "starts taxes",
        "start decks",
    ];

    // Spoken shortcut for the car hauling entry. The label already answers
    // whole ("CAR HAULING JOB") and as its two-word leading slice, so these
    // cover what the mic hears short of the full phrase: a bare "car" never
    // reaches the label match (single-word slices are rejected).
    // Accepted trade-off: small talk containing "car" while the page is open
    // lights the button, exactly like a stray "continue" does for the taxi.
    private static readonly string[] HaulKeywords = ["car", "car hauling"];

    // Spoken name of the police entry. The word stays English whatever the
    // button prints: the Vosk model is English, so a Dutch "POLITIE" button
    // still has to answer "police" (the HUD keyword table's rule).
    private static readonly string[] PoliceKeywords = ["police"];

    // Completed kits needed before DELIVER CARE PACKAGES offers itself (Unity
    // TipsCompletedScreen.CarePackageBagThreshold). Below it the finished tips
    // stay listed and the button waits.
    private const int DeliverThreshold = 5;

    // How long the police button keeps its COMING SOON text. Three real
    // seconds: the player reads the toast, and the button is back to its own
    // name before the next tap. Same clock-driven hold as the calendar's gate
    // warning.
    private const double ComingSoonSeconds = 3;

    private readonly VoiceBeat _beat;
    private readonly IClock _clock;
    private readonly ITodoList _todos;
    private readonly TaxiShift _shift;
    private readonly ICommand _backCommand;
    private readonly ICommand _todoAcknowledge;

    // The police button's own name, whatever the toast currently prints: the
    // voice entry keys off this and the hold restores it, so the swap never
    // changes what the button answers to.
    private readonly string _policeName = Strings.Get("ControlPolice");

    // Deadline of the COMING SOON text, null while the button shows its name.
    private DateTime? _policeUntil;
    // Null until the first Refresh, so the ctor's paint always runs (the HUD
    // lines used to force it with their -1 sentinels).
    private bool? _deliveryOffered;
    private bool? _shiftOffered;
    private int _todoVersion = -1;

    public MainControlScreenViewModel(
        VoiceBeat beat, IClock clock, ITodoList todos,
        TaxiShift shift, ControlActions actions, ICommand back)
    {
        _beat = beat;
        _clock = clock;
        _todos = todos;
        _shift = shift;
        _backCommand = back;
        _todoAcknowledge = actions.TodoAcknowledge;
        TaxiCommand = actions.Taxi;
        DeliveryCommand = actions.Delivery;
        HaulCommand = actions.Haul;
        PoliceCommand = new RelayCommand(ShowComingSoon);
        BackCommand = new RelayCommand(Back);
        DeliveryLabel = Strings.Get("ControlDelivery");
        HaulLabel = Strings.Get("ControlHaul");
        PoliceLabel = _policeName;
        Refresh();
    }

    public IReadOnlyList<string> TodoLines { get => _todoLines; private set => SetField(ref _todoLines, value); }
    private IReadOnlyList<string> _todoLines = [];

    /// <summary>The completed tips the popup block lists, numbered from 1
    /// (Unity's TodoFinishedPopup has no count; the page shows the player how
    /// many kits the five-kit threshold still waits on).</summary>
    public IReadOnlyList<string> CompletedTodoLines { get => _completedTodoLines; private set => SetField(ref _completedTodoLines, value); }
    private IReadOnlyList<string> _completedTodoLines = [];

    /// <summary>True while the completed block is showing.</summary>
    public bool HasCompletedTodos { get => _hasCompletedTodos; private set => SetField(ref _hasCompletedTodos, value); }
    private bool _hasCompletedTodos;

    /// <summary>True once DeliverThreshold kits wait: DELIVER CARE PACKAGES is up.</summary>
    public bool ShowDeliverCarePackages { get => _showDeliverCarePackages; private set => SetField(ref _showDeliverCarePackages, value); }
    private bool _showDeliverCarePackages;

    /// <summary>DELIVER CARE PACKAGES: acknowledges the completed tips and refills the slots.</summary>
    public ICommand TodoAcknowledgeCommand => _todoAcknowledge;

    public string TaxiLabel { get => _taxiLabel; private set => SetField(ref _taxiLabel, value); }
    private string _taxiLabel = "";
    public string DeliveryLabel { get; }
    public string HaulLabel { get; }

    /// <summary>The police entry's text: its own name, or COMING SOON while a
    /// tap's three-second hold runs.</summary>
    public string PoliceLabel { get => _policeLabel; private set => SetField(ref _policeLabel, value); }
    private string _policeLabel = "";

    public ICommand TaxiCommand { get; }
    public ICommand DeliveryCommand { get; }
    public ICommand HaulCommand { get; }

    /// <summary>The police placeholder's tap: swaps the text for COMING SOON
    /// and restores it after the hold.</summary>
    public ICommand PoliceCommand { get; }

    /// <summary>The page's way back to the menu; a voice tap glows first and
    /// waits out the shared beat like every navigating button.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool TaxiFlash { get => _taxiFlash; private set => SetField(ref _taxiFlash, value); }
    private bool _taxiFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool DeliveryFlash { get => _deliveryFlash; private set => SetField(ref _deliveryFlash, value); }
    private bool _deliveryFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool HaulFlash { get => _haulFlash; private set => SetField(ref _haulFlash, value); }
    private bool _haulFlash;

    /// <summary>Teal-glow acknowledgment for a spoken police tap.</summary>
    public bool PoliceFlash { get => _policeFlash; private set => SetField(ref _policeFlash, value); }
    private bool _policeFlash;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    // Voice-activation acknowledgment: the tapped button's text glows teal
    // for one shared beat (placeholder until the freeroam map lands and
    // buttons grow real actions). Clock-driven expiry via UpdateFlash so
    // FakeClock tests it deterministically.
    private string? _flashTarget;
    private DateTime _flashUntil;

    /// <summary>Spoken text -&gt; action. Every button on the page answers to
    /// its text and the taxi entry to both shift names. Locked ones only
    /// flash teal, never fire real actions.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// Repaints the todo lines. Called from the 10 Hz poll; the value guards
    /// keep it allocation-free between changes (hot-path discipline), the
    /// CanExecute check re-offers delivery once unlocked. The taxi label
    /// follows the shift: START while closed, CONTINUE while open (Unity
    /// JobSelectionScreen rule), so the shift state joins the guard and a
    /// start/end repaints even inside the same game minute.
    /// </summary>
    public void Refresh()
    {
        bool deliveryOpen = DeliveryCommand.CanExecute(null);
        bool shiftStarted = _shift.IsStarted;
        if (deliveryOpen == _deliveryOffered
            && shiftStarted == _shiftOffered
            && _todos.Version == _todoVersion)
        {
            return;
        }
        _deliveryOffered = deliveryOpen;
        _shiftOffered = shiftStarted;
        _todoVersion = _todos.Version;

        TodoLines = BuildLines(_todos.Slots, numbered: false);
        CompletedTodoLines = BuildLines(_todos.CompletedLines, numbered: true);
        HasCompletedTodos = _todos.HasCompleted;
        ShowDeliverCarePackages = _todos.CompletedCount >= DeliverThreshold;
        SyncTaxiLabel();
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

    // A visit tip carries the door name in {0}; every progressive tip carries
    // the target in {0} and the career reading in {1}. The copy's own format
    // specifiers decide the decimals, so no number formatting lives here.
    private static string[] BuildLines(IReadOnlyList<TodoLine> lines, bool numbered)
    {
        var texts = new string[lines.Count];
        for (int i = 0; i < texts.Length; i++)
        {
            TodoLine line = lines[i];
            string text = line.Placeholder is null
                ? Strings.Format(line.CopyKey, line.Target, line.Current)
                : Strings.Format(line.CopyKey, line.Placeholder);
            // The completed block counts its tips so the player can see how
            // many kits the five-kit threshold still waits on. The marker is a
            // resx format, not a hardcoded "n.", so a translation can word it.
            texts[i] = numbered ? Strings.Format("TodoCompletedLine", i + 1, text) : text;
        }
        return texts;
    }

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
        Action police = () => FireVoice("police", PoliceCommand);
        var actions = new Dictionary<string, Action>
        {
            [ShiftLabel(started: false)] = taxi,
            [ShiftLabel(started: true)] = taxi,
            [DeliveryLabel] = () => FireVoiceDelayed("delivery", DeliveryCommand),
            [HaulLabel] = haul,
            // Keyed off the stable name, never the mutable label: a rebuild
            // that lands inside the COMING SOON hold must not rename the door.
            [_policeName] = police,
            // Back navigates: glow first, act at the end of the shared beat.
            [Strings.Get("DeliveryBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
        if (ShowDeliverCarePackages)
        {
            // DELIVER CARE PACKAGES is a utility tap, not a navigation: it acts
            // at once like a click.
            actions[Strings.Get("TodoDeliver")] = () => _todoAcknowledge.Execute(null);
        }
        foreach (string keyword in TaxiKeywords)
        {
            actions[keyword] = taxi;
        }
        foreach (string keyword in HaulKeywords)
        {
            actions[keyword] = haul;
        }
        foreach (string keyword in PoliceKeywords)
        {
            actions[keyword] = police;
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

    // Delivery is the page's one navigating voice tap: the screen waits out
    // the shared beat so the teal glow shows before the flip. Taxi taps stay
    // on this page, so they act at once like a click.
    private void FireVoiceDelayed(string target, ICommand command)
    {
        Flash(target);
        _beat.Defer(() =>
        {
            ClearFlash();
            command.Execute(null);
        });
    }

    // The police placeholder's whole mechanic: the button prints COMING SOON
    // for three real seconds, then its own name returns (UpdateFlash). A
    // second tap inside the hold restarts it, like every latest-tap-wins slot.
    private void ShowComingSoon()
    {
        _policeUntil = _clock.UtcNow + TimeSpan.FromSeconds(ComingSoonSeconds);
        PoliceLabel = Strings.Get("ControlPoliceSoon");
    }

    private void Flash(string target)
    {
        TaxiFlash = target == "taxi";
        DeliveryFlash = target == "delivery";
        HaulFlash = target == "haul";
        PoliceFlash = target == "police";
        _flashTarget = target;
        _flashUntil = _beat.Deadline();
    }

    /// <summary>
    /// Clears the expired voice-flash. Called from the 10 Hz poll (a refresh
    /// guard would swallow a same-minute expiry): one DateTime compare per
    /// tick, allocations only on transitions. When the flash targeted the
    /// taxi button, the shift-label flip it held back (START glows while the
    /// shift already runs) lands here. The police text's own hold expires on
    /// the same tick, on its own deadline.
    /// </summary>
    public void UpdateFlash()
    {
        if (_flashTarget is not null && _beat.IsDue(_flashUntil))
        {
            ClearFlash();
            SyncTaxiLabel();
        }

        if (_policeUntil is { } due && _clock.UtcNow >= due)
        {
            RestorePoliceLabel();
        }
    }

    // The button's own name returns: the hold's expiry (poll) and the way out
    // (Back) both land here, so a player coming back never finds a stale toast
    // on a page that was cached mid-hold.
    private void RestorePoliceLabel()
    {
        _policeUntil = null;
        PoliceLabel = _policeName;
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out — a click during a pending
    // voice tap never leaves a stale teal behind (bank-page precedent).
    private void Back()
    {
        BackFlash = false;
        RestorePoliceLabel();
        _backCommand.Execute(null);
    }

    // Glow off on every button at once: the deferred delivery tap clears
    // here when it fires, the poll clears here when the beat expires.
    private void ClearFlash()
    {
        _flashTarget = null;
        TaxiFlash = false;
        DeliveryFlash = false;
        HaulFlash = false;
        PoliceFlash = false;
        BackFlash = false;
    }
}
