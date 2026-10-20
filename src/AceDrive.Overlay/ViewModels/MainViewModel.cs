using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// All overlay behaviour. Dependencies are constructor-injected (DIP);
/// time, telemetry, logging and fares are abstracted so every flow is
/// unit-testable with fakes. Knows NOTHING about HWNDs or files.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IScreenController, IVoiceInfoNavigator, IDisposable
{
    private readonly ITelemetrySource _telemetry;
    private readonly IClock _clock;
    private readonly VoiceBeat _beat;
    private readonly GameClock _game;
    private readonly RoadTestLedger _roads;
    // Persists the pick so every session reopens the same road-test car
    // until its test is driven (PORTPLAN 49).
    private readonly IRoadTestStore _roadTestStore;
    private readonly TaxiShift _shift;
    private readonly ISettingsStore _store;
    private readonly ILog _log;
    private readonly FareTable _fares;
    private readonly DriverWallet _wallet;
    private readonly IDriverStore _driverStore;
    private readonly DailyStreak _streak;
    private readonly OperatingZone _zone;
    private readonly IFriendshipStore _friends;
    // XP world standings backend (PORTPLAN 60, Unity RivalsService): the
    // receipt's rivals block reads through it on every settle.
    private readonly IRivalsService _rivals;
    // Lifetime km per driven brand behind the receipt's sponsorship rate
    // (PORTPLAN 35, Unity PlayerData.brandDistanceDriven).
    private readonly IBrandMileageStore _brands;
    // The car pool behind the road-test ledger, reused for the receipt's
    // passenger lookup (the driven car's ks id resolves against it).
    private readonly IReadOnlyList<CarSpec> _cars;
    private readonly IOnboardingStore _onboarding;
    private readonly IContentSource _content;
    // Player identity + home address (Career Start Screen 1): the opening
    // screen reads the saved profile, falls back to the Steam display name,
    // and banks the confirmed address in its own file.
    private readonly IProfileStore _profiles;
    private readonly AddressMap _addressMap;
    private readonly string? _detectedPlayerName;
    // The POI behind the birthday errand (PORTPLAN 5/24/27): one random
    // address per walk, filled into the dialogue and pinned on the HUD.
    private readonly PoiGenerator _pois;
    private PointOfInterest? _poi;
    // The typed arrival seam (PORTPLAN 24): the router turns an entered POI
    // into its interior screen, so a new business never edits EnterPoi.
    private readonly PoiArrivalRouter? _poiArrivals;
    // Goods the player bought (PORTPLAN 26), banked with the driver state.
    private readonly Trunk _trunk;
    // The one way a POI interior gets back to the control page.
    private readonly ICommand _controlBackCommand;
    // The first-run choices (distance unit + UI language): the HUD and the
    // resource lookup read them, and they survive a re-run of the intro.
    private readonly IPreferencesStore _preferencesStore;
    private readonly ICultureService _culture;
    // WHY read through on demand: Data/*.json edits (wake aliases, transcript
    // confusions) take effect while the overlay runs; screens built later pick
    // up the new snapshot automatically.
    private OnboardingContent Content => _content.Content;
    private readonly TodoTable _todos;
    private readonly IVoiceListener _voice;
    private readonly ICarBrandSource _carBrands;
    private readonly DeliveryCatalog _deliveryCatalog;
    // The delivery distance-charge table (no forfait/tip/fine): the dropped
    // goods are paid by distance alone (Data/delivery_fares.json).
    private readonly DeliveryFareTable _deliveryFares;
    // CredP balance + owned hubs, shared by the board and the hub page.
    // In-memory until the persistent save lands (PORTPLAN 51).
    private readonly DeliveryCareer _career;
    // The proposal draw behind the taxi page: passenger, second-address
    // resident and the home/residence flip (Data/name_pools.json, Germany
    // pools + Unity suffix list, PORTPLAN 11/13/61).
    private readonly TaxiJobProposer _proposals;
    // The town itself: citizens drift with the shelves, demand empties them,
    // and the size is banked on exit (PORTPLAN 25).
    private readonly CityEconomy _city;
    private readonly ICityStore _cityStore;
    // The appetite: +1 per crossed game hour, banked with the reading for the
    // next session (PORTPLAN 26).
    private readonly Hunger _hunger;
    private readonly IHungerStateStore _hungerStateStore;
    private readonly IGameClockStore _clocks;
    // The owned cars + the car driven now (PORTPLAN item 30). The garage banks
    // its own garage.json: the catalogue row behind each car is the data, the
    // per-car state (odometer, insurance) grows with items 31/32/34.
    private readonly Garage _garage;
    private readonly IGarageStore _garageStore;
    // The insurance table (PORTPLAN 34): the nightly charge rate and the broker
    // page's daily-cost math (Data/insurance_rules.json).
    private readonly InsuranceRules _insuranceRules;
    // The trip-start breakdown roll (PORTPLAN 39): a seeded instance built in
    // the composition root, never the shared global (AGENTS.md).
    private readonly BreakdownStrategy _breakdowns;
    // The 200 px companion: the poll pushes it one snapshot so
    // the HUD never opens its own timer or shared-memory handle.
    private readonly ITelemetryHud? _hud;
    private OnboardingState _progress;
    private PlayerProfile? _profile;
    private PlayerPreferences _preferences;
    private MainControlScreenViewModel? _control;
    private DeliveryJobScreenViewModel? _deliveryScreen;
    private TaxiJobProposalViewModel? _proposal;
    private ParameterizedCommand<TaxiJobState>? _acceptJobAction;
    private ParameterizedCommand<int>? _openDayAction;
    private int _lastDay = -1;
    // Display week: rolls on the Sunday-midnight crossing, persists across
    // restarts via the clock store (seeded from the save at startup).
    private int _displayWeek;
    // Weekday number of the road-test event (Unity: Tuesday), read off the
    // calendar data by RoadTestDayOfWeek, never hardcoded here.
    private readonly int _roadTestDay = RoadTestDayOfWeek();

    // WHY a throttle at all: a rebuild/Stop-Debugging kills the process, and
    // a killed process never raises Window.Closed — so the shutdown save
    // cannot be the only safety net. A game-day flip is ~53 real minutes
    // away, too coarse while playing, so Poll() also banks on wall time.
    // One 51-byte write per interval, never per 10 Hz tick.
    private const double ClockSaveSec = 60.0;
    private DateTime _lastClockSave;

    // Same idea for the odometer: the wear feed writes the current car's
    // distance to garage.json on a wall-time throttle, never per 10 Hz tick.
    // One garage record write per interval; the day flip and the settles bank
    // it too, so a kill costs at most this window of kilometres.
    private const double OdometerSaveSec = 60.0;
    private DateTime _lastGarageSave;

    // WHY a clamp: EVO reports a per-tick speed, and a teleport/hiccup can
    // report an absurd value for one frame; capping the sample keeps a torn or
    // glitched read from banking a bogus jump into the wear ledger.
    private const double MaxTelemetrySpeedKmh = 500.0;

    // The warning fires on the transition into the blocked band (Unity's
    // TOO HUNGRY edge), not per poll and not per hour.
    private bool _hungerBlocked;

    // Companion parity: hidden until "hey {brand}", idle hides after 15 s,
    // one utterance fires one button (muted until the final fragment).
    private const double IdleHideSec = 15.0;
    private const double WakeCooldownSec = 3.0;
    private const string DefaultBrand = "mazda";

    // The HUD ENTER button answers to the one word the English mic model
    // hears reliably, whatever the UI language shows on the button.
    private static readonly string[] EnterKeywords = ["enter"];
    private DateTime _lastActivity;
    private DateTime _lastWake = DateTime.MinValue;
    // The intro (opening screen) is the constructor's first paint; the first
    // hidden -> shown edge must reveal it instead of jumping to control. Every
    // later call keeps the existing "open control" rule.
    private bool _introShown;
    private string? _mutedLabel;
    private Action? _mutedAction;
    private string _mutedText = string.Empty;
    private bool _resizing;

    // Wake brand = the car currently driven in ACE (watcher-published),
    // never the saved favorite.
    private string _carBrand = DefaultBrand;
    // Car identity behind the brand, when the logs announced one: the receipt's
    // passenger lookup resolves against the car pool (one brand spans 1- and
    // 4-seaters, one folder spans showroom variants).
    private CarIdentity? _carIdentity;
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick;
    private bool _disposed;

    private TaxiJobState _job = new();
    // WHICH shown proposal already paid the dirty-car friendship penalty
    // (Unity's _tooDirtyArrived guard): tapping ACCEPT twice on the same
    // proposal must not charge the customer twice. A fresh proposal is a new
    // TaxiJobState instance (Unity's new job).
    private TaxiJobState? _dirtyRefusedJob;
    private int _lastShiftSecs = -1;

    // Dropoff placeholders until the driving slice feeds real inputs: the
    // agreed 8.2 km ride with every extra on except the speeding fine (the
    // receipt keeps its empty fine line, but no fine is charged). Car class
    // has no live source yet either (the log watcher only
    // publishes the brand, never the ks id), so it stays 1 with rarity and
    // street knowledge until active-car tracking lands.
    private const double PlaceholderDistanceKm = 8.2;
    private const int PlaceholderCarClass = 1;
    private const int PlaceholderCarRarity = 1;
    private const int PlaceholderStreetKnowledgePct = 100;
    private const int PlaceholderFines = 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(
        ITelemetrySource telemetry, IClock clock, GameClock game,
        TaxiShift shift, ISettingsStore store,
        ILog log, FareTable fares, TaxiJobProposer proposals,
        IOnboardingStore onboarding, IContentSource content,
        IProfileStore profiles, AddressMap addressMap, string? detectedPlayerName,
        IVoiceListener voice, ICarBrandSource carBrands,
        TodoTable todos, DeliveryCatalog deliveryCatalog, DeliveryCareer career,
        DeliveryFareTable deliveryFares,
        CityEconomy city, ICityStore cityStore,
        RoadTestLedger roadTests, IRoadTestStore roadTestStore,
        IGameClockStore clockStore, int startWeek,
        VoiceBeat beat, DriverWallet wallet, IDriverStore driverStore,
        DailyStreak streak, OperatingZone zone, IFriendshipStore friendships, IReadOnlyList<CarSpec> cars,
        IBrandMileageStore brands, IRivalsService rivals,
        Hunger hunger, IHungerStateStore hungerStore,
        Garage garage, IGarageStore garageStore, InsuranceRules insuranceRules,
        BreakdownStrategy breakdowns,
        PoiGenerator pois,
        IPreferencesStore preferencesStore, PlayerPreferences preferences, ICultureService culture,
        ITelemetryHud? hud = null,
        Trunk? trunk = null,
        PoiArrivalRouter? poiArrivals = null)
    {
        _telemetry = telemetry;
        _clock = clock;
        _beat = beat;
        _game = game;
        _roads = roadTests;
        _roadTestStore = roadTestStore;
        _clocks = clockStore;
        _displayWeek = startWeek;
        _shift = shift;
        _store = store;
        _log = log;
        _fares = fares;
        _proposals = proposals;
        _onboarding = onboarding;
        _content = content;
        _profiles = profiles;
        _addressMap = addressMap;
        _detectedPlayerName = detectedPlayerName;
        _pois = pois;
        _preferencesStore = preferencesStore;
        _preferences = preferences;
        _culture = culture;
        _todos = todos;
        _voice = voice;
        _carBrands = carBrands;
        _deliveryCatalog = deliveryCatalog;
        _deliveryFares = deliveryFares;
        _career = career;
        _city = city;
        _cityStore = cityStore;
        _hunger = hunger;
        _hungerStateStore = hungerStore;
        _garage = garage;
        _garageStore = garageStore;
        _insuranceRules = insuranceRules;
        _breakdowns = breakdowns;
        _hud = hud;
        _trunk = trunk ?? new Trunk();
        _poiArrivals = poiArrivals;
        _controlBackCommand = new RelayCommand(() => SetScreen(CreateControlScreen()));
        if (_hud is not null)
        {
            _hud.EnterRequested += (_, _) => EnterPoi();
        }
        _wallet = wallet;
        _driverStore = driverStore;
        _streak = streak;
        _zone = zone;
        _friends = friendships;
        _brands = brands;
        _rivals = rivals;
        _cars = cars;
        _carBrands.BrandChanged += (_, e) => OnCarBrand(e.Brand, e.Identity);
        _progress = _onboarding.Load();
        _profile = _profiles.Load();
        // Grandma's car is the first OwnedCar (PORTPLAN Career Start item 3 +
        // item 30): a save whose onboarding already ran but whose garage is
        // still empty — every save written before this slice — seeds the car
        // it picked instead of opening on an empty garage. Unity creates the
        // starter OwnedCar at the pick itself, so this is the same state one
        // session later; the key resolves through the catalogue for specs.
        if (OnboardingDone && _garage.Cars.Count == 0 && _progress.GrandmaCarId is { Length: > 0 } starter)
        {
            _garage.Add(starter, _displayWeek);
            SaveGarage();
            _log.LogInfo($"garage seeded with grandma's car: {starter}");
        }
        // Login streak (PORTPLAN 47, Unity MapScreen.Show): recount from the
        // saved dates before anything else reads the driver state, then
        // grant today's bonus once onboarding is behind (Unity's
        // firstSceneComplete gate at the grant site).
        _streak.Update();
        if (OnboardingDone && _streak.BonusDue)
        {
            int days = _streak.DaysInARow;
            int bonus = ConsecutiveDayBonus.ComputeBonus(days);
            _streak.StampBonus();
            _wallet.AddBonus(bonus);
            SaveDriver(_rivals.LastResult);
            _log.LogInfo($"[Streak] consecutive-day bonus granted: {bonus} CR (day {days})");
        }
        _lastActivity = _clock.UtcNow;
        _voice.Heard += (_, e) => OnVoiceHeard(e.Text, e.IsFinal);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Poll();
        _lastTick = _clock.UtcNow;
        // WHY not DateTime.MinValue: the first throttled write must land a full
        // interval into the session, so a startup read that failed for a
        // transient reason (locked file) can never be immediately clobbered.
        _lastClockSave = _clock.UtcNow;
        _lastGarageSave = _clock.UtcNow;

        TaxiShiftCommand = new RelayCommand(OpenTaxiShift);
        BankCommand = new RelayCommand(OpenBank);
        GarageCommand = new RelayCommand(OpenGarage);
        DealerCommand = new RelayCommand(OpenDealer);
        CarWashCommand = new RelayCommand(OpenCarWash);
        MechanicCommand = new RelayCommand(OpenMechanic);
        InsuranceCommand = new RelayCommand(OpenInsurance);
        PickupCommand = new RelayCommand(Pickup, () => _job.Phase == JobPhase.Proposed);
        DropoffCommand = new RelayCommand(Dropoff, () => _job.Phase == JobPhase.EnRoute);
        ToggleShowCommand = new RelayCommand(() =>
        {
            if (IsShown) Hide(); else Show();
        });

        RefreshJob();
        StatusText = Strings.Get("WaitingForEvo");
        SpeedText = Strings.Format("SpeedKmh", 0);
        GearText = Strings.Get("GearUnknown");
        DetailText = string.Empty;

        // First paint opens on the introduction (PORTPLAN Career Start
        // items 1-5): opening screen, brand page, grandma's car, voice info,
        // birthday dialogue, and only then the main control page (PORTPLAN
        // item 54). The first call after a hidden start reveals that page;
        // every later call (Ctrl+Shift+C, wake word) opens the control page
        // directly instead of replaying the walk. The intro taps re-save the
        // picks, so a returning player walks the same pages as a first-timer.
        // Missing content degrades to the control page (past the birthday)
        // or a blank screen (before it) instead of trapping the user on a
        // broken page.

        SetScreen(HomeScreen());

        // With a mic we start hidden until the wake word; without one the
        // window stays up so the app remains usable (and testable).
        IsShown = !voice.IsAvailable;
        // A visible start has already shown the intro; a hidden start keeps
        // the intro on the first wake (see Show).
        _introShown = IsShown;
    }

    // ---- bindable state -------------------------------------------------

    private bool _isShown;
    public bool IsShown
    {
        get => _isShown;
        private set => SetField(ref _isShown, value);
    }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }

    private bool _isLive;
    public bool IsLive { get => _isLive; private set => SetField(ref _isLive, value); }

    private string _speedText = "";
    public string SpeedText { get => _speedText; private set => SetField(ref _speedText, value); }

    private string _gearText = "";
    public string GearText { get => _gearText; private set => SetField(ref _gearText, value); }

    private string _detailText = "";
    public string DetailText { get => _detailText; private set => SetField(ref _detailText, value); }

    private string _jobLine = "";
    public string JobLine { get => _jobLine; private set => SetField(ref _jobLine, value); }

    private string _fareText = "";
    public string FareText { get => _fareText; private set => SetField(ref _fareText, value); }

    private string _shiftText = "";
    public string ShiftText { get => _shiftText; private set => SetField(ref _shiftText, value); }

    public ICommand TaxiShiftCommand { get; }
    public ICommand PickupCommand { get; }
    public ICommand DropoffCommand { get; }
    public ICommand ToggleShowCommand { get; }

    /// <summary>Temporary bank entry (PORTPLAN 48): the control page's BANK
    /// button and voice answer this until the real door — the bank POI
    /// marker (items 24/59) — lands.</summary>
    public ICommand BankCommand { get; }

    /// <summary>Temporary garage entry (PORTPLAN 30): a You-menu page in Unity,
    /// moves behind the marker network (items 24/59) with the rest.</summary>
    public ICommand GarageCommand { get; }

    /// <summary>Temporary dealer entry (PORTPLAN 29): a POI in Unity.</summary>
    public ICommand DealerCommand { get; }

    /// <summary>Temporary car-wash entry (PORTPLAN 33): a POI in Unity, moving
    /// behind the marker network with the garage and dealer entries.</summary>
    public ICommand CarWashCommand { get; }

    /// <summary>Temporary mechanic entry (PORTPLAN items 31/32): a POI in Unity,
    /// moving behind the marker network with the rest.</summary>
    public ICommand MechanicCommand { get; }

    /// <summary>Temporary insurance-broker entry (PORTPLAN item 34): a POI in
    /// Unity, moving behind the marker network with the rest.</summary>
    public ICommand InsuranceCommand { get; }

    // ---- onboarding navigation (IScreenController) ---------------------------

    private object? _currentScreen;

    /// <summary>Active screen: an onboarding page, a jobs page or null.</summary>
    public object? CurrentScreen
    {
        get => _currentScreen;
        private set
        {
            if (SetField(ref _currentScreen, value))
            {
                OnPropertyChanged(nameof(IsOnboarding));
                OnPropertyChanged(nameof(JobsVisible));
            }
        }
    }

    public bool IsOnboarding => CurrentScreen is not null && CurrentScreen is not IJobsScreen;
    public bool JobsVisible => CurrentScreen is null;

    /// <summary>
    /// True once the birthday dialogue is done on disk. The view needs this
    /// instead of <see cref="IsOnboarding"/> to decide whether a saved window
    /// size may be restored: first paint may still be the introduction while
    /// every later call opens the control page, so the page on screen no
    /// longer says whether the player is past onboarding.
    /// </summary>
    public bool OnboardingDone => _progress.IsComplete;

    public void ChooseBrand(string brand)
    {
        if (!Content.Brands.Contains(brand)) return;
        _progress = _progress with { FavoriteBrand = brand };
        _onboarding.Save(_progress);
        _log.LogInfo($"favorite brand: {brand}");
        SetScreen(new GrandmaCarScreenViewModel(this, Content.Cars));
    }

    public void ChooseCar(string carId)
    {
        if (!Content.Cars.Any(c => c.Id == carId)) return;
        _progress = _progress with { GrandmaCarId = carId };
        _onboarding.Save(_progress);
        // Unity GrandmaCarScreen: the pick IS the first OwnedCar (odometer 0,
        // no insurance) and the car driven from here on. Walking the
        // introduction again keeps an owned car, but the pick still becomes
        // current: the player just told us which car they drive.
        bool added = false;
        if (!_garage.Owns(carId))
        {
            _garage.Add(carId, _displayWeek);
            added = true;
            _log.LogInfo($"grandma car: {carId} (first OwnedCar)");
        }
        else
        {
            _log.LogInfo($"grandma car: {carId}");
        }
        bool selected = _garage.SetCurrent(carId);
        if (added || selected)
        {
            SaveGarage();
        }
        SetScreen(new VoiceInfoScreenViewModel(this, _beat));
    }

    public void ShowDialogue()
    {
        _log.LogInfo("voice info acknowledged: showing birthday dialogue");
        _poi = _pois.Spawn();
        SetScreen(new DialogueScreenViewModel(
            this, FillDialogue(Content.Dialogue, _poi), _beat));
    }

    // The errand line names the business and a random house; ordinary pages
    // pass through untouched. A null POI (no content or no usable address)
    // leaves the authored text as-is instead of trapping the walk.
    private static IReadOnlyList<DialogueLine> FillDialogue(
        IReadOnlyList<DialogueLine> lines, PointOfInterest? poi) =>
        poi is { } destination
            ? DialogueText.Fill(
                lines, destination.Name, AddressFormatter.Format(destination.Address))
            : lines;

    public void FinishDialogue()
    {
        _progress = _progress with { BirthdayDone = true };
        _onboarding.Save(_progress);
        // The HUD is the reminder of where to drive; pin the errand as the
        // dialogue hands it over (PORTPLAN 5/24/27).
        _hud?.SetDestination(_poi);
        if (_poi is { } poi)
        {
            _log.LogInfo($"birthday errand: {poi.Name} at {AddressFormatter.Format(poi.Address)}");
        }
        _log.LogInfo("birthday dialogue done: main control page unlocked");
        SetScreen(CreateControlScreen());
        // The birthday is the hand-over from the intro panel to the companion:
        // the HUD appears here, the panel leaves until the player calls it
        // back with the hotkey or wake word.
        Hide();
        _hud?.Reveal();
    }

    // The HUD's ENTER button fires only once the player is parked on the
    // errand's house. The typed router builds the interior (PORTPLAN 24);
    // an unclaimed type keeps the log-only fallback so no POI is a dead end.
    // A second ENTER while already inside is ignored.
    private void EnterPoi()
    {
        if (_poi is not { } poi)
        {
            return;
        }

        // Already inside: a second ENTER only brings a hidden panel back,
        // never rebuilds the page (and never loses the basket).
        if (CurrentScreen is IPoiScreen current)
        {
            if (!IsShown)
            {
                Present(current);
            }
            return;
        }

        object? screen = _poiArrivals?.Open(poi, _controlBackCommand, OnPoiChanged);
        _log.LogInfo($"entered {poi.Name} at {AddressFormatter.Format(poi.Address)}");
        if (screen is not null)
        {
            Present(screen);
        }
    }

    // A shop purchase moves cash + trunk; the driver save is the one bank
    // both live in (PORTPLAN 51 interim).
    private void OnPoiChanged() => SaveDriver(_rivals.LastResult);

    // Reveal the panel on the requested POI screen. Show() deliberately
    // resets a hidden start to the control page; entering a shop must land
    // on the shop instead, so set the screen before flipping visibility.
    private void Present(object screen)
    {
        RecordActivity();
        _introShown = true;
        SetScreen(screen);
        IsShown = true;
        _log.LogInfo("overlay shown (POI)");
    }

    // Introduction home: the first page of the walk first paint opens on.
    // The chain it starts (preferences -> opening screen -> brand -> grandma's
    // car -> voice info -> birthday dialogue) ends on the main control page, so
    // the walk is never a dead end, and its taps re-save the picks like a first
    // run. No new IScreenController verbs: each page takes its actions by
    // constructor (composition over interface growth).
    private PreferencesScreenViewModel HomeScreen() =>
        new(_preferences, OnPreferencesChanged, ShowOpening);

    // Preferences page CONTINUE: the name + address form is the next step.
    // Missing content degrades instead of trapping the player on a page with
    // nothing to tap: past the birthday the control page still opens, before it
    // there is no home to show.
    private void ShowOpening()
    {
        if (Content.Brands.Count == 0)
        {
            SetScreen(_progress.IsComplete ? CreateControlScreen() : null);
            return;
        }
        SetScreen(new OpeningScreenViewModel(
            _addressMap, _profile, _detectedPlayerName, CompleteOpening));
    }

    // Preferences change: bank the choice, point the resource lookup at the
    // language, and repaint the companion HUD (unit + labels) so the first
    // run never shows a stale language on the one window that is always up.
    private void OnPreferencesChanged(PlayerPreferences preferences)
    {
        _preferences = preferences;
        _preferencesStore.Save(preferences);
        _culture.Apply(preferences.Language);
        _hud?.ApplyPreferences(preferences);
    }

    // Opening screen CONFIRM: the name + address is progress the player just
    // committed, so it banks its own file before the brand page opens. The
    // Steam name only fills an empty profile; a returning player's edited
    // name is never overwritten.
    private void CompleteOpening(PlayerProfile profile)
    {
        _profile = profile;
        _profiles.Save(profile);
        _log.LogInfo($"opening confirmed: {profile.DisplayName}, " +
            $"{profile.Country}, {profile.City}, {StreetName(profile.StreetId)} {profile.HouseNumber}");
        if (Content.Brands.Count == 0)
        {
            SetScreen(_progress.IsComplete ? CreateControlScreen() : null);
            return;
        }
        SetScreen(new BrandScreenViewModel(this, Content.Brands));
    }

    private string StreetName(string streetId)
    {
        foreach (AddressStreet street in _addressMap.Streets)
        {
            if (string.Equals(street.Id, streetId, StringComparison.OrdinalIgnoreCase))
            {
                return street.Name;
            }
        }
        return streetId;
    }

    // Built once and reused: the date header stays live across wakes.
    // The taxi entry opens the job proposal page (port of Unity
    // TaxiJobProposalScreen); delivery opens the career page (port of Unity
    // DeliveryJobScreen); the calendar day buttons have
    // no destinations until the freeroam map lands, so tapping the
    // highlighted day just logs — except the road-test day, which banks the
    // week's car as tested and switches to the next pick (manual stand-in
    // until the road-test drive flow lands). Off-gate taps never reach this
    // action: the page's own day command warns them before dispatching.
    private MainControlScreenViewModel CreateControlScreen()
    {
        _openDayAction ??= new ParameterizedCommand<int>(OnDay);
        if (_control is null)
        {
            // The page boots on the resumed week (if any), not the stamp
            // default: the header is right on the first paint.
            _control = new MainControlScreenViewModel(
                _beat, _game, _clock, _roads, _todos, _hunger, _garage,
                CalendarStamp.Default with { Week = _displayWeek },
                _shift,
                TaxiShiftCommand, new RelayCommand(OpenDelivery),
                new RelayCommand(OnCarHaul),
                BankCommand, GarageCommand, DealerCommand, CarWashCommand, MechanicCommand,
                InsuranceCommand,
                _openDayAction);
            _lastDay = _control.Today;
        }
        return _control;
    }

    private void OnDay(int day)
    {
        if (day == _roadTestDay)
        {
            string before = DescribeRoadTest();
            _roads.MarkTested();
            SaveRoadTest();
            _log.LogInfo($"road test done: {before} -> {DescribeRoadTest()}");
            _control?.Refresh();
            return;
        }
        _log.LogInfo($"calendar day {day} tapped while locked: waiting on the freeroam map");
    }

    private string DescribeRoadTest() =>
        _roads.Current is { } car ? CarTitles.Of(car) : "none";

    // Built once and reused: the pickup cart survives leaving the page, like
    // Unity's player.pickupCart. Back lands on the control page. The run
    // button carries the whole cart run to the host: DELIVER settles it
    // straight into a delivery receipt (the drive is skipped while there is
    // no Eifel address table, exactly like the taxi job's ACCEPT skip). The
    // hub button opens the real OPEN NEW CAREER page (item 18).
    private void OpenDelivery()
    {
        _deliveryScreen ??= new DeliveryJobScreenViewModel(
            _deliveryCatalog, _career, _city, _log,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            new ParameterizedCommand<DeliveryRun>(OnDeliveryRun),
            new RelayCommand(OpenHub),
            _beat);
        SetScreen(_deliveryScreen);
    }

    // Car-hauling contracts (PORTPLAN 20): today the tap settles the haul
    // straight into its receipt — the taxi ACCEPT skip's third use. With no
    // driving slice the dealer-pickup/borrowed-leg/customer flow has no
    // inputs, so the placeholder haul (30-minute delivery leg, driver
    // rating until item 06) settles here and the receipt shows at once.
    // The settle is shared by the driving slice's DELIVER THE CAR later.
    private void OnCarHaul()
    {
        // Unity's receipt names the hauled car (a brand-new customer order
        // waiting at a dealer). With no dealer/offers yet the customer's
        // car is the one the player is driving now: the pool row resolves
        // its display name and real class band via the watcher's identity
        // (exact variant -> folder -> sole row), unknown falls through.
        CarSpec? car = _carIdentity?.FindIn(_cars);
        string carName = car is not null
            ? $"{car.Brand} {car.Model}".ToUpperInvariant()
            : Strings.Get("HaulReceiptCarUnknown");
        int carClass = car is { Class: > 0 } ? car.Class : PlaceholderCarClass;
        if (car is { Class: > 0 })
        {
            _log.LogInfo($"haul car: {carName} class {carClass}");
        }
        else
        {
            // Class comes from the cars.json column; a row without one (or
            // an unknown car) keeps the placeholder band and says so.
            _log.LogInfo($"haul car: {carName} class {carClass} (placeholder, no pool class)");
        }
        CarHaulReceipt receipt = CarHaulReceiptBuilder.Build(
            carName, carClass, PlaceholderDriverRating, PlaceholderHaulMinutes);
        _wallet.AddHaul(receipt);
        // Checkpoint bank, same rule as the paid jobs: settled cash is
        // progress the player can see, so it goes to disk now.
        SaveClock();
        _log.LogInfo($"haul settled: {carName} minutes={receipt.DriveMinutes} " +
            $"tier={receipt.Tier} rate={receipt.RatePerMin:0.00}/min " +
            $"earned={receipt.TotalEarned:0.00} wallet={_wallet.Cash:0.00}");
        SetScreen(new CarHaulReceiptScreenViewModel(
            receipt, _wallet, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen()))));
    }

    // Car-haul placeholders until the drive slice feeds the legs: a
    // 30-minute delivery leg (Unity stamps both leg ends in in-game
    // minutes; the settle prices the difference). The driver rating has no
    // system yet (PORTPLAN item 06), so the pay formula runs on the
    // band's midpoint — rate = 0.83 * (rating * tier / 100).
    private const int PlaceholderHaulMinutes = 30;
    private const int PlaceholderDriverRating = 50;

    // Unity closes the board and shows a fresh DeliveryHubScreen; the board's
    // own VM survives (its cart does), so coming back refreshes it to pull in
    // the spent CredP and the newly connected stores.
    private void OpenHub() =>
        SetScreen(new DeliveryHubScreenViewModel(
            _career, _deliveryCatalog, _city, _log,
            new RelayCommand(CloseHub),
            _beat));

    private void CloseHub()
    {
        _deliveryScreen?.Refresh();
        SetScreen(_deliveryScreen);
    }

    private void SetScreen(object? screen)
    {
        // WHY a real screen change supersedes a pending voice tap (wake-word
        // re-home, mouse navigation): the old beat must never fire afterwards.
        _beat.Cancel();
        CurrentScreen = screen;
    }

    // ---- lifecycle (called by the view) ----------------------------------

    public void Start()
    {
        _lastTick = _clock.UtcNow;
        _carBrand = _carBrands.Prime(DefaultBrand);
        _carIdentity = _carBrands.CurrentIdentity;
        _log.LogInfo($"listening for 'hey {_carBrand}'");
        _carBrands.StartWatching();
        _voice.StartListening();
        _timer.Start();
        // Bank the resumed (or freshly drawn) pick at the top of the session:
        // a killed process never reaches a completion, and the next session
        // must still name this car (PORTPLAN 49).
        SaveRoadTest();
    }

    public WindowBounds? LoadPlacement() => _store.Load();
    public void SavePlacement(WindowBounds bounds) => _store.Save(bounds);

    /// <summary>
    /// Persists the game moment + display week and restamps the poll throttle,
    /// so every bank path (day flip, checkpoint, interval, window close) keeps
    /// the interval honest. Never throws (the store swallows IO).
    /// </summary>
    public void SaveClock()
    {
        _lastClockSave = _clock.UtcNow;
        _clocks.Save(new GameClockState(_game.GameNow, _displayWeek));
    }

    /// <summary>
    /// Banks the road-test pick. Two moments need it: a session opening (a
    /// rebuild/Stop-Debugging kills the process before any test is driven, and
    /// the next session must offer that very same car) and a completion (the
    /// only event allowed to move the car). Never throws (the store swallows IO).
    /// </summary>
    private void SaveRoadTest() => _roadTestStore.Save(_roads.Snapshot());

    /// <summary>
    /// Banks the town for the next session (PORTPLAN 25): the city size plus
    /// the shelf state that drives it. Exit-only, like Unity's storeStates,
    /// which are never written mid-session — the drift is world flavour, so a
    /// killed process costs an hour of growth and no player progress. Never
    /// throws (the store swallows IO).
    /// </summary>
    public void SaveCity() => _cityStore.Save(_city.Snapshot());

    /// <summary>
    /// Banks the hunger reading for the next session (Unity PlayerData.hunger
    /// survives restarts): the meter plus the one-shot event state resume
    /// instead of re-seeding at 0. Exit-only, like the city. Never throws
    /// (the store swallows IO).
    /// </summary>
    public void SaveHunger() => _hungerStateStore.Save(_hunger.Snapshot());

    // ---- core loop (public so tests drive it directly) --------------------

    public void Poll()
    {
        var now = _clock.UtcNow;
        double dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        // Idle auto-hide, companion parity. Must run before any telemetry
        // early-return: hiding is independent of whether EVO is live.
        // Suspended while the window is being resized: the sizing drag
        // itself is the user's activity, even when held still.
        // Suspended while a dialogue is still open (IDialogueScreen): a
        // slow reader is never hidden mid-conversation.
        // Uses the clock, never tick counts.
        bool dialogueOpen = CurrentScreen is IDialogueScreen dialogue && !dialogue.IsComplete;
        // A POI interior is a page the player is actively reading/shopping;
        // hiding it mid-basket would lose the visit, so it keeps the panel up
        // until BACK (the dialog rule applies to the same reason).
        bool poiOpen = CurrentScreen is IPoiScreen;
        if (IsShown && !_resizing && !dialogueOpen && !poiOpen
            && (now - _lastActivity).TotalSeconds >= IdleHideSec)
        {
            Hide();
        }

        // The control page date stays live whether or not EVO is attached.
        // Refresh self-guards on the minute: no per-tick allocations.
        // A timed-out shift ends here (Unity 30-min idle rule) so the taxi
        // button flips back to START even when the player never taps it.
        if (_shift.IsTimedOut(now) && _shift.TryEndTimedOut(now, out TimeSpan idle))
        {
            _log.LogInfo($"shift ended (timeout after {idle.TotalMinutes:0}m idle): " +
                $"{_shift.JobsDone} jobs / {_shift.Earned:0.00}");
            _lastShiftSecs = -1;
            RefreshJob();
        }
        // Voice acknowledgment: the beat drains first so a due tap acts at
        // its end, then flash expiry runs on the clock, not the minute
        // guard: a half-second teal glow must clear inside the same minute
        // it started.
        _beat.Poll();
        _control?.UpdateFlash();
        _control?.Refresh();

        // Game-day flip: the calendar highlight moved. One int compare per
        // tick, work only on transitions (hot-path discipline).
        if (_control is not null && _control.Today != _lastDay)
        {
            int prev = _lastDay;
            _log.LogInfo($"calendar day changed: {prev} -> {_control.Today}");
            _lastDay = _control.Today;
            // Sunday-midnight crossing completes the week: the display week
            // rolls and the moment persists. The road-test pick deliberately
            // does NOT rotate here — an untested car stays on offer until it
            // is actually driven (PORTPLAN 49).
            if (prev == ControlCalendar.Sunday && _control.Today == ControlCalendar.Monday)
            {
                _displayWeek++;
                _control.DisplayWeek = _displayWeek;
                _log.LogInfo($"week {_displayWeek} opened: road test stays {DescribeRoadTest()}");
            }
            SaveClock();
            _control.Refresh();
        }

        // The town ticks on game minutes, whether or not a page is open: one
        // DateTime subtraction per poll, all work on a whole-minute boundary
        // (hot-path discipline). Demand needs an owned hub, like Unity's gate.
        if (_city.AdvanceTo(_game.GameNow, _career.Owned.Count > 0))
        {
            // The log reports the town that gets banked (every cell), not the
            // career-gated header the pages show: a fresh career sees 0 on
            // screen while its stores are still drifting behind it.
            _log.LogInfo($"city: {_city.TotalCitizens(_deliveryCatalog.Stores)} citizens, " +
                $"{_city.TotalGrowthPerHour(_deliveryCatalog.Stores)}/hr");
            // Unity repaints the board from OnCitizensChanged; only the cached
            // page needs the push, the hub page is rebuilt on every visit.
            _deliveryScreen?.Refresh();
        }

        // Hunger meter (PORTPLAN 26, Unity GameClockManager.UpdateHunger): +1
        // once per crossed game hour and only past onboarding (Unity's
        // firstSceneComplete gate), capped at the rules' max, work only on a
        // whole-hour boundary (hot-path discipline). Unity blocks the job
        // centre at maxHunger - 1 (HungerBlockPopup); the block is disabled
        // for now — no trunk or grocery store exists yet (items 51 / 26 map
        // POI), so the gate would be a one-way valve. hasPancakes stays false
        // until a trunk lands; the one-shot event then fires exactly the way
        // Unity fires it.
        HungerReport report = _hunger.AdvanceTo(_game.GameNow, OnboardingDone, hasPancakes: false);
        if (report.Changed)
        {
            _log.LogInfo($"[Hunger] {_hunger.Value}/{_hunger.Max}");
        }
        if (_hunger.Blocks != _hungerBlocked)
        {
            _hungerBlocked = _hunger.Blocks;
            if (_hungerBlocked)
            {
                _log.LogWarning("[Hunger] job gate reached (" +
                    $"{_hunger.Value}/{_hunger.Max}) — the TOO HUNGRY block is disabled until a food source lands");
            }
        }

        // Nightly insurance charge (PORTPLAN 34, Unity GameClockManager.
        // UpdateInsurance): once per game date, in the charge hour. Runs on the
        // game clock, so it lands whether or not EVO is attached.
        ChargeDailyInsurance();

        // Wall-clock bank: runs before the telemetry read so it also holds
        // while EVO is closed, and before the shift paths because a banked
        // moment is exactly what a kill between sessions would otherwise
        // lose. SaveClock restamps, so this costs one write per ClockSaveSec.
        if ((now - _lastClockSave).TotalSeconds >= ClockSaveSec)
        {
            SaveClock();
        }

        // Shift countdown: second-guarded so an open-but-idle shift costs one
        // string per second, never per 10 Hz tick. While EnRoute the meter
        // below already repaints every tick, so this path skips it.
        if (_shift.IsStarted && _job.Phase != JobPhase.EnRoute)
        {
            int secs = (int)_shift.Remaining(now).TotalSeconds;
            if (secs != _lastShiftSecs)
            {
                _lastShiftSecs = secs;
                RefreshJob();
            }
        }
        else if (!_shift.IsStarted)
        {
            _lastShiftSecs = -1;
        }

        var s = _telemetry.TryRead();
        if (s is null)
        {
            IsLive = false;
            StatusText = Strings.Get(_telemetry.GameRunning ? "EvoNoShm" : "WaitingForEvo");
            SpeedText = Strings.Format("SpeedKmh", 0);
            _hud?.Update(null, _garage.Current?.Odometer, _telemetry.GameRunning);
            return;
        }

        IsLive = true;
        StatusText = Strings.Format("EvoLive", s.PacketId);
        SpeedText = Strings.Format("SpeedKmh", s.SpeedKmh);
        GearText = TelemetryConventions.GearDisplay(s.Gear);
        DetailText = Strings.Format("EvoDetail", s.Rpm, s.Gas, s.Brake);

        // Odometer feed (PORTPLAN items 31/32, Unity DrivingManager's odometer
        // integration): every live tick banks the distance the current car
        // covered into the wear deck's input, and the engine inspections ride
        // the drive. The garage file is banked on the same wall-time throttle
        // as the clock, so a kill costs at most the window.
        IntegrateOdometer(s, dt);
        _hud?.Update(
            s, _garage.Current?.Odometer, gameRunning: true, IsDrivingWrongCar());
        if ((now - _lastGarageSave).TotalSeconds >= OdometerSaveSec)
        {
            SaveGarage();
        }

        // Meter runs while the passenger is aboard.
        if (_job.Phase == JobPhase.EnRoute && dt is > 0 and < 1)
        {
            _job.DistanceKm += Math.Max(0, s.SpeedKmh) / 3600.0 * dt;
            _job.Fare = TaxiFare.ComputeBase(_job.DistanceKm, _fares);
            RefreshJob();
        }
    }

    // ---- visibility + voice --------------------------------------------------

    /// <summary>Called by the view on any mouse/key activity.</summary>
    public void RecordActivity() => _lastActivity = _clock.UtcNow;

    /// <summary>
    /// Called by the view when a window resize drag starts/stops.
    /// While resizing, idle auto-hide is suspended; both edges count as
    /// activity so the 15 s window restarts after the drag.
    /// </summary>
    public void SetResizing(bool resizing)
    {
        _resizing = resizing;
        RecordActivity();
    }

    public void Show()
    {
        RecordActivity();
        if (IsShown) return;
        IsShown = true;
        // The single "the app was called" seam: the Ctrl+Shift+C hotkey
        // and the wake word all land here. The first show of a hidden start
        // reveals the intro already built in the constructor; every later
        // show opens the main control page directly instead of resuming
        // whatever page was last open. It rides the hidden -> shown edge only
        // — a Show() while already visible must not yank the player off the
        // page they are using.
        if (!_introShown && CurrentScreen is not null)
        {
            _introShown = true;
            _log.LogInfo("overlay shown (intro)");
            return;
        }
        _introShown = true;
        SetScreen(CreateControlScreen());
        _log.LogInfo("overlay shown");
    }

    private void Hide()
    {
        if (!IsShown) return;
        IsShown = false;
        _mutedLabel = null;
        _mutedAction = null;
        _mutedText = string.Empty;
        _log.LogInfo("overlay hidden");
    }

    // Watcher thread lands here; marshal to the UI thread like voice.
    private void OnCarBrand(string brand, CarIdentity? identity)
    {
        try
        {
            _timer.Dispatcher.Invoke(() =>
            {
                _carBrand = brand;
                _carIdentity = identity;
                _log.LogInfo($"now following: {brand} (say 'hey {brand}')");
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // WHY: Dispatcher.Invoke posts to a dispatcher that may have shut
            // down mid-race; InvalidOperationException is that exact signal.
            // The lambda's own work (assign + log) never throws: FileLogger
            // swallows sink errors, stores swallow IO. Anything else here is
            // a real bug and must surface, not die silent on this thread.
        }
    }

    // Mic thread lands here; marshal to the UI thread for all state changes.
    private void OnVoiceHeard(string text, bool isFinal)
    {
        try
        {
            _timer.Dispatcher.Invoke(() => HandleVoice(text, isFinal));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // WHY: same shutdown race as OnCarBrand: the post or the inline
            // UI-thread run can meet a dispatcher that is going down. Screen
            // commands only touch VM state and stores that swallow their own
            // IO, so these two types are the whole recoverable set; a bug
            // inside HandleVoice stays visible instead of being muted here.
        }
    }

    internal void HandleVoice(string text, bool isFinal)
    {
        // Diagnostic: one line per finished utterance. Non-English names
        // ("Cento Miglia") arrive as whatever the English model believes it
        // heard; reading the real transcripts from the log lets us extend
        // the keyword tables from data instead of guesses.
        if (isFinal) _log.LogInfo($"voice heard: {text}");

        // The companion HUD sits outside the main screen stack, so its
        // ENTER button answers even while the overlay is hidden for driving.
        // Finals only: one utterance must not enter twice.
        if (isFinal && TryEnterPoi(text))
        {
            return;
        }

        if (!IsShown)
        {
            string brand = _carBrand;
            if (VoiceMatch.IsWake(text, brand, AliasesFor(brand), Content.Confusions, isFinal))
            {
                DateTime now = _clock.UtcNow;
                if ((now - _lastWake).TotalSeconds < WakeCooldownSec) return;
                _lastWake = now;
                _log.LogInfo($"wake heard: {text}");
                Show();

            }

            return;
        }

        if (CurrentScreen is not IVoiceScreen screen) return;
        string? label = VoiceMatch.MatchLabel(text, screen.VoiceActions.Keys, Content.Confusions);
        if (label is null)
        {
            // A failed attempt is still the user being present. The logged
            // 2026-10-13 session hid mid-retry ("open your career" x3, then
            // the 15 s window expired and the next phrase woke to home
            // instead of pressing the button). Unmatched FINAL transcripts
            // restart the idle window; partials do not, so a background
            // voice that never finalizes keeps the old hide timing.
            if (isFinal) RecordActivity();
            return;
        }
        // Voice control counts as user input: same idle-reset as a click.
        // Repeats of the same tap still count as presence (restarting the
        // 15 s window) even though the repeat itself stays muted below.
        RecordActivity();
        // Mute repeats of the same tap until the utterance's final fragment.
        // One utterance can reach a button under two different keys (a
        // partial hearing "where is" alias, the final "Where is Willy"
        // label), so the shared action delegate counts as the same tap too.
        // WHY the utterance text and the fired label's word run join the
        // mute: a beat-deferred page change can hand the FINAL fragment to
        // the new screen, where the same words map to a different label and
        // delegate. Logged 2026-10-14 18:37: "we continue" closed the
        // birthday dialogue and its final re-scored to "the continue" one
        // second later — already on the control page, where bare
        // "continue" is a taxi keyword: the tail tapped the button and the
        // shift started with nobody asking for it. Exact text missed it
        // (leading word rewritten), the label's word run survives:
        // fragments answering the fired label are the same utterance,
        // whichever page is now on screen.
        Action action = screen.VoiceActions[label];
        string heard = VoiceMatch.Normalize(text);
        if (label == _mutedLabel || action == _mutedAction || heard == _mutedText
            || (_mutedLabel is not null && VoiceMatch.AnswersText(_mutedLabel, heard)))
        {
            if (isFinal) { _mutedLabel = null; _mutedAction = null; _mutedText = string.Empty; }
            return;
        }

        _mutedLabel = label;
        _mutedAction = action;
        _mutedText = heard;
        _log.LogInfo($"voice button: {text}");
        action.Invoke();
    }

    // HUD arrival voice tap: only while the button is up, and only a finished
    // utterance. The HUD owns the teal acknowledgment and the half-second
    // wait; the host still owns what entering means.
    private bool TryEnterPoi(string text)
    {
        if (_hud is not { AtDestination: true })
        {
            return false;
        }
        if (VoiceMatch.MatchLabel(text, EnterKeywords, Content.Confusions) is null)
        {
            return false;
        }

        RecordActivity();
        _log.LogInfo($"voice button: {text}");
        _hud.RequestEnter();
        return true;
    }

    private string[] AliasesFor(string brand)
    {
        if (Content.Aliases.TryGetValue(brand, out string[]? hit)) return hit;
        return Content.Aliases.TryGetValue(VoiceMatch.Normalize(brand), out string[]? norm) ? norm : [];
    }

    // ---- job commands ------------------------------------------------------

    // Unity JobSelectionScreen's OnStartTaxiShift: the tap owns the shift
    // bookkeeping (start fresh, or end an idle-timed-out one first), then
    // opens the proposal page — the job itself only exists once ACCEPTed.
    // The shift flips the control-page button to CONTINUE right here,
    // behind the voice acknowledgment glow, exactly like Unity stamps
    // taxiShiftStarted before showing the job.
    private void OpenTaxiShift()
    {
        DateTime now = _clock.UtcNow;
        if (_shift.IsTimedOut(now) && _shift.TryEndTimedOut(now, out TimeSpan idle))
        {
            _log.LogInfo($"shift ended (timeout after {idle.TotalMinutes:0}m idle): " +
                $"{_shift.JobsDone} jobs / {_shift.Earned:0.00}");
        }
        if (!_shift.IsStarted)
        {
            _shift.Start(now);
            _lastShiftSecs = -1;
            _log.LogInfo("taxi shift started");
            // Checkpoint bank: a shift start is the moment a player reads as
            // progress, so the calendar never rewinds past it when the process
            // dies before the next throttled write.
            SaveClock();
        }

        // Built once and reused like the delivery board: the committed radius
        // survives leaving the page, and every visit opens on a fresh
        // proposal (the Unity screen shows a newly generated job per visit).
        _proposal ??= new TaxiJobProposalViewModel(
            _proposals, _log, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            _acceptJobAction ??= new ParameterizedCommand<TaxiJobState>(TaxiAccepted));
        _proposal.Open();
        SetScreen(_proposal);
        RefreshJob();
        _control?.Refresh();
    }

    // Accepting settles the job straight into its receipt: with no driving
    // slice the PICKUP/DROPOFF meter flow has no inputs, so the placeholder
    // ride (8.2 km, tip eligible, no fine) settles here and the receipt
    // screen shows at once. Pickup()/Dropoff() below are the driving
    // slice's hooks and stay dormant until telemetry feeds them.
    private void TaxiAccepted(TaxiJobState? job)
    {
        if (job is null) return;
        // Dirt gate (PORTPLAN 33, Unity TaxiJobLifecycleHandler's pickup
        // arrival): a car at max filth refuses the ride — the customer walks
        // and familiarity drops 1. Unity cancels the accepted job when the
        // pickup is reached, but this placeholder settles on ACCEPT, so the
        // gate sits here until the driving slice gives the pickup its own
        // moment. Unity also costs concentration -1 on the refusal; parked
        // for item 06 (no driver rating yet).
        if (RefuseDirtyPickup(job)) return;
        // Breakdown roll at the trip start (PORTPLAN 39, Unity
        // BeginTripSession): a triggered trip is cut before it pays — the job
        // never becomes active, the customer loses 1 familiarity, the mechanic's
        // tow fee is charged, and the breakdown page replaces the receipt.
        if (RollTripStart(BreakdownKind.Taxi) is { } breakdown)
        {
            _friends.AddFamiliarity(BaseCustomerName(job.Customer), -1);
            _log.LogInfo($"taxi job cut by breakdown: {job.Customer.Full} " +
                $"{job.PickupName} -> {job.DropoffName}");
            ShowBreakdown(breakdown);
            return;
        }
        _job = job;
        _log.LogInfo($"job accepted: {job.Customer.Full} {job.PickupName} -> {job.DropoffName}");
        SettleReceipt();
    }

    // Unity's dirty-car refusal: the customer refuses the ride, familiarity
    // with them drops by one (the store banks itself), and the proposal page
    // shows the reason. The page never starts a job on this path. A repeated
    // ACCEPT on the same proposal shows the reason again but does not charge
    // the customer twice (Unity's _tooDirtyArrived).
    private bool RefuseDirtyPickup(TaxiJobState job)
    {
        if (_garage.Current is not { } car || !car.Filth.RefusesPickup)
        {
            _dirtyRefusedJob = null;
            return false;
        }
        string customer = BaseCustomerName(job.Customer);
        if (!ReferenceEquals(job, _dirtyRefusedJob))
        {
            _dirtyRefusedJob = job;
            _friends.AddFamiliarity(customer, -1);
            _log.LogWarning($"[Filth] pickup refused at {car.Filth.Value}/{car.Filth.Max}: " +
                $"{customer} friendship -1");
        }
        _proposal?.ShowRefusal(Strings.Format("TaxiTooDirty", customer));
        return true;
    }

    private void Pickup()
    {
        if (_job.Phase != JobPhase.Proposed) return;
        _job.Phase = JobPhase.EnRoute;
        _job.DistanceKm = 0;
        _log.LogInfo($"job pickup: {_job.PickupName}");
        RefreshJob();
    }

    private void Dropoff()
    {
        if (_job.Phase != JobPhase.EnRoute) return;
        _job.Phase = JobPhase.Complete;
        SettleReceipt();
    }

    // Prices the placeholder ride, banks it to the wallet, the driver save
    // and the shift, and shows the receipt. Shared by ACCEPT now and the
    // meter DROPOFF once the driving slice lands.
    private void SettleReceipt()
    {
        // No driving slice yet: the placeholder ride settles here instead
        // of the live meter, wherever the settle was triggered.
        _job.Phase = JobPhase.Complete;
        _job.DistanceKm = PlaceholderDistanceKm;
        // Testing hook: the settled ride wears the car by its own distance.
        BankJobDistance(_job.DistanceKm);
        int capacity = ResolveCapacity();
        // Unity keys familiarity off the base name (no companion suffix):
        // every completed ride is +1 for that resident, strangers included
        // (0 -> 1 on the first ride). The outcome rides the receipt so the
        // page renders it; transitions log, quiet rides do not.
        string baseName = BaseCustomerName(_job.Customer);
        FriendshipOutcome friendship = _friends.RegisterRide(baseName);
        // Unity banks the trip km into the driven brand before pricing, so
        // the current ride already counts toward its own sponsorship rate.
        string brandName = ResolveBrandName();
        double brandKm = _brands.RegisterRide(brandName, _job.DistanceKm);
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            _job.DistanceKm, capacity, PlaceholderCarClass,
            PlaceholderCarRarity, PlaceholderStreetKnowledgePct,
            tipEligible: true, PlaceholderFines, _fares, _wallet.Xp,
            new FriendshipResult(friendship.OwnerName, friendship.Level, friendship.TierIncreased),
            brandName, brandKm);
        if (friendship.TierIncreased)
        {
            _log.LogInfo($"friendship tier up: {friendship.OwnerName} -> {friendship.Level}");
        }
        _job.Fare = receipt.BaseTotal;
        _job.BrandBonus = receipt.BrandBonus;
        _job.Tip = receipt.Tip;
        _job.Fine = receipt.FineDeduction;
        _wallet.AddReceipt(receipt);
        // Zone tick (PORTPLAN 58, Unity expansionJobsCompleted++): every
        // settled taxi ride counts; the interest lands in the bank before
        // the expansion street joins (Unity orders it the same way —
        // counter, interest, street registration at the receipt CONTINUE).
        _zone.RegisterJob();
        ApplyBankInterest();
        TryExpandZone();
        RivalsResult? standings = RefreshStandings();
        _shift.RecordCompleted(_job, _clock.UtcNow);
        // Dirt tick (PORTPLAN 33, Unity TaxiJobLifecycleHandler's dropoff
        // block): every completed ride is +1 on the car driven now.
        DirtyCurrentCar();
        // Checkpoint bank: a paid job is progress the player can see, so the
        // moment goes to disk now instead of waiting for the throttle. The
        // garage rides along so the dirt tick is not lost with the process.
        SaveClock();
        SaveGarage();
        _log.LogInfo($"job dropoff: {_job.PickupName} -> {_job.DropoffName} " +
                  $"dist={receipt.DistanceKm:0.00}km total={receipt.Total:0.00} xp={receipt.XpEarned:0.00} " +
                  $"brand={receipt.BrandName} bonus={receipt.BrandBonus:0.00} " +
                  $"wallet={_wallet.Cash:0.00} shift={_shift.JobsDone} jobs / {_shift.Earned:0.00}");
        SetScreen(new TaxiReceiptScreenViewModel(
            receipt, _job.Customer.Full, _wallet, _shift, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen())), _rivals));
        RefreshJob();
    }

    // One driver save the whole VM shares (PORTPLAN 51 interim): every state
    // owner contributes its slice, so a save from any path can never clobber
    // another owner with stale numbers. Standings ride along from the caller
    // that just refreshed them; null keeps the last known board snapshot
    // (OfflineRivals seeds it from the driver save at boot).
    private void SaveDriver(RivalsResult? standings)
    {
        _driverStore.Save(_wallet.Snapshot() with
        {
            RivalsRank = standings?.CurrentRank ?? 0,
            RivalsTotal = standings?.TotalCompetitors ?? 0,
            DaysInARow = _streak.DaysInARow,
            LastLoginDate = _streak.LastLoginDate,
            LastDailyBonusDate = _streak.LastDailyBonusDate,
            ExpansionJobs = _zone.CountedJobs,
            ExpansionCount = _zone.ExpansionCount,
            StreetIds = _zone.StreetIds,
            ZoneKm = _zone.Km,
            Bank = _wallet.BankBalance,
            HasEnteredBank = _wallet.HasEnteredBank,
            Trunk = _trunk.Snapshot(),
        });
    }

    /// <summary>The standings tail both receipts share (Unity
    /// TaxiJobLifecycleHandler's dropoff block + ApplyStoreDeliveryEffects,
    /// PORTPLAN item 60): the refresh and the XP push ride right after the
    /// XP bank, before the receipt opens — LastResult is ready when the
    /// page reads it.</summary>
    private RivalsResult? RefreshStandings()
    {
        RivalsResult? standings = _rivals.Refresh(_wallet.Xp);
        // The opening screen supplies the display name; before it is
        // confirmed the push stays nameless like the old placeholder.
        _rivals.PushXp(_wallet.Xp, _profile?.DisplayName ?? string.Empty);
        SaveDriver(standings);
        LogStandingsMove(standings);
        return standings;
    }

    // The nightly per-car insurance premium (PORTPLAN 34, Unity
    // GameClockManager.UpdateInsurance): once per game date, inside the rules'
    // charge hour, for every insured car with a price. The charge takes what
    // the cash covers (a premium is a commitment, not a purchase), and the
    // stamp rides the driver save so a restart inside the same hour never
    // double-charges.
    private void ChargeDailyInsurance()
    {
        DateTime now = _game.GameNow;
        if (now.Hour != _insuranceRules.ChargeHour)
        {
            return;
        }
        string date = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (string.Equals(_wallet.LastInsuranceChargeDate, date, StringComparison.Ordinal))
        {
            return;
        }
        _wallet.MarkInsuranceCharged(date);
        double total = 0.0;
        int insured = 0;
        foreach (OwnedCar car in _garage.Cars)
        {
            if (!car.HasInsurance || _garage.SpecOf(car) is not { Price: > 0.0 } spec)
            {
                continue;
            }
            double premium = InsurancePricing.DiscountedDailyCost(spec.Price, PlaceholderDriverRating, _insuranceRules);
            total += _wallet.ChargeUnavoidable(premium);
            insured++;
        }
        if (insured > 0)
        {
            _log.LogInfo($"[Insurance] daily premium {total:0.00} CR for {insured} car(s) " +
                $"(cash {_wallet.Cash:0.00})");
            SaveDriver(_rivals.LastResult);
        }
    }

    // One trip-start roll (PORTPLAN 39, Unity DrivingManager.BeginTripSession):
    // a stranded engine always fires the engine-failure variant; otherwise the
    // roll runs against the wear-derived chance. Past onboarding and with a car
    // only (Unity's firstSceneComplete + mechanic gates).
    private BreakdownOutcome? RollTripStart(BreakdownKind kind)
    {
        if (!OnboardingDone || _garage.Current is not { } car)
        {
            return null;
        }
        double fee = _garage.MaintenanceRules.PickupFee;
        if (car.Engine.Broken)
        {
            return new BreakdownOutcome(kind, Fraction: 0.0, fee, EngineFailure: true);
        }
        double? fraction = _breakdowns.TryTrigger(car.Condition.BreakdownChance(car.Odometer));
        return fraction is { } hit ? new BreakdownOutcome(kind, hit, fee, EngineFailure: false) : null;
    }

    // The mechanic tows the car in: the pickup fee is charged (Unity
    // BreakdownFlow.PickupFee), the driver save banks it, and the page reports
    // the interrupted trip. The per-kind consequences are applied by the caller,
    // which holds the job context.
    private void ShowBreakdown(BreakdownOutcome breakdown)
    {
        double fee = _wallet.ChargeUnavoidable(breakdown.PickupFee);
        SaveDriver(_rivals.LastResult);
        _log.LogWarning($"[Breakdown] {breakdown.Kind}" +
            $"{(breakdown.EngineFailure ? " engine failure" : $" at {breakdown.Fraction:0.00} of the route")}," +
            $" tow fee {fee:0.00} CR (cash {_wallet.Cash:0.00})");
        SetScreen(new BreakdownScreenViewModel(
            breakdown, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen()))));
    }

    // The distinct hub owners a run would have befriended, resolved read-only
    // (a failed trip must not credit the ramp). Used by the breakdown hook.
    private List<string> SourceOwners(DeliveryRun run)
    {
        List<string> owners = [];
        foreach (string key in run.StoreKeys)
        {
            if (_deliveryCatalog.Find(key) is not { } store)
            {
                continue;
            }
            string owner = _career.OwnerFor(store.Category);
            if (owner.Length > 0 && !owners.Contains(owner))
            {
                owners.Add(owner);
            }
        }
        return owners;
    }

    // Zone expansion at the settle hooks (PORTPLAN 58, Unity
    // ExpandOperatingZone): past onboarding (the firstSceneComplete gate)
    // and past the threshold curve, one more abstract street joins the
    // zone. Unity expands at the receipt CONTINUE with preloaded real
    // streets — the interim registers placeholder ids right away, since
    // the concrete Eifel street table (PORTPLAN Step 1) is still pending.
    private void TryExpandZone()
    {
        if (!OnboardingDone || !_zone.ExpansionDue) return;
        string streetId = $"s{_zone.ExpansionCount + 1}";
        // Interim length 0.0: no street metadata exists yet to grow the km.
        if (_zone.TryExpand(streetId, 0.0))
        {
            _log.LogInfo($"[Zone] expanded: {streetId} joins " +
                $"(zone {_zone.StreetIds.Count} streets, {_zone.Km:0.0} km)");
        }
    }

    // Bank interest at the two settle sites (PORTPLAN 48, Unity
    // BankInterestHelper.ApplyForCompletion): credited INTO the bank after
    // the payday so it compounds, never on the car-haul settle (no hook
    // there — Unity parity). A fresh driver already answers rate 1 (the
    // home street × day one), so the saldo grows from the first job.
    private void ApplyBankInterest()
    {
        int rate = BankRules.Rate(_zone.StreetIds.Count, _streak.DaysInARow);
        if (rate <= 0)
        {
            // Unity skips the event on a zero rate: nothing to credit, no log.
            return;
        }
        double interest = BankRules.ComputeInterest(_wallet.BankBalance, _zone.StreetIds.Count, _streak.DaysInARow);
        _wallet.ApplyInterest(interest);
        _log.LogInfo($"[Bank] interest +{interest:0.00} CR " +
            $"(rate {rate}, bank {_wallet.BankBalance:0.00})");
    }

    // The bank screen (PORTPLAN 48, Unity BankScript.Show): opening stamps
    // hasEnteredBank (VisitBank todo once item 51 lands). A fresh screen per
    // open, since the saldo lines always open on live values — Unity's Show
    // semantics. The back command returns to the control page; a withdraw
    // persists through the shared driver save so the saldo never forks.
    private void OpenBank()
    {
        _wallet.MarkBankEntered();
        SetScreen(new BankScreenViewModel(
            _wallet,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            moved =>
            {
                SaveDriver(_rivals.LastResult);
                _log.LogInfo($"[Bank] withdrew {moved:0.00} CR " +
                    $"(cash {_wallet.Cash:0.00}, bank {_wallet.BankBalance:0.00})");
            },
            _beat));
    }

    // The garage page (PORTPLAN 30, Unity GarageScreen): a fresh page per visit,
    // so a car bought since the last visit is on it. A switch banks the garage
    // through the shared save (Unity SaveManager.Save) — the page never writes.
    private void OpenGarage()
    {
        SetScreen(new GarageScreenViewModel(
            _garage,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            key =>
            {
                SaveGarage();
                _log.LogInfo($"[Garage] now driving {key} " +
                    $"({_garage.Cars.Count} cars owned)");
            },
            _beat));
    }

    // The dealer's weekly floor (PORTPLAN 29, Unity CarDealerScreen). The week
    // rides in from the display week, so the floor turns over with the calendar
    // and a re-opened page inside one week shows the same cars (DealerStock).
    private void OpenDealer()
    {
        SetScreen(new DealerScreenViewModel(
            _cars, _garage, _wallet, _displayWeek,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            OnCarBought, _beat));
    }

    // The car wash (PORTPLAN 33, Unity CarWashScreen): a fresh page per visit,
    // so the car driven now and its reading are live. A wash spends the flat
    // price and clears the car's filth; both are banked here (the driver save
    // for the cash, garage.json for the reading) and logged — the page never
    // writes (SRP).
    private void OpenCarWash()
    {
        SetScreen(new CarWashScreenViewModel(
            _garage, _wallet, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            (removed, paid) =>
            {
                SaveDriver(_rivals.LastResult);
                SaveGarage();
                _log.LogInfo($"[CarWash] removed {removed} filth for {paid:0.00} CR " +
                    $"(cash {_wallet.Cash:0.00}, {_garage.CurrentKey ?? "no car"})");
            }));
    }

    // The car mechanic (PORTPLAN items 31/32, Unity CarMechanicScreen): a fresh
    // page per visit, so the car driven now, its wear deck and its engine are
    // live. Parts are replaced, the engine repaired or swapped; every action
    // moves cash and mutates the car's condition, so both are banked here (the
    // driver save for the cash, garage.json for the condition) and logged — the
    // page never writes (SRP).
    private void OpenMechanic()
    {
        SetScreen(new MechanicScreenViewModel(
            _garage, _wallet, _game, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            repair =>
            {
                // A covered car's repair pays 90% into the escrow the broker
                // collects (Unity RepairScreen's insurance tail, repurposed to
                // the mechanic since ACE owns accidents). The page never
                // writes; the host banks the cash and the condition.
                if (_garage.Current is { HasInsurance: true } insured && repair.Cost > 0.0)
                {
                    _wallet.AddInsurancePayout(repair.Cost * 0.9);
                }
                SaveDriver(_rivals.LastResult);
                SaveGarage();
                _log.LogInfo($"[Mechanic] {repair.Kind} cost {repair.Cost:0.00} CR " +
                    $"(parts {repair.PartsReplaced}, engine replaced {repair.EngineReplaced}, " +
                    $"cash {_wallet.Cash:0.00}, {_garage.CurrentKey ?? "no car"})");
            }));
    }

    // Insurance broker (PORTPLAN 34, Unity InsuranceScreen): a fresh page per
    // visit, so the cover flags and the escrow are live. A toggle and a
    // collection are banked here (the garage for the flag, the driver for the
    // cash + escrow) and logged — the page never writes (SRP).
    private void OpenInsurance()
    {
        SetScreen(new InsuranceScreenViewModel(
            _garage, _wallet, _insuranceRules, PlaceholderDriverRating, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            (key, insured) =>
            {
                SaveGarage();
                _log.LogInfo($"[Insurance] {key} {(insured ? "insured" : "uninsured")} " +
                    $"(cash {_wallet.Cash:0.00})");
            },
            moved =>
            {
                SaveDriver(_rivals.LastResult);
                _log.LogInfo($"[Insurance] collected {moved:0.00} CR (cash {_wallet.Cash:0.00})");
            }));
    }

    // Unity CarDealerScreen.OnBuyClicked's tail: the car is already in the
    // garage (the page added it after the cash moved), the purchase is banked,
    // and the follow-up choice opens. Unity shows the summary block only when
    // this was the garage's second car — the starter plus this purchase.
    private void OnCarBought(CarSpec car)
    {
        SaveGarage();
        _log.LogInfo($"[Dealer] bought {car.Key}: {CarTitles.Of(car)} " +
            $"for {car.Price:0} CR (cash {_wallet.Cash:0.00}, " +
            $"{_garage.Cars.Count} cars owned)");
        bool second = _garage.Cars.Count == 2;
        SetScreen(new BoughtFirstCarScreenViewModel(
            car, second,
            new RelayCommand(() => DriveNewCar(car)),
            new RelayCommand(KeepCurrentCar)));
    }

    // Unity BoughtFirstCar.OnNewCarClicked: the new car becomes the one driven
    // (and with it the trunk capacity the delivery jobs read). Both branches
    // bank and return to the control page.
    private void DriveNewCar(CarSpec car)
    {
        if (_garage.SetCurrent(car.Key))
        {
            _log.LogInfo($"[Dealer] driving the new car: {car.Key}");
        }
        SaveGarage();
        SetScreen(CreateControlScreen());
    }

    // Unity BoughtFirstCar.OnCurrentCarClicked: the new car stays parked; the
    // car driven does not change.
    private void KeepCurrentCar()
    {
        _log.LogInfo($"[Dealer] keeping {_garage.CurrentKey ?? "no car"}");
        SaveGarage();
        SetScreen(CreateControlScreen());
    }

    // The garage's own save (PORTPLAN 51 interim): its file, its call sites
    // (a purchase, a switch, the starter seed), never the driver's scalars — a
    // dead process between the two must not lose the car that was just paid
    // for. Also runs at window close (the odometer's last bank) and on the
    // poll's throttle. Restamps so the throttle stays honest.
    public void SaveGarage()
    {
        _lastGarageSave = _clock.UtcNow;
        _garageStore.Save(_garage.Snapshot());
    }

    // The career follows the garage's current car; a live car that is not
    // that row is a free-roam detour, and the HUD says so. An unknown
    // identity (the log watcher has not caught up yet) is not a mismatch:
    // warning on a guess would flag every fresh session.
    private bool IsDrivingWrongCar() =>
        _carIdentity is { } identity
        && _garage.Current is { } current
        && !identity.Matches(current.Key);

    // The odometer feed (PORTPLAN items 31/32, Unity DrivingManager's speed
    // integration): bank this tick's distance into the car driven now, then run
    // the engine inspections. The same dt guard as the fare meter keeps a
    // stalled, torn or teleported poll out of the ledger; the speed clamp keeps
    // a one-frame glitch out of it too.
    private void IntegrateOdometer(TelemetrySample sample, double dt)
    {
        if (_garage.Current is not { } car)
        {
            return;
        }
        double speed = double.IsFinite(sample.SpeedKmh)
            ? Math.Clamp(sample.SpeedKmh, 0.0, MaxTelemetrySpeedKmh)
            : 0.0;
        if (speed <= 0.0)
        {
            return;
        }
        // Only a sane tick banks distance; a stalled or lagged poll skips the
        // add (the guard the fare meter uses) but still lets the inspections
        // run, so a long frame never delays a due engine check.
        if (dt is > 0 and < 1)
        {
            car.Drive(speed / 3600.0 * dt);
        }
        // Unity runs the inspections only while driving and only past the intro
        // (firstSceneComplete); the whole engine system is dormant before it.
        if (OnboardingDone)
        {
            RunEngineChecks(car);
        }
    }

    // Unity DrivingManager.RunEngineInspections: the weekly check is
    // informational (the schedule rolls forward), the annual one can strand the
    // car. Both fire at most once per period because the schedule rolls on the
    // due tick (EngineHealth, Unity parity).
    private void RunEngineChecks(OwnedCar car)
    {
        DateTime now = _game.GameNow;
        car.Engine.EnsureInitialized(now);
        if (car.Engine.IsWeeklyDue(now))
        {
            car.Engine.CompleteWeekly(now);
            _log.LogInfo($"[Engine] {car.Key} weekly inspection " +
                $"({car.Engine.BrokenPercent(EngineCcOf(car), car.Odometer) * 100.0:0.0000}% broken)");
        }
        if (car.Engine.IsAnnualDue(now))
        {
            bool failed = car.Engine.RunAnnual(EngineCcOf(car), car.Odometer, now);
            if (failed)
            {
                _log.LogWarning($"[Engine] {car.Key} failed its annual inspection at " +
                    $"{car.Odometer:0} km — stranded until the mechanic");
            }
            else
            {
                _log.LogInfo($"[Engine] {car.Key} passed its annual inspection at {car.Odometer:0} km");
            }
        }
    }

    // The catalogue cc behind the driven car; an unknown key reads 0 and rides
    // the smallest red-km band, never throws.
    private int EngineCcOf(OwnedCar car) => _garage.SpecOf(car)?.EngineCc ?? 0;

    // One completed job's dirt (PORTPLAN 33, Unity IncrementFilth): +1 on the
    // car driven now, clamped at the rules' cap. A settled job on an already
    // filthy car is no transition, so nothing logs. The caller banks the garage.
    private void DirtyCurrentCar()
    {
        if (_garage.Current is not { } car)
        {
            return;
        }
        int before = car.Filth.Value;
        car.Filth.Add(1);
        if (car.Filth.Value != before)
        {
            _log.LogInfo($"[Filth] {car.Key} {car.Filth.Value}/{car.Filth.Max} after the job");
        }
    }

    // TESTING HOOK (PORTPLAN items 31/32, remove when the driving slice
    // lands): a settled job's placeholder km is banked into the current car's
    // odometer so a completed taxi/delivery leg wears the mechanic without the
    // player having to drive the route. Live telemetry already banks the
    // odometer per tick, so once the real pickup/drop-off feeds distances this
    // would double-count and must go. Nothing banks on an empty garage or a
    // zero distance; the caller banks the garage.
    private void BankJobDistance(double distanceKm)
    {
        if (_garage.Current is not { } car || distanceKm <= 0.0 || !double.IsFinite(distanceKm))
        {
            return;
        }
        car.Drive(distanceKm);
        _log.LogInfo($"[Odometer] job distance {distanceKm:0.00} km banked into " +
            $"{car.Key} (odometer {car.Odometer:0.00} km)");
    }

    private void LogStandingsMove(RivalsResult? standings)
    {
        if (standings is { IsFirstTime: true })
        {
            _log.LogInfo($"standings first fix: rank {standings.CurrentRank} of {standings.TotalCompetitors}");
        }
        else if (standings is { RankChange: not 0 })
        {
            _log.LogInfo($"standings moved: {standings.PreviousRank} -> {standings.CurrentRank} of {standings.TotalCompetitors}");
        }
    }

    // Accepting settles the run straight into its receipt — the taxi ACCEPT
    // skip's delivery twin. With no driving slice the pickup/drop-off flow
    // has no inputs, so the placeholder run (8.2 km) settles here: the run
    // carries the goods money exactly as the cart moved it, every delivered
    // store earns its hub's ramp, the producing hub's owner gains
    // familiarity, and the receipt shows at once. The settle here is shared
    // by today's board DELIVER and the driving slice's drop-off later.
    private void OnDeliveryRun(DeliveryRun? run)
    {
        if (run is null) return;
        // Breakdown roll at the delivery trip start (PORTPLAN 39, Unity
        // DeliveryJobHandler's BeginTripSession): the cargo is lost (no settle,
        // no credit) and every producing hub's owner loses 1 familiarity. The
        // Delivery Insurance Shield refund waits for the reward catalog (item 61).
        if (RollTripStart(BreakdownKind.Delivery) is { } breakdown)
        {
            foreach (string owner in SourceOwners(run))
            {
                _friends.AddFamiliarity(owner, -1);
            }
            _log.LogInfo($"delivery cut by breakdown: {run.StoreKeys.Count} stores lost");
            ShowBreakdown(breakdown);
            return;
        }
        // Unity ApplyStoreDeliveryEffects runs per stop; the sum receipt
        // totals it: each store credits its producing hub's ramp, distinct
        // owners collect. A store the catalog no longer lists (content
        // changed mid-run) earns nothing instead of crashing the settle.
        List<string> owners = [];
        int credPEarned = 0;
        foreach (string key in run.StoreKeys)
        {
            if (_deliveryCatalog.Find(key) is not { } store)
            {
                continue;
            }
            DeliveryEarning earning = _career.RegisterDelivery(store.Category);
            credPEarned += earning.CredPEarned;
            if (earning.OwnerName.Length > 0 && !owners.Contains(earning.OwnerName))
            {
                owners.Add(earning.OwnerName);
            }
        }
        // Unity keys the run's familiarity on the producing hub's owner
        // once the row completes. One sourced hub rides the receipt exactly
        // like a taxi passenger; mixed-hub runs still credit every owner
        // (progress never stalls) but hide the friendship block until the
        // per-stop receipts land with the map's store→hub routing.
        FriendshipResult? friendship = null;
        if (owners.Count == 1)
        {
            FriendshipOutcome outcome = _friends.RegisterRide(owners[0]);
            friendship = new FriendshipResult(outcome.OwnerName, outcome.Level, outcome.TierIncreased);
            if (outcome.TierIncreased)
            {
                _log.LogInfo($"friendship tier up: {outcome.OwnerName} -> {outcome.Level}");
            }
        }
        else
        {
            foreach (string owner in owners)
            {
                _ = _friends.RegisterRide(owner);
            }
        }
        // Testing hook: the settled run wears the car by its own distance.
        BankJobDistance(PlaceholderDistanceKm);
        DeliveryReceipt receipt = DeliveryReceiptBuilder.Build(
            PlaceholderDistanceKm, run.PaidForGoods, run.ReceivedForGoods,
            _deliveryFares, PlaceholderStreetKnowledgePct, _wallet.Xp,
            friendship, credPEarned, _career.CredP);
        _wallet.AddDelivery(receipt);
        // Zone tick, delivery twin (Unity DeliveryJobHandler's settle): the
        // counters, interest and expansion flow exactly like the taxi hook.
        _zone.RegisterJob();
        ApplyBankInterest();
        TryExpandZone();
        RefreshStandings();
        // Dirt tick, delivery twin (PORTPLAN 33, Unity DeliveryJobHandler's
        // store arrival): a completed run is +1 on the car driven now.
        DirtyCurrentCar();
        // Checkpoint bank, same rule as the taxi dropoff: the paid run is
        // progress the player can see, so it goes to disk now.
        SaveClock();
        SaveGarage();
        _log.LogInfo($"delivery settled: {run.StoreKeys.Count} stores " +
            $"paid={receipt.PaidForGoods:0.00} received={receipt.ReceivedForGoods:0.00} " +
            $"fare={receipt.DistanceCharge:0.00} earned={receipt.TotalEarned:0.00} " +
            $"xp={receipt.XpEarned:0.00} credP=+{receipt.CredPEarned}/{_career.CredP:0} " +
            $"wallet={_wallet.Cash:0.00}");
        SetScreen(new DeliveryReceiptScreenViewModel(
            receipt, _wallet, _beat,
            new RelayCommand(() =>
                SetScreen(_deliveryScreen ?? (object)CreateControlScreen())), _rivals));
    }

    // Passenger seats of the driven car (Unity: passengerCapacity scales
    // the distance charge). Unknown car (EVO quiet, pool gap), ambiguous
    // variant (multi-variant folder, no preset announced) or 0-seat race
    // entry rides solo; a 0-seat entry still takes one customer. Builder
    // clamps too.
    private int ResolveCapacity()
    {
        if (_carIdentity?.FindIn(_cars) is { } car)
        {
            return Math.Max(1, car.Passengers);
        }

        return 1;
    }

    // Display brand for the receipt's sponsorship lines: the watcher's
    // spoken brand ("mazda") canonicalized to the onboarding spelling
    // ("Mazda") so the receipt reads like the brand page. Unknown brands
    // ride through untouched; the mileage store matches case-insensitively.
    private string ResolveBrandName()
    {
        string spoken = (_carBrand ?? "").Trim();
        foreach (string known in Content.Brands)
        {
            if (string.Equals(known, spoken, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }
        return spoken;
    }

    // Unity's customerBaseName: first + surname without the companion
    // suffix ("Klaus Berger and some colleagues" -> "Klaus Berger"), so
    // repeat rides find the same resident.
    private static string BaseCustomerName(ProceduralName customer) =>
        $"{customer.FirstName} {customer.Surname}".Trim();

    private void RefreshJob()
    {
        JobLine = _job.Phase switch
        {
            JobPhase.None => Strings.Get("NoJob"),
            JobPhase.Proposed => Strings.Format("JobProposed", _job.PickupName, _job.DropoffName),
            JobPhase.EnRoute => Strings.Format("JobEnRoute", _job.PickupName, _job.DropoffName, _job.DistanceKm),
            JobPhase.Complete => Strings.Format("JobComplete", _job.PickupName, _job.DropoffName, _job.Tip),
            _ => string.Empty,
        };
        FareText = Strings.Format("FareTotal", _job.Total);
        ShiftText = _shift.IsStarted
            ? Strings.Format("ShiftSummaryActive",
                _shift.JobsDone, _shift.Earned, FormatRemaining(_shift.Remaining(_clock.UtcNow)))
            : Strings.Format("ShiftSummary", _shift.JobsDone, _shift.Earned);
        ((RelayCommand)PickupCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DropoffCommand).RaiseCanExecuteChanged();
    }

    private static string FormatRemaining(TimeSpan remaining) =>
        remaining.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    // Road-test weekday number, read off the calendar data (no Unity-day
    // literal here: the strip owns which weekday hosts the event).
    private static int RoadTestDayOfWeek()
    {
        foreach (CalendarDay day in ControlCalendar.Week)
        {
            if (day.EventId == ControlCalendar.RoadTest)
            {
                return day.DayOfWeek;
            }
        }
        return -1;
    }

    // ---- plumbing ------------------------------------------------------------

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _voice.Dispose();
        _carBrands.Dispose();
        _telemetry.Dispose();
    }
}
