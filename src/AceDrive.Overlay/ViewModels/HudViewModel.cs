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
    private string _wrongCarWarning = Strings.Get("HudWrongCar");

    // The destination itself, kept so a language switch can repaint the line
    // without the ViewModel pushing it again. The copy is a set of resource
    // keys the story hands over, never sentences, so it repaints with the rest.
    private PointOfInterest? _destination;
    private HudDestinationCopy? _copy;
    // The active taxi leg, when any: it replaces the story destination in the
    // banner until the job settles.
    private HudJobGuidance? _jobGuidance;
    private bool _hasDestination;
    private bool _atDestination;
    private bool _wrongCar;
    private bool _isRevealed;
    // The customer request currently presented on the companion, if any. The
    // host creates it (it owns the countdown and the outcome); the HUD only
    // paints it in place of the task text and lets its buttons run.
    private CustomerRequestScreenViewModel? _request;

    // The line under the banner: the remaining job distance, or a transient
    // host notice (the notice wins). Both are empty outside a job.
    private string _jobDistanceText = string.Empty;
    private string _noticeText = string.Empty;
    private DateTime _noticeUntil;
    // The quantised distance already painted, so the 10 Hz poll only formats
    // when the displayed step moves. Sentinels force the first paint.
    private long _shownJobDistance = long.MinValue;
    private bool _jobDistanceInKm;

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
    /// Remaining distance on the pinned job leg ("120 m"/"1.4 km"), empty when
    /// no leg is up or the live sample has no track position.
    /// </summary>
    public string JobDistanceText => _jobDistanceText;

    /// <summary>A transient host notice (police fine, refusal hint); empty when none.</summary>
    public string NoticeText => _noticeText;

    public bool HasNotice => _noticeText.Length > 0;

    /// <summary>The line under the banner: the notice wins over the distance.</summary>
    public string HudLineText => HasNotice ? _noticeText : _jobDistanceText;

    public bool HasHudLine => HudLineText.Length > 0;

    /// <summary>
    /// True once the birthday dialogue has handed the errand over. The window
    /// binds its visibility to this so the intro pages own the screen first.
    /// </summary>
    public bool IsRevealed
    {
        get => _isRevealed;
        private set => SetField(ref _isRevealed, value);
    }

    /// <summary>True while a destination is pinned; hides the top block otherwise.</summary>
    public bool HasDestination
    {
        get => _hasDestination;
        private set => SetField(ref _hasDestination, value);
    }

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
    /// sentence swaps for the arrival sentence and the ENTER button; driving
    /// off swaps both back.
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

            OnPropertyChanged(nameof(ShowEnterButton));
            if (!value)
            {
                EnterFlash = false;
            }
            RepaintDestination();
        }
    }

    public bool ShowEnterButton => AtDestination;

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
        // A notice is already-formatted host copy; a language switch would
        // leave it stale, so drop it with the old strings.
        SetNoticeText(string.Empty);
        RepaintDestination();
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
        // A taxi leg already pinned keeps the banner: the job outranks a story
        // destination until it settles.
        HasDestination = destination is not null || _jobGuidance is not null;
        AtDestination = false;
        RepaintDestination();
    }

    /// <summary>
    /// Pins the taxi leg the job is on, or clears it with null when the job
    /// settles. The leg owns the banner until then; the story destination is
    /// kept underneath and comes back when the leg clears.
    /// </summary>
    public void SetJobGuidance(HudJobGuidance? guidance)
    {
        _jobGuidance = guidance;
        _shownJobDistance = long.MinValue;
        SetJobDistanceText(string.Empty);
        HasDestination = guidance is not null || _destination is not null;
        AtDestination = false;
        RepaintDestination();
        UpdateJobDistance(_lastSample, force: true);
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
        if (!AtDestination)
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

    // BannerText and HasBanner are one state machine (warning > destination), so
    // every input change repaints through here instead of each setter raising
    // the pair on its own.
    private void RepaintBanner()
    {
        OnPropertyChanged(nameof(BannerText));
        OnPropertyChanged(nameof(HasBanner));
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
                ? Strings.Format(job.ArrivedKey, job.TargetName)
                : Strings.Format(
                    job.DriveKey, job.TargetName, job.Target.StreetName,
                    job.Target.HouseNumber, _map.City);
            EnterText = Strings.Get(job.ActionKey);
        }
        else if (_destination is { } poi)
        {
            bool person = poi.Type == PoiTypes.Person;
            string arrivedKey = _copy?.ArrivedKey
                ?? (person ? "HudArrivedPerson" : "HudArrived");
            string actionKey = _copy?.ActionKey
                ?? (person ? "HudPickUp" : "HudEnter");
            DestinationText = AtDestination
                ? Strings.Format(arrivedKey, poi.Name)
                : DriveTo(poi);
            EnterText = Strings.Get(actionKey);
        }
        else
        {
            DestinationText = string.Empty;
            EnterText = string.Empty;
        }

        RepaintBanner();
    }

    /// The hint replaces the generic drive-to wording (not appended): a story
    /// leg reads as one authored sentence, with the same placeholders so a
    /// translator keeps the address regardless of word order.
    private string DriveTo(PointOfInterest poi) =>
        Strings.Format(
            _copy?.DriveKey ?? "HudDestination",
            poi.Name,
            poi.Address.StreetName,
            poi.Address.HouseNumber,
            _map.City);

    private void Enter()
    {
        if (!AtDestination)
        {
            return;
        }

        // A click is newer input than a pending voice tap: drop the deferred
        // action so one arrival cannot enter twice.
        _beat.Cancel();
        FlashEnter();
        RaiseEnter();
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
        UpdateJobDistance(sample, force);

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

    // The distance line only repaints when the shown step moves: metres below
    // a kilometre (rules step), then tenths of a kilometre. The lookup and the
    // arithmetic are cheap enough for the 10 Hz poll; the format is not.
    private void UpdateJobDistance(TelemetrySample? sample, bool force)
    {
        if (_jobGuidance is not { } job
            || sample?.NormalizedPosition is not { } position)
        {
            _shownJobDistance = long.MinValue;
            SetJobDistanceText(string.Empty);
            return;
        }

        double length = sample.Track?.LengthMeters ?? _map.TrackLengthMeters;
        double target = AddressBook.LapOffsetMeters(_map, job.Target, length);
        double distance = LapGeometry.DistanceMeters(
            LapGeometry.OffsetMeters(position, length), target, length);
        if (!double.IsFinite(distance))
        {
            _shownJobDistance = long.MinValue;
            SetJobDistanceText(string.Empty);
            return;
        }

        bool inKm = distance >= 1000.0;
        long key = inKm
            ? (long)Math.Floor(distance / 100.0)
            : (long)Math.Floor(distance / _distanceStepMeters);
        if (!force && key == _shownJobDistance && inKm == _jobDistanceInKm)
        {
            return;
        }

        _shownJobDistance = key;
        _jobDistanceInKm = inKm;
        SetJobDistanceText(inKm
            ? Strings.Format("HudJobDistanceKm", key / 10.0)
            : Strings.Format("HudJobDistanceMeters", key * _distanceStepMeters));
    }

    private void UpdateNotice()
    {
        if (_noticeText.Length > 0 && _clock.UtcNow >= _noticeUntil)
        {
            SetNoticeText(string.Empty);
        }
    }

    private void SetJobDistanceText(string value)
    {
        if (!SetField(ref _jobDistanceText, value, nameof(JobDistanceText)))
        {
            return;
        }

        OnPropertyChanged(nameof(HudLineText));
        OnPropertyChanged(nameof(HasHudLine));
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
