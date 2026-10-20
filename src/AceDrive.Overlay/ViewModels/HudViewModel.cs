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

    private PlayerPreferences _preferences;

    private string _headerText = Strings.Get("WaitingForEvo");
    private string _mileageText = Strings.Get("HudNoCar");
    private string _addressText = Strings.Get("HudNoPosition");
    private string _mileageLabel = Strings.Get("HudMileageLabel");
    private string _addressLabel = Strings.Get("HudAddressLabel");
    private string _destinationText = string.Empty;
    private string _enterText = string.Empty;
    private string _wrongCarWarning = Strings.Get("HudWrongCar");

    // The errand itself, kept so a language switch can repaint the line
    // without the ViewModel pushing it again.
    private PointOfInterest? _destination;
    private bool _hasDestination;
    private bool _atDestination;
    private bool _wrongCar;
    private bool _isRevealed;

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

    public HudViewModel(AddressMap map, PlayerPreferences preferences, VoiceBeat beat)
    {
        _map = map;
        _preferences = preferences;
        _beat = beat;
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
    /// empty with no errand.
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

    /// <summary>"ENTER"/"BINNENGAAN"; empty with no errand.</summary>
    public string EnterText
    {
        get => _enterText;
        private set => SetField(ref _enterText, value);
    }

    /// <summary>Tap handler for the arrival button; the host decides what entering means.</summary>
    public RelayCommand EnterCommand { get; }

    /// <summary>
    /// True once the birthday dialogue has handed the errand over. The window
    /// binds its visibility to this so the intro pages own the screen first.
    /// </summary>
    public bool IsRevealed
    {
        get => _isRevealed;
        private set => SetField(ref _isRevealed, value);
    }

    /// <summary>True while an errand destination is pinned; hides the top block otherwise.</summary>
    public bool HasDestination
    {
        get => _hasDestination;
        private set
        {
            if (SetField(ref _hasDestination, value))
            {
                OnPropertyChanged(nameof(HasBanner));
            }
        }
    }

    /// <summary>
    /// True while the live car is not the garage's current car. The career
    /// warning takes the errand line's place; the street and mileage readouts
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

            OnPropertyChanged(nameof(BannerText));
            OnPropertyChanged(nameof(HasBanner));
        }
    }

    /// <summary>
    /// The warning when the wrong car is driven, else the errand sentence.
    /// The HUD's top line binds this one property so both states share its
    /// position and font.
    /// </summary>
    public string BannerText => WrongCar ? _wrongCarWarning : DestinationText;

    /// <summary>True while either the warning or an errand has a top line.</summary>
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
        OnPropertyChanged(nameof(BannerText));
        RepaintDestination();
        Refresh(_lastSample, _lastOdometerKm, _lastGameRunning, _lastWrongCar, force: true);
    }

    /// <summary>
    /// Shows the errand handed over by the birthday dialogue, or hides the
    /// line again when <paramref name="destination"/> is null.
    /// </summary>
    public void SetDestination(PointOfInterest? destination)
    {
        _destination = destination;
        HasDestination = destination is not null;
        AtDestination = false;
        RepaintDestination();
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
            EnterRequested?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>
    /// Raised when the player taps ENTER while parked on the destination. The
    /// host owns what entering means; the HUD only knows the arrival state.
    /// </summary>
    public event EventHandler? EnterRequested;

    private void RepaintDestination()
    {
        if (_destination is { } poi)
        {
            DestinationText = AtDestination
                ? Strings.Format("HudArrived", poi.Name)
                : Strings.Format(
                    "HudDestination",
                    poi.Name,
                    poi.Address.StreetName,
                    poi.Address.HouseNumber,
                    _map.City);
            EnterText = Strings.Get("HudEnter");
            return;
        }

        DestinationText = string.Empty;
        EnterText = string.Empty;
    }

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
        // The warning describes the live car, so a dropped sample clears it
        // rather than leaving a stale mismatch while EVO is closed.
        WrongCar = wrongCar && sample is not null;
        AddressLocation? location = Resolve(sample);
        UpdateMileage(odometerKm, force);
        UpdateAddress(location, force);
        UpdateArrival(sample, location);

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
        bool arrived = _destination is { } poi
            && location is { } resolved
            && resolved.HouseNumber == poi.Address.HouseNumber
            && string.Equals(resolved.StreetName, poi.Address.StreetName, StringComparison.Ordinal)
            && sample is { } live
            && Math.Abs(live.SpeedKmh) <= StationarySpeedKmh;

        // SetField inside AtDestination filters the 10 Hz repeat, so only the
        // parked/driving edge repaints.
        AtDestination = arrived;
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
