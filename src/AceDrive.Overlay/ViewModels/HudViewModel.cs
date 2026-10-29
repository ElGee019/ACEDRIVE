using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The 200 px companion HUD: the driven car's odometer plus the street and
/// house number behind the live lap position. It is a passive consumer of
/// MainViewModel's 10 Hz poll; formatting happens only when a displayed value
/// actually changes, never once per tick.
/// </summary>
public sealed class HudViewModel : ViewModelBase, ITelemetryHud
{
    // WHY 1 km/h: EVO reports a little jitter around a standstill. Anything
    // above a slow creep still counts as moving, so the ENTER button only
    // appears once the car is genuinely parked.
    private const float StationarySpeedKmh = 1.0f;

    private readonly AddressMap _map;
    private readonly VoiceBeat _beat;
    private readonly IClock _clock;
    // The distance line quantises to this step, read from Data/taxi_rules.json
    // by the composition root so the HUD never carries a balance literal.
    private readonly double _distanceStepMeters;

    private PlayerPreferences _preferences;

    private string _headerText = Strings.Get("WaitingForEvo");
    private string _mileageText = Strings.Get("HudNoCar");
    private string _addressText = Strings.Get("HudNoPosition");
    private string _mileageLabel = Strings.Get("HudMileageLabel");
    private string _addressLabel = Strings.Get("HudAddressLabel");
    private string _destinationText = string.Empty;
    private string _enterText = string.Empty;
    private IReadOnlyList<string> _enterKeywords = HudVoiceKeywords.For("HudEnter");
    private string _wrongCarWarning = Strings.Get("HudWrongCar");

    // The destination itself, kept so a language switch can repaint the line
    // without the ViewModel pushing it again. The copy is a set of resource
    // keys the story hands over, never sentences, so it repaints with the rest.
    private PointOfInterest? _destination;
    private HudDestinationCopy? _copy;
    // The active job leg, when any (taxi or delivery): it replaces the story
    // destination in the banner until the job settles.
    private HudJobGuidance? _jobGuidance;
    private bool _hasDestination;
    private bool _atDestination;
    // Set while the player is inside the pinned door. The arrival was consumed
    // by the entry, so the task text and ENTER hide until a new leg is pinned
    // or the car leaves and parks there again.
    private bool _entered;
    private bool _wrongCar;
    private bool _isRevealed;
    // The live club reputation the Cento Miglia race hands over, if any, held
    // as the 5-decimal reading the counter paints. It keeps its own line so the
    // banner above stays the drive-to sentence: the player must still read
    // where the finish is. Null takes the line down again.
    private double? _reputation;
    // The reading already painted, so the 10 Hz race counter formats only when
    // the shown digit actually moves. The sentinel forces the first paint.
    private long _shownReputation = long.MinValue;
    private string _reputationText = string.Empty;
    // The customer request currently presented on the companion, if any. The
    // host creates it (it owns the countdown and the outcome); the HUD only
    // paints it in place of the task text and lets its buttons run.
    private CustomerRequestScreenViewModel? _request;

    // The road test's live block (either leg): the task sentence's resource key,
    // the target speed the key prints and the reading already painted. The flow
    // owns the clock, so these are kept to repaint the sentence on a language
    // or unit switch.
    private string _roadTestTaskKey = string.Empty;
    private double _roadTestTargetKmh;
    private HudRoadTestReadout? _roadTest;

    // The line under the banner: the remaining distance to the pinned leg (a
    // taxi job or an intro drive), or a transient host notice (the notice
    // wins). Both are empty with no leg.
    private string _legDistanceText = string.Empty;
    private string _noticeText = string.Empty;
    private DateTime _noticeUntil;
    // The quantised distance already painted, so the 10 Hz poll only formats
    // when the displayed step moves. Sentinels force the first paint.
    private long _shownLegDistance = long.MinValue;
    private bool _legDistanceInKm;

    // The timed ask's remaining delivery time (speed challenge or accepted
    // passenger promise), painted under the distance line. The deadline rides
    // on HudJobGuidance, so the HUD still formats and ticks it while the host
    // owns the clock.
    private string _deadlineText = string.Empty;
    private bool _deadlineLate;
    private long _shownDeadlineSeconds = long.MinValue;

    // Voice acknowledgment: the ENTER button glows teal for one shared beat,
    // then the deferred voice tap acts. Cleared by the 10 Hz poll on expiry.
    private bool _enterFlash;
    private DateTime _enterFlashUntil;

    // The live header is the cached static TrackInfo object; comparing the
    // record is enough on the hot path (no string formatting when it matches).
    private TrackInfo? _lastTrack;
    private bool _headerLive;

    // Tenths of the player's unit: the readout carries one decimal, so the
    // guard must trip when the displayed digit moves, not on whole units.
    private long? _shownTenths;

    // The address only repaints when the street or number moves.
    private bool _addressShown;
    private string _street = string.Empty;
    private int _houseNumber;

    // WHY kept: ApplyPreferences repaints the readouts between two polls, so
    // the last inputs must be replayable without opening shared memory again.
    private TelemetrySample? _lastSample;
    private double? _lastOdometerKm;
    private bool _lastGameRunning;
    private bool _lastWrongCar;

    public HudViewModel(
        AddressMap map, PlayerPreferences preferences, VoiceBeat beat,
        IClock clock, TaxiRules rules)
    {
        _map = map;
        _preferences = preferences;
        _beat = beat;
        _clock = clock;
        _distanceStepMeters = double.IsFinite(rules.DisplayDistanceStepMeters)
            && rules.DisplayDistanceStepMeters > 0.0
                ? rules.DisplayDistanceStepMeters
                : TaxiRules.Default.DisplayDistanceStepMeters;
        EnterCommand = new RelayCommand(Enter);
        PhotoshootCommand = new RelayCommand(Photoshoot);
    }

    /// <summary>Track line while live, or the waiting/EVO status when not.</summary>
    public string HeaderText
    {
        get => _headerText;
        private set => SetField(ref _headerText, value);
    }

    /// <summary>The driven car's odometer in the player's chosen unit.</summary>
    public string MileageText
    {
        get => _mileageText;
        private set => SetField(ref _mileageText, value);
    }

    /// <summary>Street, house number and city at the current lap position.</summary>
    public string AddressText
    {
        get => _addressText;
        private set => SetField(ref _addressText, value);
    }

    /// <summary>"MILEAGE"/"KILOMETERSTAND"; bindable so a language switch repaints it.</summary>
    public string MileageLabel
    {
        get => _mileageLabel;
        private set => SetField(ref _mileageLabel, value);
    }

    /// <summary>"CURRENT STREET"/"HUIDIGE STRAAT"; bindable for the same reason.</summary>
    public string AddressLabel
    {
        get => _addressLabel;
        private set => SetField(ref _addressLabel, value);
    }

    /// <summary>
    /// Drive-to sentence while en route, arrival sentence while parked;
    /// empty with no destination.
    /// </summary>
    public string DestinationText
    {
        get => _destinationText;
        private set
        {
            if (SetField(ref _destinationText, value))
            {
                OnPropertyChanged(nameof(BannerText));
            }
        }
    }

    /// <summary>"ENTER"/"PICK UP"; empty with no destination.</summary>
    public string EnterText
    {
        get => _enterText;
        private set => SetField(ref _enterText, value);
    }

    /// <summary>Tap handler for the arrival button; the host decides what entering means.</summary>
    public RelayCommand EnterCommand { get; }

    /// <summary>
    /// Spoken words the action button answers to right now ("pick up" at the
    /// pickup door, "drop off" at the drop-off). Resolved from the same action
    /// key that paints the label, so voice follows whatever the button means;
    /// the words stay English even when the label is translated.
    /// </summary>
    public IReadOnlyList<string> EnterKeywords => _enterKeywords;

    /// <summary>
    /// Remaining distance to the pinned leg ("120 m"/"1.4 km") — a taxi or
    /// delivery leg, or an intro drive — empty when no leg is up or the live
    /// sample has no track position.
    /// </summary>
    public string LegDistanceText => _legDistanceText;

    /// <summary>A transient host notice (police fine, refusal hint); empty when none.</summary>
    public string NoticeText => _noticeText;

    public bool HasNotice => _noticeText.Length > 0;

    /// <summary>The line under the banner: the notice wins over the distance.</summary>
    public string HudLineText => HasNotice ? _noticeText : _legDistanceText;

    public bool HasHudLine => HudLineText.Length > 0;

    /// <summary>
    /// The timed ask's countdown ("2:31", then "TOO LATE — NO TIP"), empty
    /// when no deadline is up. The label comes from the guidance's
    /// <c>DeadlineKey</c>, so a speed challenge and a passenger's hurry read
    /// differently on the same clock. Painted in the design's Tourney face so
    /// it reads as a clock rather than a guidance sentence.
    /// </summary>
    public string DeadlineText
    {
        get => _deadlineText;
        private set
        {
            if (SetField(ref _deadlineText, value))
            {
                OnPropertyChanged(nameof(HasDeadline));
            }
        }
    }

    public bool HasDeadline => _deadlineText.Length > 0;

    public bool DeadlineLate
    {
        get => _deadlineLate;
        private set => SetField(ref _deadlineLate, value);
    }

    /// <summary>
    /// The live club reputation while a Cento Miglia race runs ("YOUR CLUB
    /// REPUTATION: 1.00623"), empty when no race is on. It has its own line
    /// under the distance so the banner keeps the drive-to sentence.
    /// </summary>
    public string ReputationText
    {
        get => _reputationText;
        private set => SetField(ref _reputationText, value);
    }

    /// <summary>True while the race counter has a line of its own.</summary>
    public bool HasReputation => _reputation is not null;

    /// <summary>
    /// True while a road-test leg paints its block: the task sentence and the
    /// countdown or running time. It replaces the banner — a leg being timed
    /// has no door left to drive to — and clears when the run ends.
    /// </summary>
    public bool HasRoadTest => _roadTest is not null;

    /// <summary>True while the destination banner has the top line.</summary>
    public bool ShowBanner => HasBanner && _roadTest is null;

    /// <summary>
    /// The road-test task sentence ("Let's see how fast you can get from
    /// 0-60 km/h."), empty with no leg. Formatted from the stored key so a
    /// language or miles/kilometres switch repaints the target it names.
    /// </summary>
    public string RoadTestTaskText
    {
        get => _roadTestTaskText;
        private set => SetField(ref _roadTestTaskText, value);
    }
    private string _roadTestTaskText = string.Empty;

    /// <summary>The counter digit or the running three-decimal time, empty with no leg.</summary>
    public string RoadTestClockText
    {
        get => _roadTestClockText;
        private set
        {
            if (SetField(ref _roadTestClockText, value))
            {
                OnPropertyChanged(nameof(HasRoadTestClock));
            }
        }
    }
    private string _roadTestClockText = string.Empty;

    /// <summary>
    /// True while the block paints a reading (the counter, the stopwatch, the
    /// brake distance). The photo mission is not timed, so its block shows the
    /// drive-anywhere brief and the PHOTOSHOOT button instead of an empty clock
    /// line.
    /// </summary>
    public bool HasRoadTestClock => _roadTestClockText.Length > 0;

    /// <summary>
    /// True while the road test's photo mission is up: the companion offers the
    /// PHOTOSHOOT button that takes the shot wherever the car is parked.
    /// </summary>
    public bool HasPhotoshoot => _photoshoot;
    private bool _photoshoot;

    /// <summary>"PHOTOSHOOT"; bindable so a language switch repaints it.</summary>
    public string PhotoshootText
    {
        get => _photoshootText;
        private set => SetField(ref _photoshootText, value);
    }
    private string _photoshootText = Strings.Get("HudPhotoshoot");

    /// <summary>Takes the photo-mission shot; the host prices and banks it.</summary>
    public RelayCommand PhotoshootCommand { get; }

    /// <summary>
    /// Raised when the player on the photo mission asks for the shot. The host
    /// owns what it means, exactly like the arrival's ENTER.
    /// </summary>
    public event EventHandler? PhotoshootRequested;

    /// <summary>
    /// True once the walk's closing car page has handed the errand over. The
    /// window binds its visibility to this so the intro pages own the screen
    /// first.
    /// </summary>
    public bool IsRevealed
    {
        get => _isRevealed;
        private set => SetField(ref _isRevealed, value);
    }

    /// <summary>True while the destination is pinned; hides the top block otherwise.</summary>
    public bool HasDestination
    {
        get => _hasDestination;
        private set => SetField(ref _hasDestination, value);
    }

    /// <summary>The unit the readouts are in; follows the settings page live.</summary>
    public MileageUnit Units => _preferences.Units;

    /// <summary>
    /// True while the live car is not the garage's current car. The career
    /// warning takes the destination line's place; the street and mileage readouts
    /// below keep updating from the live poll.
    /// </summary>
    public bool WrongCar
    {
        get => _wrongCar;
        private set
        {
            if (!SetField(ref _wrongCar, value))
            {
                return;
            }

            RepaintBanner();
        }
    }

    /// <summary>
    /// The warning when the wrong car is driven, else the destination line. The
    /// HUD's top line binds this one property so both states share its
    /// position and font.
    /// </summary>
    public string BannerText =>
        WrongCar ? _wrongCarWarning : DestinationText;

    /// <summary>True while the warning or a destination has a top line.</summary>
    public bool HasBanner => WrongCar || HasDestination;

    /// <summary>
    /// True while the car is parked on the destination house. The drive-to
    /// sentence swaps for the arrival sentence and the ENTER button; walking
    /// in consumes the guidance, driving off swaps both back.
    /// </summary>
    public bool AtDestination
    {
        get => _atDestination;
        private set
        {
            if (!SetField(ref _atDestination, value))
            {
                return;
            }

            if (!value)
            {
                EnterFlash = false;
                // The car left the door: the next arrival invites ENTER again.
                SetEntered(false);
            }
            OnPropertyChanged(nameof(ShowEnterButton));
            OnPropertyChanged(nameof(ParkedAtTarget));
            RepaintDestination();
        }
    }

    /// <summary>
    /// The arrival state under its job name: a leg is "parked at the target"
    /// for the flow that pinned it. The Cento Miglia race reads it to open the
    /// registration page at the start and to finish at the finish.
    /// </summary>
    public bool ParkedAtTarget => AtDestination;

    /// <summary>
    /// True while the task text is up: the drive-to/arrival line, the distance
    /// and the action button. Entering the pinned door consumes the arrival,
    /// so the block hides until a new destination is pinned or the car parks
    /// there again; a customer request swaps it out on its own.
    /// </summary>
    public bool HasTaskText => !_entered;

    // WHY the label check: a Cento Miglia leg carries no action (the start
    // opens its own page, the finish settles itself), so the arrival must not
    // paint an empty button the player can press for nothing.
    public bool ShowEnterButton => AtDestination && !_entered && _enterText.Length > 0;

    /// <summary>
    /// The host entered the pinned door (a shop interior, a story dialogue or
    /// an arrival with nothing to open). The task text and the action button
    /// have done their job and hide until the next arrival or a new leg.
    /// </summary>
    public void DestinationEntered() => SetEntered(true);

    /// <summary>Teal voice acknowledgment on the ENTER button for one beat.</summary>
    public bool EnterFlash
    {
        get => _enterFlash;
        private set => SetField(ref _enterFlash, value);
    }

    /// <summary>Reveals the companion after the introduction has finished.</summary>
    public void Reveal() => IsRevealed = true;

    /// <summary>
    /// The customer request the companion is presenting, or null. The view
    /// swaps its whole content for this while it is set.
    /// </summary>
    public CustomerRequestScreenViewModel? Request => _request;

    /// <summary>True while the customer request overlay owns the companion.</summary>
    public bool HasRequest => _request is not null;

    /// <summary>The normal task text is hidden while the request is up.</summary>
    public bool HasNoRequest => _request is null;

    /// <summary>
    /// Shows the customer request in place of the task text. The request's own
    /// options and BACK run their commands; the host clears it once the
    /// outcome is reported.
    /// </summary>
    public void ShowRequest(CustomerRequestScreenViewModel request)
    {
        _request = request;
        RaiseRequestChanged();
    }

    /// <summary>Clears the request overlay, restoring the task text.</summary>
    public void ClearRequest()
    {
        if (_request is null)
        {
            return;
        }

        _request = null;
        RaiseRequestChanged();
    }

    IVoiceScreen? ITelemetryHud.RequestScreen => _request;

    public void Update(
        TelemetrySample? sample, double? odometerKm, bool gameRunning,
        bool wrongCar = false) =>
        Refresh(sample, odometerKm, gameRunning, wrongCar, force: false);

    /// <summary>
    /// Adopts the new unit/language and repaints every readout at once: the
    /// first-run page can flip both while the HUD is already on screen.
    /// </summary>
    public void ApplyPreferences(PlayerPreferences preferences)
    {
        _preferences = preferences;
        MileageLabel = Strings.Get("HudMileageLabel");
        AddressLabel = Strings.Get("HudAddressLabel");
        _wrongCarWarning = Strings.Get("HudWrongCar");
        PhotoshootText = Strings.Get("HudPhotoshoot");
        // A notice is already-formatted host copy; a language switch would
        // leave it stale, so drop it with the old strings.
        SetNoticeText(string.Empty);
        RepaintDestination();
        RepaintReputation();
        // The road-test sentence names the target speed in the player's unit,
        // so a unit flip mid-run repaints it like the mileage readout.
        if (_roadTest is not null)
        {
            RoadTestTaskText = FormatRoadTestTask(_roadTestTaskKey, _roadTestTargetKmh);
        }
        Refresh(_lastSample, _lastOdometerKm, _lastGameRunning, _lastWrongCar, force: true);
    }

    /// <summary>
    /// Shows the destination the story hands over, or hides the line again
    /// when <paramref name="destination"/> is null. <paramref name="copy"/> is
    /// an optional story-authored set of resource keys (drive-to, arrival and
    /// action) that replaces the generic wording, e.g. the childhood-friend,
    /// pancakes or home lines.
    /// </summary>
    public void SetDestination(PointOfInterest? destination, HudDestinationCopy? copy = null)
    {
        _destination = destination;
        _copy = copy;
        // A fresh leg has not been entered yet, even when the previous door
        // was; the guidance just changed, so its text comes back.
        SetEntered(false);
        // A taxi leg already pinned keeps the banner: the job outranks a story
        // destination until it settles.
        HasDestination = destination is not null || _jobGuidance is not null;
        AtDestination = false;
        RepaintDestination();
        // The distance line follows whatever the banner now shows (a pinned
        // intro leg included), so repaint it from the last live sample without
        // waiting for the next poll.
        _shownLegDistance = long.MinValue;
        UpdateLegDistance(_lastSample, force: true);
    }

    /// <summary>
    /// Raised when a page asks the companion to surface its task text while
    /// the main panel is still open. The window owns visibility; the HUD only
    /// reports the request.
    /// </summary>
    public event EventHandler? TaskTextRequested;

    /// <inheritdoc />
    public void ShowTaskText() => TaskTextRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The door the banner is guiding the player to, or null.</summary>
    public PointOfInterest? CurrentDestination => _destination;

    /// <summary>
    /// Pins the taxi leg the job is on, or clears it with null when the job
    /// settles. The leg owns the banner until then; the story destination is
    /// kept underneath and comes back when the leg clears.
    /// </summary>
    public void SetJobGuidance(HudJobGuidance? guidance)
    {
        _jobGuidance = guidance;
        // A new leg is never already entered: its action button must come back.
        SetEntered(false);
        _shownLegDistance = long.MinValue;
        _shownDeadlineSeconds = long.MinValue;
        SetLegDistanceText(string.Empty);
        SetDeadlineText(string.Empty, late: false);
        HasDestination = guidance is not null || _destination is not null;
        AtDestination = false;
        RepaintDestination();
        UpdateLegDistance(_lastSample, force: true);
        UpdateDeadline(force: true);
    }

    /// <summary>
    /// Shows the live club reputation on its own line (the Cento Miglia race
    /// counter), or takes the line down with null. Only the 5-decimal reading
    /// is compared, so the race's 10 Hz update formats once per displayed
    /// change, never once per sample. The banner is left alone: the finish leg
    /// keeps telling the player where to drive.
    /// </summary>
    public void SetReputation(double? reputation)
    {
        bool wasUp = HasReputation;
        if (reputation is not { } value)
        {
            if (!wasUp)
            {
                return;
            }

            _reputation = null;
            _shownReputation = long.MinValue;
            ReputationText = string.Empty;
        }
        else
        {
            if (!double.IsFinite(value))
            {
                return;
            }

            long shown = (long)Math.Round(value * 100000.0, MidpointRounding.AwayFromZero);
            if (shown == _shownReputation)
            {
                return;
            }

            _shownReputation = shown;
            // WHY the rounded reading: it is exactly the precision the counter
            // prints, so the guard and the painted digits can never disagree.
            _reputation = shown / 100000.0;
            RepaintReputation();
        }

        if (wasUp != HasReputation)
        {
            OnPropertyChanged(nameof(HasReputation));
        }
    }

    /// <summary>
    /// Shows the road test's live block (the task sentence and the countdown or
    /// running time), or clears it with null. The road-test flow owns the
    /// clock: it hands one reading per poll while the leg runs, and the HUD
    /// repaints only the reading that moved. The task sentence is formatted
    /// here from its stored key so the unit and language switch still repaint.
    /// </summary>
    public void SetRoadTest(HudRoadTestReadout? readout)
    {
        bool wasShown = _roadTest is not null;
        bool hadPhotoshoot = _photoshoot;
        string taskKey = readout?.TaskKey ?? string.Empty;
        double targetKmh = readout?.TargetKmh ?? 0.0;
        _roadTest = readout;
        _photoshoot = readout?.Photoshoot ?? false;
        if (!string.Equals(taskKey, _roadTestTaskKey, StringComparison.Ordinal)
            || targetKmh != _roadTestTargetKmh)
        {
            _roadTestTaskKey = taskKey;
            _roadTestTargetKmh = targetKmh;
            RoadTestTaskText = taskKey.Length > 0
                ? FormatRoadTestTask(taskKey, targetKmh)
                : string.Empty;
        }

        RoadTestClockText = readout?.ClockText ?? string.Empty;
        if (hadPhotoshoot != _photoshoot)
        {
            OnPropertyChanged(nameof(HasPhotoshoot));
        }
        if (wasShown == (_roadTest is not null))
        {
            return;
        }

        OnPropertyChanged(nameof(HasRoadTest));
        OnPropertyChanged(nameof(ShowBanner));
    }

    /// <summary>
    /// Shows a transient line under the banner. The host formats the copy; the
    /// HUD only owns its expiry, on the clock rather than a tick count.
    /// </summary>
    public void ShowNotice(string text, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _noticeUntil = duration > TimeSpan.Zero
            ? _clock.UtcNow + duration
            : _clock.UtcNow;
        SetNoticeText(text);
    }

    /// <summary>
    /// Voice activation: glows teal for the shared beat, then acts. The
    /// flash survives the wait so the acknowledgment is visible before any
    /// future screen change. A click bypasses the wait (see EnterCommand).
    /// </summary>
    public void RequestEnter()
    {
        if (!ShowEnterButton)
        {
            return;
        }

        FlashEnter();
        _beat.Defer(() =>
        {
            EnterFlash = false;
            RaiseEnter();
        });
    }

    /// <summary>
    /// Raised when the player taps ENTER while parked on the destination. The
    /// host owns what entering means; the HUD only knows the arrival state.
    /// </summary>
    public event EventHandler? EnterRequested;

    /// <summary>
    /// Raised when the player taps ENTER while parked at the pinned job door.
    /// The host confirms the pickup or the drop-off from the job phase.
    /// </summary>
    public event EventHandler? JobEnterRequested;

    // One latch drives both halves of the task text: the guidance block and
    // its action button hide together when the player walks in, so a visit
    // never keeps inviting an ENTER it already consumed.
    private void SetEntered(bool entered)
    {
        if (_entered == entered)
        {
            return;
        }

        _entered = entered;
        OnPropertyChanged(nameof(HasTaskText));
        OnPropertyChanged(nameof(ShowEnterButton));
    }

    // BannerText, HasBanner and ShowBanner are one state machine (the warning
    // outranks the destination, the road-test block outranks both), so every
    // input change repaints through here instead of each setter raising the
    // trio on its own.
    private void RepaintBanner()
    {
        OnPropertyChanged(nameof(BannerText));
        OnPropertyChanged(nameof(HasBanner));
        OnPropertyChanged(nameof(ShowBanner));
    }

    // The road test's sentence: the target is the leg's own speed (60 for the
    // accel run, 100 for the brake run) in the player's unit, so a miles player
    // reads "0-37 mph" for the same 60 km/h the clock measures (cars.json's
    // Acceleration column stays the metric one). A zero target names a line
    // with no placeholders, like the halt ask.
    private string FormatRoadTestTask(string key, double targetKmh) =>
        targetKmh > 0.0
            ? Strings.Format(
                key,
                RoadTestRules.DisplaySpeed(targetKmh, _preferences.Units),
                Strings.Get(_preferences.Units == MileageUnit.Miles
                    ? "RoadTestUnitMph"
                    : "RoadTestUnitKmh"))
            : Strings.Get(key);

    // The counter is a sentence with the race's number in it, so a language
    // switch repaints it from the stored reading like the banner's copy.
    private void RepaintReputation()
    {
        ReputationText = _reputation is { } reading
            ? Strings.Format("HudCentoReputation", reading)
            : string.Empty;
    }

    // WHY two templates per state: a shop invites the player in for the errand,
    // while a friend's house only has an address for now (no interior until the
    // pickup slice), so the drive-to line carries the story hint and the
    // arrival line stays a plain arrival with a PICK UP action.
    private void RepaintDestination()
    {
        if (_jobGuidance is { } job)
        {
            DestinationText = AtDestination
                ? Strings.Format(job.ArrivedKey, Args(job.ArrivedArgs, job.TargetName))
                : Strings.Format(
                    job.DriveKey,
                    Args(job.DriveArgs, job.TargetName, job.Target.StreetName,
                        job.Target.HouseNumber, _map.City));
            SetAction(job.ActionKey);
        }
        else if (_destination is { } poi)
        {
            bool person = poi.Type == PoiTypes.Person;
            // An empty key is the story saying the arrival is only its button:
            // the valuation venue asks to enter the showcase, not to park at it.
            string arrivedKey = _copy?.ArrivedKey
                ?? (person ? "HudArrivedPerson" : "HudArrived");
            DestinationText = AtDestination
                ? arrivedKey.Length > 0
                    ? Strings.Format(arrivedKey, Args(_copy?.ArrivedArgs, poi.Name))
                    : string.Empty
                : DriveTo(poi);
            SetAction(_copy?.ActionKey ?? (person ? "HudPickUp" : "HudEnter"));
        }
        else
        {
            DestinationText = string.Empty;
            SetAction(string.Empty);
        }

        RepaintBanner();
    }

    // The values one drive/arrival key formats: the leg's own list when the
    // flow supplied one (the Touristenfahrten order and dropoff names), the
    // shared address shape otherwise. Allocates only on a repaint (a pinned
    // leg or an arrival edge), never per tick.
    private static object?[] Args(
        IReadOnlyList<string>? supplied, params object?[] fallback)
    {
        if (supplied is null)
        {
            return fallback;
        }

        var values = new object?[supplied.Count];
        for (int i = 0; i < supplied.Count; i++)
        {
            values[i] = supplied[i];
        }

        return values;
    }

    // Label and spoken words come from one action key: the label localizes,
    // the keywords stay the English words Vosk hears. An empty key means no
    // button is up, so nothing can fire.
    private void SetAction(string actionKey)
    {
        EnterText = actionKey.Length > 0 ? Strings.Get(actionKey) : string.Empty;
        _enterKeywords = HudVoiceKeywords.For(actionKey);
    }

    /// The hint replaces the generic drive-to wording (not appended): a story
    /// leg reads as one authored sentence, with the same placeholders so a
    /// translator keeps the address regardless of word order.
    private string DriveTo(PointOfInterest poi) =>
        Strings.Format(
            _copy?.DriveKey ?? "HudDestination",
            Args(_copy?.DriveArgs, poi.Name,
                poi.Address.StreetName, poi.Address.HouseNumber, _map.City));

    private void Enter()
    {
        if (!ShowEnterButton)
        {
            return;
        }

        // A click is newer input than a pending voice tap: drop the deferred
        // action so one arrival cannot enter twice.
        _beat.Cancel();
        FlashEnter();
        RaiseEnter();
    }

    // The photo mission's shutter. A click is newer input than a pending voice
    // tap here too, so the deferred acknowledgment goes. The host decides
    // whether the shot can be priced (a missing position keeps the mission up).
    private void Photoshoot()
    {
        if (!HasPhotoshoot)
        {
            return;
        }

        _beat.Cancel();
        PhotoshootRequested?.Invoke(this, EventArgs.Empty);
    }

    // The pinned job leg owns ENTER while it is up; otherwise the story
    // destination does. One arrival can only mean one of the two.
    private void RaiseEnter()
    {
        if (_jobGuidance is not null)
        {
            JobEnterRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        EnterRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FlashEnter()
    {
        EnterFlash = true;
        _enterFlashUntil = _beat.Deadline();
    }

    private void UpdateFlash()
    {
        if (_enterFlash && _beat.IsDue(_enterFlashUntil))
        {
            EnterFlash = false;
        }
    }

    private void Refresh(
        TelemetrySample? sample, double? odometerKm, bool gameRunning,
        bool wrongCar, bool force)
    {
        _lastSample = sample;
        _lastOdometerKm = odometerKm;
        _lastGameRunning = gameRunning;
        _lastWrongCar = wrongCar;

        UpdateFlash();
        UpdateNotice();
        // The warning describes the live car, so a dropped sample clears it
        // rather than leaving a stale mismatch while EVO is closed.
        WrongCar = wrongCar && sample is not null;
        AddressLocation? location = Resolve(sample);
        UpdateMileage(odometerKm, force);
        UpdateAddress(location, force);
        UpdateArrival(sample, location);
        UpdateLegDistance(sample, force);
        UpdateDeadline(force);

        if (sample is null)
        {
            _headerLive = false;
            _lastTrack = null;
            HeaderText = Strings.Get(gameRunning ? "EvoNoShm" : "WaitingForEvo");
            return;
        }

        if (force || !_headerLive || !Equals(_lastTrack, sample.Track))
        {
            _headerLive = true;
            _lastTrack = sample.Track;
            HeaderText = FormatTrack(sample.Track);
        }
    }

    private AddressLocation? Resolve(TelemetrySample? sample) =>
        sample?.NormalizedPosition is { } position
            ? AddressResolver.Resolve(position, sample.Track?.LengthMeters ?? 0.0, _map)
            : null;

    private void UpdateArrival(TelemetrySample? sample, AddressLocation? location)
    {
        bool arrived;
        if (_jobGuidance is { } job)
        {
            arrived = sample is { } live
                && Math.Abs(live.SpeedKmh) <= StationarySpeedKmh
                && IsAtJobTarget(job, live);
        }
        else
        {
            arrived = _destination is { } poi
                && location is { } resolved
                && resolved.HouseNumber == poi.Address.HouseNumber
                && string.Equals(resolved.StreetName, poi.Address.StreetName, StringComparison.Ordinal)
                && sample is { } live
                && Math.Abs(live.SpeedKmh) <= StationarySpeedKmh;
        }

        // SetField inside AtDestination filters the 10 Hz repeat, so only the
        // parked/driving edge repaints.
        AtDestination = arrived;
    }

    // WHY the radius rule: the job door only has to be inside the taxi rules'
    // radius, unlike the exact-house story arrival. The closed lap puts a door
    // just behind the line next to a car just before it.
    private bool IsAtJobTarget(HudJobGuidance job, TelemetrySample live)
    {
        if (live.NormalizedPosition is not { } position)
        {
            return false;
        }

        double length = live.Track?.LengthMeters ?? _map.TrackLengthMeters;
        double target = AddressBook.LapOffsetMeters(_map, job.Target, length);
        return LapGeometry.IsArrived(position, target, length, job.ArrivalRadiusMeters);
    }

    // The banner's leg owns the distance line too: the pinned job outranks a
    // story destination, exactly like RepaintDestination.
    private AddressLocation? PinnedTarget =>
        _jobGuidance?.Target ?? _destination?.Address;

    // The distance line only repaints when the shown step moves: metres below
    // a kilometre (rules step), then tenths of a kilometre. The lookup and the
    // arithmetic are cheap enough for the 10 Hz poll; the format is not. It
    // serves every leg kind — a taxi or delivery job and an intro drive — so
    // the driver always reads how far the pinned door is.
    private void UpdateLegDistance(TelemetrySample? sample, bool force)
    {
        if (PinnedTarget is not { } target
            || sample?.NormalizedPosition is not { } position)
        {
            _shownLegDistance = long.MinValue;
            SetLegDistanceText(string.Empty);
            return;
        }

        double length = sample.Track?.LengthMeters ?? _map.TrackLengthMeters;
        double targetOffset = AddressBook.LapOffsetMeters(_map, target, length);
        double distance = LapGeometry.DistanceMeters(
            LapGeometry.OffsetMeters(position, length), targetOffset, length);
        if (!double.IsFinite(distance))
        {
            _shownLegDistance = long.MinValue;
            SetLegDistanceText(string.Empty);
            return;
        }

        bool inKm = distance >= 1000.0;
        long key = inKm
            ? (long)Math.Floor(distance / 100.0)
            : (long)Math.Floor(distance / _distanceStepMeters);
        if (!force && key == _shownLegDistance && inKm == _legDistanceInKm)
        {
            return;
        }

        _shownLegDistance = key;
        _legDistanceInKm = inKm;
        SetLegDistanceText(inKm
            ? Strings.Format("HudJobDistanceKm", key / 10.0)
            : Strings.Format("HudJobDistanceMeters", key * _distanceStepMeters));
    }

    // The countdown repaints only when the displayed second moves, so the
    // 10 Hz poll costs one subtraction and one comparison per tick. The label
    // key belongs to the guidance: the same clock counts a speed challenge or
    // an accepted passenger's hurry.
    private void UpdateDeadline(bool force)
    {
        if (_jobGuidance?.Deadline is not { } deadline)
        {
            _shownDeadlineSeconds = long.MinValue;
            SetDeadlineText(string.Empty, late: false);
            return;
        }

        double left = (deadline - _clock.UtcNow).TotalSeconds;
        bool late = left < 0.0;
        long seconds = late ? 0 : (long)Math.Ceiling(left);
        if (!force && seconds == _shownDeadlineSeconds && late == _deadlineLate)
        {
            return;
        }

        _shownDeadlineSeconds = seconds;
        SetDeadlineText(
            late
                ? Strings.Get("HudDeadlineLate")
                : Strings.Format(_jobGuidance.DeadlineKey, ClockText(seconds)),
            late);
    }

    private static string ClockText(long seconds) =>
        $"{seconds / 60}:{seconds % 60:00}";

    private void UpdateNotice()
    {
        if (_noticeText.Length > 0 && _clock.UtcNow >= _noticeUntil)
        {
            SetNoticeText(string.Empty);
        }
    }

    private void SetLegDistanceText(string value)
    {
        if (!SetField(ref _legDistanceText, value, nameof(LegDistanceText)))
        {
            return;
        }

        OnPropertyChanged(nameof(HudLineText));
        OnPropertyChanged(nameof(HasHudLine));
    }

    private void SetDeadlineText(string value, bool late)
    {
        bool changed = SetField(
            ref _deadlineText, value, nameof(DeadlineText));
        DeadlineLate = late;
        if (changed)
        {
            OnPropertyChanged(nameof(HasDeadline));
        }
    }

    private void SetNoticeText(string value)
    {
        if (!SetField(ref _noticeText, value, nameof(NoticeText)))
        {
            return;
        }

        OnPropertyChanged(nameof(HasNotice));
        OnPropertyChanged(nameof(HudLineText));
        OnPropertyChanged(nameof(HasHudLine));
    }

    // The view listens for both the content swap and the frame change, so one
    // notify per edge keeps them in step.
    private void RaiseRequestChanged()
    {
        OnPropertyChanged(nameof(Request));
        OnPropertyChanged(nameof(HasRequest));
        OnPropertyChanged(nameof(HasNoRequest));
    }

    private void UpdateMileage(double? odometerKm, bool force)
    {
        if (odometerKm is not { } km || !double.IsFinite(km) || km < 0.0)
        {
            // null is both "no car" and "already showing it"; repaint on the
            // car -> no-car edge, or when a preference change asks for it.
            if (force || _shownTenths is not null)
            {
                _shownTenths = null;
                MileageText = Strings.Get("HudNoCar");
            }
            return;
        }

        long tenths = (long)Math.Floor(Mileage.FromKilometres(km, _preferences.Units) * 10.0);
        if (!force && _shownTenths == tenths)
        {
            return;
        }
        _shownTenths = tenths;
        // WHY /10.0: formatting the floored tenths keeps the string stable
        // while the live value moves within a tenth of the player's unit.
        MileageText = Strings.Format("HudMileage", tenths / 10.0);
    }

    private void UpdateAddress(AddressLocation? location, bool force)
    {
        if (location is not { } resolved)
        {
            if (force || _addressShown)
            {
                _addressShown = false;
                AddressText = Strings.Get("HudNoPosition");
            }
            return;
        }

        if (!force
            && _addressShown
            && resolved.HouseNumber == _houseNumber
            && string.Equals(resolved.StreetName, _street, StringComparison.Ordinal))
        {
            return;
        }

        _addressShown = true;
        _street = resolved.StreetName;
        _houseNumber = resolved.HouseNumber;
        AddressText = Strings.Format(
            "HudAddressWithCity", AddressFormatter.Format(resolved), _map.City);
    }

    private static string FormatTrack(TrackInfo? track)
    {
        if (track is not { Name.Length: > 0 } info)
        {
            return Strings.Get("HudNoTrack");
        }
        return info.Configuration.Length > 0
            ? Strings.Format("HudTrack", info.Name, info.Configuration)
            : info.Name;
    }
}
