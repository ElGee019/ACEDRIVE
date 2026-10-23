using System.Globalization;
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
public sealed class MainViewModel : ViewModelBase, IVoiceHost, IFleetHost, IStoryHost, IDisposable
{
    private readonly ITelemetrySource _telemetry;
    private readonly IClock _clock;
    private readonly VoiceBeat _beat;
    private readonly GameClock _game;
    private readonly RoadTestLedger _roads;
    // The single writer of every save file: the stores, the snapshot assembly
    // and the write throttles live there, never in this class (SRP).
    private readonly GameStateSaver _saver;
    // The fleet's condition (odometer, inspections, filth, insurance): every
    // path that wears an owned car goes through it.
    private readonly FleetService _fleet;
    private readonly TaxiShift _shift;
    private readonly ISettingsStore _store;
    // The settings page's RESET GAME: the save wipe plus the replacement
    // process. Both null keeps the button off pages built without them.
    private readonly ISaveDataResetter? _saveResets;
    private readonly IAppRestarter? _restarter;
    private readonly ILog _log;
    private readonly FareTable _fares;
    private readonly DriverWallet _wallet;
    private readonly DailyStreak _streak;
    private readonly OperatingZone _zone;
    // Lifetime settled jobs (PORTPLAN 24, Unity jobsCompleted): the dealer and
    // mechanic gates read this; the zone's own counter resets per expansion.
    private readonly JobLedger _jobs;
    // The business side of the POI directory (PORTPLAN 24): checks the street
    // and job thresholds and draws a free house for every newly opened place.
    private readonly PoiSpawner _poiGrowth;
    // What the driven car carries (PORTPLAN 26): the trunk page lists it and
    // the EAT tap consumes from it. Persisted through _saver, never here.
    private readonly Trunk _trunk;
    // The item database (PORTPLAN 26): the trunk page reads each carried
    // stack's hunger value from the catalogue row its name resolves.
    private readonly StoreCatalog _storeCatalog;
    private readonly IFriendshipStore _friends;
    // XP world standings backend (PORTPLAN 60, Unity RivalsService): the
    // receipt's rivals block reads through it on every settle.
    private readonly IRivalsService _rivals;
    // The dynamic car-rarity board behind the XP multiplier (PORTPLAN 35,
    // Unity CarRarityService): reads are cached, the km write is fire-and-forget.
    private readonly ICarRaritySource _rarity;
    // Zone, best-shift and country pushes (Unity FirestoreService + CountryKm).
    private readonly IPlayerTelemetry _online;
    // The persisted best single shift (PORTPLAN 12): the settle path banks it
    // and the online board receives it at the shift end.
    private readonly ShiftRecord _shifts;
    // Best-single-shift world standings (Unity ShiftStandingsService): the
    // shift-ended page reads it and the board updates arrive live.
    private readonly IShiftStandings _shiftBoard;
    // Lifetime km per driven brand behind the receipt's sponsorship rate
    // (PORTPLAN 35, Unity PlayerData.brandDistanceDriven).
    private readonly IBrandMileageStore _brands;
    // The car pool behind the road-test ledger, reused for the receipt's
    // passenger lookup (the driven car's ks id resolves against it).
    private readonly IReadOnlyList<CarSpec> _cars;
    private readonly IContentSource _content;
    // WHY read through on demand: Data/*.json edits (wake aliases, transcript
    // confusions) take effect while the overlay runs; screens built later pick
    // up the new snapshot automatically.
    private OnboardingContent Content => _content.Content;
    // The introduction walk + its errand (PORTPLAN Career Start 1-5): owns the
    // persisted progress, the profile, the errand POI and the trunk read.
    private readonly OnboardingFlow _story;
    private readonly TodoTable _todos;
    private readonly IVoiceListener _voice;
    private readonly ICarBrandSource _carBrands;
    // The delivery distance-charge table (no forfait/tip/fine): the dropped
    // goods are paid by distance alone (Data/delivery_fares.json).
    private readonly DeliveryFareTable _deliveryFares;
    // CredP balance + owned hubs, shared by the board and the hub page.
    // In-memory until the persistent save lands (PORTPLAN 51).
    private readonly DeliveryCareer _career;
    // The delivery board. A placed grocery/coffee store appends its row at
    // runtime, so this is the live catalogue, not only the JSON snapshot.
    private DeliveryCatalog _deliveryCatalog;
    // The proposal draw behind the taxi page: two map addresses, the
    // persisted resident at each door and the home/residence flip
    // (Data/name_pools.json, Germany pools + Unity suffix list, PORTPLAN
    // 11/13/45/61).
    private readonly TaxiJobProposer _proposals;
    // The customer request draw (PORTPLAN item 11): suffix/passive, speed
    // challenge and the car-stat quiz, with its own seeded stream.
    private readonly ICustomerRequestRoller? _requests;
    // Timer length, tier chances and quiz categories; the host also uses the
    // category count for the per-car asked-history reset.
    private readonly CustomerRequestTable _requestTable;
    // Speed-challenge patrols (Phase C): one seeded plan per accepted job.
    private readonly PolicePatrolPlanner? _police;
    // The last live track length, so a patrol planned after the request page
    // sits on the same lap offsets the HUD resolves.
    private double? _trackLengthMeters;
    // The placeholder free-roam map: the HUD, the onboarding home and the
    // taxi draw all read the same streets and lap geometry (PORTPLAN Step 1).
    private readonly AddressMap _addressMap;
    // The discovered POIs behind the addresses page (PORTPLAN 24/27): the
    // story and the business spawner fill it, the addresses page reads it.
    private readonly PoiDirectory _poiDirectory;
    // The directory's own file: every newly opened business is banked once.
    private readonly IPoiDirectoryStore _poiStore;
    // The driving-slice balance the meter, the HUD radius and the distance
    // line share (Data/taxi_rules.json).
    private readonly TaxiRules _taxiRules;
    // The town itself: citizens drift with the shelves, demand empties them,
    // and the size is banked on exit (PORTPLAN 25).
    private readonly CityEconomy _city;
    // The appetite: +1 per crossed game hour, banked with the reading for the
    // next session (PORTPLAN 26).
    private readonly Hunger _hunger;
    // The owned cars + the car driven now (PORTPLAN item 30). The garage banks
    // its own garage.json: the catalogue row behind each car is the data, the
    // per-car state (odometer, insurance) grows with items 31/32/34.
    private readonly Garage _garage;
    // The insurance table (PORTPLAN 34): the nightly charge rate and the broker
    // page's daily-cost math (Data/insurance_rules.json).
    private readonly InsuranceRules _insuranceRules;
    // The trip-start breakdown roll (PORTPLAN 39): a seeded instance built in
    // the composition root, never the shared global (AGENTS.md).
    private readonly BreakdownStrategy _breakdowns;
    // The 200 px companion: the poll pushes it one snapshot so
    // the HUD never opens its own timer or shared-memory handle.
    private readonly ITelemetryHud? _hud;
    private MainControlScreenViewModel? _control;
    // The calendar page is built once per session so its week and day rows
    // survive leaving the page; the poll refreshes it on the minute guard.
    private CalendarScreenViewModel? _calendar;
    // The menu is built once per session so its clock keeps ticking behind
    // another page; the poll refreshes it on the minute guard.
    private MenuScreenViewModel? _menu;
    private DeliveryJobScreenViewModel? _deliveryScreen;
    private TaxiJobProposalViewModel? _proposal;
    private CustomerRequestScreenViewModel? _requestScreen;
    // A rolled request waits out the drop-off task text before it shows; the
    // deadline is real time, never a tick count.
    private CustomerRequest? _pendingRequest;
    private DateTime _requestDueAt;
    // The speed challenge arms its patrols once at the pickup; the request
    // page's BACK must not draw a second plan later in the ride.
    private bool _speedChallengeArmed;
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

    // Same idea for the odometer: the wear feed writes the current car's
    // distance to garage.json on a wall-time throttle, never per 10 Hz tick.
    // One garage record write per interval; the day flip and the settles bank
    // it too, so a kill costs at most this window of kilometres.
    private const double OdometerSaveSec = 60.0;

    // The warning fires on the transition into the blocked band (Unity's
    // TOO HUNGRY edge), not per poll and not per hour.
    private bool _hungerBlocked;

    // Companion parity: hidden until "hey {brand}", idle hides after 15 s,
    // one utterance fires one button (muted until the final fragment).
    private const double IdleHideSec = 15.0;
    private const string DefaultBrand = "mazda";

    private DateTime _lastActivity;
    // The intro (opening screen) is the constructor's first paint; the first
    // hidden -> shown edge must reveal it instead of jumping to control. Every
    // later call keeps the existing "open control" rule.
    private bool _introShown;
    private bool _resizing;

    // Wake brand = the car currently driven in ACE (watcher-published),
    // never the saved favorite.
    private string _carBrand = DefaultBrand;
    // Car identity behind the brand, when the logs announced one: the receipt's
    // passenger lookup resolves against the car pool (one brand spans 1- and
    // 4-seaters, one folder spans showroom variants).
    private CarIdentity? _carIdentity;
    private readonly DispatcherTimer _timer;
    private readonly VoiceRouter _voiceRouter;
    private DateTime _lastTick;
    private bool _disposed;

    private TaxiJobState _job = new();
    // A job is live from ACCEPT (the drive to the pickup) until the drop-off
    // settles. The idle-shift rule and the overlay call both read this one
    // definition, so a new leg cannot mean two different things.
    private bool JobInProgress => _job.Phase is JobPhase.ToPickup or JobPhase.EnRoute;
    // The passenger-leg distance. Lives outside the job so a refused pickup or
    // a breakdown can discard the ride without leaving a half-run meter.
    private readonly TaxiDriveMeter _meter;
    // WHICH shown proposal already paid the dirty-car friendship penalty
    // (Unity's _tooDirtyArrived guard): tapping ACCEPT twice on the same
    // proposal must not charge the customer twice. A fresh proposal is a new
    // TaxiJobState instance (Unity's new job).
    private TaxiJobState? _dirtyRefusedJob;
    private int _lastShiftSecs = -1;

    // The last painted telemetry readouts. The 10 Hz poll re-formats only when
    // the shown (quantised) value changes, so a parked car or a paused game
    // costs no string work per tick (AGENTS.md hot-path discipline). The
    // sentinels force the first paint.
    private const int Unpainted = -1;
    private int _lastPacketId = Unpainted;
    private int _lastSpeedKmh = Unpainted;
    private int _lastGear = int.MinValue;
    private int _lastRpm = Unpainted;
    private int _lastThrottlePct = Unpainted;
    private int _lastBrakePct = Unpainted;
    // Which "no physics yet" line is painted, so the waiting state also costs
    // one lookup on its first tick instead of one per tick.
    private string? _lastWaitingKey;

    // Dropoff placeholders until the driving slice feeds real inputs: the
    // agreed 8.2 km ride with every extra on except the speeding fine (the
    // receipt keeps its empty fine line, but no fine is charged). The car's
    // class/rarity come from the owned car driven now; these fallbacks cover
    // an unknown catalogue row and the unknown-identity case. An unowned car
    // deliberately reads 0 × 0 so it earns no XP.
    private const double PlaceholderDistanceKm = 8.2;
    private const int FallbackCarClass = 1;
    private const int FallbackCarRarity = 1;
    private const int PlaceholderStreetKnowledgePct = 100;

    public MainViewModel(
        ITelemetrySource telemetry, IClock clock, GameClock game,
        TaxiShift shift, IShiftStore shiftStore, ISettingsStore store,
        ILog log, FareTable fares, TaxiJobProposer proposals,
        IOnboardingStore onboarding, IContentSource content,
        IProfileStore profiles, IPlayerNameRegistry playerNames,
        AddressMap addressMap, string? detectedPlayerName,
        IVoiceListener voice, ICarBrandSource carBrands,
        TodoTable todos, DeliveryCatalog deliveryCatalog, DeliveryCareer career,
        DeliveryFareTable deliveryFares,
        CityEconomy city, ICityStore cityStore,
        RoadTestLedger roadTests, IRoadTestStore roadTestStore,
        IGameClockStore clockStore, int startWeek,
        VoiceBeat beat, DriverWallet wallet, IDriverStore driverStore,
        DailyStreak streak, OperatingZone zone, IFriendshipStore friendships, IReadOnlyList<CarSpec> cars,
        IBrandMileageStore brands, IRivalsService rivals,
        ICarRaritySource rarity, IPlayerTelemetry playerTelemetry, ShiftRecord shifts,
        IShiftStandings shiftStandings,
        Hunger hunger, IHungerStateStore hungerStore,
        Garage garage, IGarageStore garageStore, InsuranceRules insuranceRules,
        BreakdownStrategy breakdowns,
        PoiGenerator pois,
        PoiDirectory poiDirectory, IPoiDirectoryStore poiStore,
        PoiSpawner poiGrowth, JobLedger jobs,
        IPreferencesStore preferencesStore, PlayerPreferences preferences, ICultureService culture,
        Trunk trunk,
        TaxiRules taxiRules,
        ITelemetryHud? hud = null,
        IReadOnlyList<IPoiArrivalHandler>? poiHandlers = null,
        ICustomerRequestRoller? requests = null,
        CustomerRequestTable? requestTable = null,
        PolicePatrolPlanner? police = null,
        ISaveDataResetter? saveResets = null,
        IAppRestarter? restarter = null,
        StoreCatalog? storeCatalog = null)
    {
        _telemetry = telemetry;
        _clock = clock;
        _beat = beat;
        _game = game;
        _roads = roadTests;
        _displayWeek = startWeek;
        _shift = shift;
        _store = store;
        _saveResets = saveResets;
        _restarter = restarter;
        _log = log;
        _fares = fares;
        _proposals = proposals;
        _requests = requests;
        _requestTable = requestTable ?? CustomerRequestTable.Empty;
        _police = police;
        _addressMap = addressMap;
        _poiDirectory = poiDirectory;
        _poiStore = poiStore;
        _poiGrowth = poiGrowth;
        _jobs = jobs;
        _taxiRules = taxiRules;
        _meter = new TaxiDriveMeter(taxiRules.MaxMeterSpeedKmh);
        _content = content;
        _todos = todos;
        _voice = voice;
        _carBrands = carBrands;
        _deliveryCatalog = deliveryCatalog;
        _deliveryFares = deliveryFares;
        _career = career;
        _city = city;
        _hunger = hunger;
        _garage = garage;
        _insuranceRules = insuranceRules;
        _breakdowns = breakdowns;
        _hud = hud;
        if (_hud is not null)
        {
            // The HUD only knows it is parked at the pinned door; the job
            // phase decides whether that confirms the pickup or the drop-off.
            _hud.JobEnterRequested += (_, _) => OnJobEntered();
        }
        _wallet = wallet;
        _streak = streak;
        _zone = zone;
        _trunk = trunk;
        _storeCatalog = storeCatalog ?? StoreCatalog.Default;
        _friends = friendships;
        _brands = brands;
        _rivals = rivals;
        _rarity = rarity;
        _online = playerTelemetry;
        _shifts = shifts;
        _shiftBoard = shiftStandings;
        _cars = cars;
        // The persistence collaborator is built from the injected services, so
        // the early banks below (garage seed, streak bonus) already route
        // through it instead of assembling snapshots here.
        _saver = new GameStateSaver(
            clock, game, clockStore, driverStore, garageStore, cityStore,
            hungerStore, roadTestStore, shiftStore, wallet, streak, zone, trunk,
            garage, city, hunger, roadTests, shift, shifts, jobs);
        _fleet = new FleetService(
            garage, cars, game, wallet, insuranceRules, log, this, DriverRatingPercent);
        // The POI entry router: the store handler rides in from the root, the
        // business interiors are built here because they bank through this host
        // and change the page themselves (dealer -> bought-car choice).
        var poiArrivals = new PoiArrivalRouter(BuildPoiHandlers(poiHandlers));
        // The story collaborator owns the persisted walk + profile + errand POI;
        // it is built before the garage seed below reads its progress.
        _story = new OnboardingFlow(
            onboarding, content, profiles, playerNames, addressMap, detectedPlayerName,
            pois, poiDirectory, poiStore, poiArrivals, trunk,
            preferencesStore, preferences,
            culture, beat, hud, garage, () => _displayWeek, SaveGarage,
            () => _carBrand, log, this);
        _carBrands.BrandChanged += (_, e) => OnCarBrand(e.Brand, e.Identity);
        // Business catch-up at boot (Unity MapScreen.Show's generator checks):
        // a save whose street/job counters already crossed a threshold opens
        // the missing doors now. A fresh career is still in onboarding, so the
        // gate keeps the business list empty until the introduction ends.
        GrowPois();
        // Grandma's car is the first OwnedCar (PORTPLAN Career Start item 3 +
        // item 30): a save whose onboarding already ran but whose garage is
        // still empty — every save written before this slice — seeds the car
        // it picked instead of opening on an empty garage. Unity creates the
        // starter OwnedCar at the pick itself, so this is the same state one
        // session later; the key resolves through the catalogue for specs.
        if (OnboardingDone && _garage.Cars.Count == 0 && _story.Progress.GrandmaCarId is { Length: > 0 } starter)
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
        // The day-flip detector starts on the session's opening day, so the
        // first poll does not report a transition.
        _lastDay = _game.Today;
        _lastActivity = _clock.UtcNow;
        // The router's warn-free reads go through IVoiceHost (see the explicit
        // implementations below), so the host surface stays small.
        _voiceRouter = new VoiceRouter(this, clock, log, hud);
        _voice.Heard += (_, e) => OnVoiceHeard(e.Text, e.IsFinal);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Poll();
        _lastTick = _clock.UtcNow;

        TaxiShiftCommand = new RelayCommand(OpenTaxiShift);
        GarageCommand = new RelayCommand(OpenGarage);
        PickupCommand = new RelayCommand(ConfirmPickup, () => _job.Phase == JobPhase.ToPickup);
        DropoffCommand = new RelayCommand(ConfirmDropoff, () => _job.Phase == JobPhase.EnRoute);
        ToggleShowCommand = new RelayCommand(() =>
        {
            if (IsShown) Hide(); else Show();
        });

        RefreshJob();
        StatusText = Strings.Get("WaitingForEvo");
        SpeedText = Strings.Format("SpeedKmh", 0);
        GearText = Strings.Get("GearUnknown");
        DetailText = string.Empty;

        // First paint: a finished introduction opens on the menu (the three
        // tiles); an unfinished one resumes the walk page the last session
        // closed on (PORTPLAN Career Start items 1-5). The tour leg is
        // re-pinned first so a save that is mid-tour still has its HUD
        // destination after the relaunch, and the companion is revealed
        // because the birthday hand-over that used to do it no longer replays.
        _story.ResumeTour();
        if (OnboardingDone)
        {
            _hud?.Reveal();
        }
        SetScreen(OnboardingDone ? CreateMenuScreen() : _story.StartPage());

        // The VM's default: hidden until called when a mic is present, visible
        // otherwise so the shell stays usable (and testable). The app root
        // calls Show() at launch, so a real start opens on the intro either way.
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

    /// <summary>Temporary garage entry (PORTPLAN 30): a You-menu page in Unity,
    /// opened from the home page; moves behind the marker network (items 24/59)
    /// with the rest.</summary>
    public ICommand GarageCommand { get; }

    // ---- onboarding navigation -----------------------------------------------

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
    /// size may be restored: a finished introduction opens on the menu, while
    /// an unfinished one resumes a walk page.
    /// </summary>
    public bool OnboardingDone => _story.Done;

    /// <summary>
    /// True once RESET GAME wiped the save files: the window's close path must
    /// not bank the live session back onto disk while the replacement process
    /// starts (or if starting it failed and the player closes this one).
    /// </summary>
    public bool SkipShutdownSave { get; private set; }

    public void ChooseBrand(string brand) => _story.ChooseBrand(brand);

    public void ChooseCar(string carId) => _story.ChooseCar(carId);

    public void ShowDialogue() => _story.ShowDialogue();

    public void FinishDialogue() => _story.FinishDialogue();

    // The host slice the story flow sees (interface segregation): page swaps,
    // panel reveal/hide, the control page and the shared driver save.
    object? IStoryHost.CurrentScreen => CurrentScreen;
    bool IStoryHost.IsShown => IsShown;
    void IStoryHost.ShowScreen(object? screen) => SetScreen(screen);
    void IStoryHost.Present(object screen) => Present(screen);
    void IStoryHost.Hide() => Hide();
    object IStoryHost.ControlScreen() => CreateControlScreen();
    object IStoryHost.MenuScreen() => CreateMenuScreen();
    void IStoryHost.SaveDriver() => SaveDriver(_rivals.LastResult);

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

    // Built once and reused. The taxi entry opens the job proposal page (port
    // of Unity TaxiJobProposalScreen); delivery opens the career page (port
    // of Unity DeliveryJobScreen). The weekly strip lives on its own page
    // (see CreateCalendarScreen).
    private MainControlScreenViewModel CreateControlScreen()
    {
        _control ??= new MainControlScreenViewModel(
            _beat, _todos, _shift,
            new ControlActions(
                TaxiShiftCommand, new RelayCommand(OpenDelivery),
                new RelayCommand(OnCarHaul)),
            new RelayCommand(() => SetScreen(CreateMenuScreen())));
        return _control;
    }

    // Built once and reused; boots on the resumed week (if any), not the
    // stamp default, so the header is right on the first paint. The calendar
    // day buttons have no destinations until the freeroam map lands, so
    // tapping the highlighted day just logs — except the road-test day, which
    // banks the week's car as tested and switches to the next pick (manual
    // stand-in until the road-test drive flow lands). Off-gate taps never
    // reach this action: the page's own day command warns them before
    // dispatching.
    private CalendarScreenViewModel CreateCalendarScreen()
    {
        _openDayAction ??= new ParameterizedCommand<int>(OnDay);
        _calendar ??= new CalendarScreenViewModel(
            _beat, _game, _clock, _roads,
            CalendarStamp.Default with { Week = _displayWeek },
            _openDayAction, new RelayCommand(() => SetScreen(CreateMenuScreen())));
        return _calendar;
    }

    private void OpenCalendar() => SetScreen(CreateCalendarScreen());

    private void OnDay(int day)
    {
        if (day == _roadTestDay)
        {
            string before = DescribeRoadTest();
            _roads.MarkTested();
            SaveRoadTest();
            _log.LogInfo($"road test done: {before} -> {DescribeRoadTest()}");
            _calendar?.Refresh();
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
        // A hub bought on the previous visit may have opened store doors; the
        // board must include them before the cached page paints.
        GrowPois();
        _deliveryScreen ??= new DeliveryJobScreenViewModel(
            _deliveryCatalog, _career, _city, _log,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            new ParameterizedCommand<DeliveryRun>(OnDeliveryRun),
            new RelayCommand(OpenHub),
            _beat);
        _deliveryScreen.Refresh(_deliveryCatalog);
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
        int carClass = car is { Class: > 0 } ? car.Class : FallbackCarClass;
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
            carName, carClass, DriverRatingPercent, PlaceholderHaulMinutes);
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
    // minutes; the settle prices the difference). The pay formula reads the
    // wallet's persisted rating — rate = 0.83 * (rating * tier / 100).
    private const int PlaceholderHaulMinutes = 30;

    // The persisted skills average, rounded to the whole percent the haul
    // receipt and the fleet's insurance math take (Unity uses the float
    // overall directly; the receipt model is an int).
    private int DriverRatingPercent =>
        (int)Math.Round(_wallet.Rating.Overall, MidpointRounding.AwayFromZero);

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
        // Unity's backfill runs when the career opens: the newly owned
        // category places its first store before the board is shown again.
        GrowPois();
        _deliveryScreen?.Refresh(_deliveryCatalog);
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
    public void SaveClock() => _saver.SaveClock(_displayWeek);

    /// <summary>
    /// Banks the road-test pick. Two moments need it: a session opening (a
    /// rebuild/Stop-Debugging kills the process before any test is driven, and
    /// the next session must offer that very same car) and a completion (the
    /// only event allowed to move the car). Never throws (the store swallows IO).
    /// </summary>
    private void SaveRoadTest() => _saver.SaveRoadTest();

    /// <summary>
    /// Banks the town for the next session (PORTPLAN 25): the city size plus
    /// the shelf state that drives it. Exit-only, like Unity's storeStates,
    /// which are never written mid-session — the drift is world flavour, so a
    /// killed process costs an hour of growth and no player progress. Never
    /// throws (the store swallows IO).
    /// </summary>
    public void SaveCity() => _saver.SaveCity();

    /// <summary>
    /// Banks the hunger reading for the next session (Unity PlayerData.hunger
    /// survives restarts): the meter plus the one-shot event state resume
    /// instead of re-seeding at 0. Exit-only, like the city. Never throws
    /// (the store swallows IO).
    /// </summary>
    public void SaveHunger() => _saver.SaveHunger();

    /// <summary>
    /// Banks the taxi shift (PORTPLAN 12) with its wall-clock stamps, so the
    /// 30-minute idle rule keeps running while the overlay is closed. Banked
    /// on every shift-changing event, not just exit. Never throws (the store
    /// swallows IO).
    /// </summary>
    public void SaveShift() => _saver.SaveShift();

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
        // Some pages run their own timer or wait for CONTINUE; hiding one
        // mid-read would lose it (customer request, shift summary, receipts),
        // so they opt out like a POI page.
        bool keepOpen = CurrentScreen is IKeepPanelOpen;
        if (IsShown && !_resizing && !dialogueOpen && !poiOpen && !keepOpen
            && (now - _lastActivity).TotalSeconds >= IdleHideSec)
        {
            Hide();
        }

        // The request countdown is part of the one allowed loop: integer
        // seconds only, and a timeout finishes the ask exactly once. It runs
        // whether or not EVO is still attached so a closed game cannot freeze
        // an open request.
        _requestScreen?.Tick(now);

        // A rolled ask replaces the drop-off task text only once the read-in
        // delay has passed. One null check per tick; the clock comparison is
        // the only thing that appears while an ask is waiting.
        if (_pendingRequest is { } pending && now >= _requestDueAt)
        {
            _pendingRequest = null;
            ShowCustomerRequest(pending);
        }

        // The control page date stays live whether or not EVO is attached.
        // Refresh self-guards on the minute: no per-tick allocations.
        // A timed-out shift ends here (Unity 30-min idle rule) so the taxi
        // button flips back to START even when the player never taps it.
        // WHY the job guard: an accepted job is work in progress, and a long
        // drive to a far door must not idle the shift out mid-ride.
        if (!JobInProgress && _shift.IsTimedOut(now) && _shift.TryEndTimedOut(now, out TimeSpan idle))
        {
            EndShift(idle);
        }
        // Voice acknowledgment: the beat drains first so a due tap acts at
        // its end, then flash expiry runs on the clock, not the minute
        // guard: a half-second teal glow must clear inside the same minute
        // it started.
        _beat.Poll();
        _control?.UpdateFlash();
        _control?.Refresh();
        _calendar?.UpdateFlash();
        _calendar?.Refresh();
        _menu?.UpdateFlash();
        _menu?.UpdateReveal();
        _menu?.Refresh();

        // Game-day flip: the calendar highlight moved. One int compare per
        // tick, work only on transitions (hot-path discipline).
        int today = _game.Today;
        if (today != _lastDay)
        {
            int prev = _lastDay;
            _log.LogInfo($"calendar day changed: {prev} -> {today}");
            _lastDay = today;
            // Sunday-midnight crossing completes the week: the display week
            // rolls and the moment persists. The road-test pick deliberately
            // does NOT rotate here — an untested car stays on offer until it
            // is actually driven (PORTPLAN 49).
            if (prev == ControlCalendar.Sunday && today == ControlCalendar.Monday)
            {
                _displayWeek++;
                if (_calendar is not null)
                {
                    _calendar.DisplayWeek = _displayWeek;
                    // The header repaints now instead of on the next tick: a
                    // week opening is the one transition the poll already ran
                    // its refresh before.
                    _calendar.Refresh();
                }
                if (_menu is not null) _menu.DisplayWeek = _displayWeek;
                _log.LogInfo($"week {_displayWeek} opened: road test stays {DescribeRoadTest()}");
            }
            SaveClock();
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
        // game clock, so it lands whether or not EVO is attached. A charged
        // premium is progress the player loses on a kill, so it banks now.
        if (_fleet.ChargeDailyInsurance())
        {
            SaveDriver(_rivals.LastResult);
        }

        // Wall-clock bank: runs before the telemetry read so it also holds
        // while EVO is closed, and before the shift paths because a banked
        // moment is exactly what a kill between sessions would otherwise
        // lose. SaveClock restamps, so this costs one write per ClockSaveSec.
        if ((now - _saver.LastClockSave).TotalSeconds >= ClockSaveSec)
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
            ShowWaitingReadouts();
            _hud?.Update(null, _garage.Current?.Odometer, _telemetry.GameRunning);
            return;
        }

        IsLive = true;
        if (s.Track is { LengthMeters: > 0.0 } track && double.IsFinite(track.LengthMeters))
        {
            _trackLengthMeters = track.LengthMeters;
        }
        ShowLiveReadouts(s);

        // Odometer feed (PORTPLAN items 31/32, Unity DrivingManager's odometer
        // integration): every live tick banks the distance the current car
        // covered into the wear deck's input, and the engine inspections ride
        // the drive. The garage file is banked on the same wall-time throttle
        // as the clock, so a kill costs at most the window.
        _fleet.IntegrateOdometer(s, dt);
        _hud?.Update(
            s, _garage.Current?.Odometer, gameRunning: true, _fleet.IsDrivingWrongCar());
        if ((now - _saver.LastGarageSave).TotalSeconds >= OdometerSaveSec)
        {
            SaveGarage();
        }

        // Meter runs while the passenger is aboard: the accumulator applies
        // the stalled-frame guard and the speed clamp, so a torn read cannot
        // inflate the fare.
        if (_job.Phase == JobPhase.EnRoute)
        {
            double before = _meter.DistanceKm;
            _meter.Add(s.SpeedKmh, dt);
            if (_meter.DistanceKm != before)
            {
                _job.DistanceKm = _meter.DistanceKm;
                _job.Fare = TaxiFare.ComputeBase(_job.DistanceKm, _fares);
                RefreshJob();
            }

            if (_job.SpeedChallengeAgreed)
            {
                CheckPolice(s);
            }
        }
    }

    // Live readouts: one format per changed value, never one per tick. Each
    // guard quantises to what the resx actually prints, so the cache can
    // never hold a stale-looking value.
    private void ShowLiveReadouts(TelemetrySample sample)
    {
        _lastWaitingKey = null;
        if (sample.PacketId != _lastPacketId)
        {
            _lastPacketId = sample.PacketId;
            StatusText = Strings.Format("EvoLive", sample.PacketId);
        }
        int speed = (int)Math.Round(sample.SpeedKmh);
        if (speed != _lastSpeedKmh)
        {
            _lastSpeedKmh = speed;
            SpeedText = Strings.Format("SpeedKmh", sample.SpeedKmh);
        }
        if (sample.Gear != _lastGear)
        {
            _lastGear = sample.Gear;
            GearText = TelemetryConventions.GearDisplay(sample.Gear);
        }
        int rpm = sample.Rpm;
        int throttle = (int)Math.Round(sample.Gas * 100.0f);
        int brake = (int)Math.Round(sample.Brake * 100.0f);
        if (rpm != _lastRpm || throttle != _lastThrottlePct || brake != _lastBrakePct)
        {
            _lastRpm = rpm;
            _lastThrottlePct = throttle;
            _lastBrakePct = brake;
            DetailText = Strings.Format("EvoDetail", sample.Rpm, sample.Gas, sample.Brake);
        }
    }

    // The "no physics yet" state paints once per entering it, then stays put
    // (the string never changes while EVO is absent, so re-formatting it per
    // tick would be pure waste).
    private void ShowWaitingReadouts()
    {
        _lastPacketId = Unpainted;
        _lastSpeedKmh = Unpainted;
        _lastGear = int.MinValue;
        _lastRpm = Unpainted;
        _lastThrottlePct = Unpainted;
        _lastBrakePct = Unpainted;
        string key = _telemetry.GameRunning ? "EvoNoShm" : "WaitingForEvo";
        if (key == _lastWaitingKey)
        {
            return;
        }

        _lastWaitingKey = key;
        StatusText = Strings.Get(key);
        SpeedText = Strings.Format("SpeedKmh", 0);
    }

    // ---- visibility + voice --------------------------------------------------

    // The host slices the collaborators see (interface segregation): only the
    // reads they actually make. IsShown, CurrentScreen, Show, RecordActivity
    // and OnboardingDone are already public and satisfy their interfaces
    // directly.
    OnboardingContent IVoiceHost.Content => Content;
    public CarIdentity? CarIdentity => _carIdentity;
    public string CarBrand => _carBrand;
    public IReadOnlyList<string> Brands => Content.Brands;

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
        // WHY: from ACCEPT to the receipt the HUD owns the screen and the
        // mic (guidance + customer request). A call mid-job would let the
        // player open the job centre over the ride and start a second job,
        // so the call is ignored until the job settles or is abandoned.
        if (JobInProgress)
        {
            _log.LogInfo("overlay call ignored: job in progress");
            return;
        }

        RecordActivity();
        if (IsShown) return;
        IsShown = true;
        // The single "the app was called" seam: the Ctrl+Shift+C hotkey and
        // the wake word both land here. The first show of a hidden start
        // reveals the page already built in the constructor (the resumed walk
        // page, or the menu past the introduction); every later show opens the
        // menu page (Jobs leads to the control page) instead of resuming
        // whatever page was last open. It rides the hidden -> shown edge only —
        // a reveal while already visible must not yank the player off the page
        // they are using.
        if (!_introShown && CurrentScreen is not null)
        {
            _introShown = true;
            _log.LogInfo("overlay shown (first page)");
            return;
        }
        _introShown = true;
        SetScreen(CreateMenuScreen());
        _log.LogInfo("overlay shown (menu)");
    }

    private MenuScreenViewModel CreateMenuScreen() =>
        _menu ??= new MenuScreenViewModel(
            new RelayCommand(OpenWallet),
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            new RelayCommand(OpenCalendar),
            new RelayCommand(OpenTrunk),
            new RelayCommand(OpenSettings),
            new RelayCommand(OpenHome),
            _hunger, _garage,
            _beat,
            _game,
            _clock,
            _displayWeek);

    // The home page (Unity YouScreen): the player's own doors, starting with
    // the garage. A fresh page per visit like the wallet, so nothing stale
    // survives between openings; BACK returns to the menu it was opened from.
    private void OpenHome() => SetScreen(CreateHomeScreen());

    private HomeScreenViewModel CreateHomeScreen() =>
        new(GarageCommand,
            new RelayCommand(OpenAddresses),
            new RelayCommand(() => SetScreen(CreateMenuScreen())),
            _beat);

    // The addresses page (PORTPLAN 24/27): the discovered POIs alphabetic,
    // the temporary stand-in for markers on the map. A row pins the HUD's
    // drive-to line; driving there and pressing ENTER enters the place.
    private void OpenAddresses() =>
        SetScreen(new AddressesScreenViewModel(
            _poiDirectory, _hud,
            new RelayCommand(() => SetScreen(CreateHomeScreen())),
            _beat));

    // The trunk page (PORTPLAN 26, Unity YouScreen's TrunkScreen) is rebuilt
    // on every visit like Unity's RebuildTrunkScreen: a shop purchase made
    // since the last look must show, so the page is never cached. EAT banks
    // the trunk and the appetite together — they live in different files, and
    // a bite moves both.
    private void OpenTrunk() =>
        SetScreen(new TrunkScreenViewModel(
            _trunk, _hunger, _garage, _storeCatalog,
            new RelayCommand(() => SetScreen(CreateMenuScreen())),
            OnTrunkItemEaten,
            _beat));

    private void OnTrunkItemEaten()
    {
        SaveDriver(_rivals.LastResult);
        SaveHunger();
    }

    // The Settings tile reuses the first-run unit + language page: selections
    // save and apply immediately through the walk's writer, and BACK comes
    // back here instead of advancing the introduction (the walk is done). The
    // reset action is passed only when the composition root injected the
    // wipe + restart pair; otherwise the page keeps its first-run shape.
    private void OpenSettings()
    {
        Func<bool>? resetGame =
            _saveResets is null || _restarter is null ? null : ResetGame;
        SetScreen(_story.CreateSettingsPage(
            () => SetScreen(CreateMenuScreen()), resetGame));
    }

    /// <summary>
    /// Settings' RESET GAME: wipes every save file, then hands over to a fresh
    /// process that boots from the introduction like a new install. A false
    /// result leaves this session running with its failure line on the page.
    /// </summary>
    private bool ResetGame()
    {
        // WHY stop the poll first: its throttled banks would otherwise
        // recreate a save file between the wipe and the process exit.
        _timer.Stop();
        if (_saveResets is null || _restarter is null || !_saveResets.DeleteAll())
        {
            _timer.Start();
            _log.LogWarning("game reset aborted: not every save file could be removed");
            return false;
        }

        // The wipe is final: a restart failure must not let the close path
        // bank the old career back onto disk.
        SkipShutdownSave = true;
        _log.LogInfo("game reset: save data removed, restarting from the introduction");
        if (_restarter.Restart()) return true;
        _timer.Start();
        return false;
    }

    private void Hide()
    {
        if (!IsShown) return;
        IsShown = false;
        _voiceRouter.ResetMute();
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
            _timer.Dispatcher.Invoke(() => _voiceRouter.Handle(text, isFinal));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // WHY: same shutdown race as OnCarBrand: the post or the inline
            // UI-thread run can meet a dispatcher that is going down. Screen
            // commands only touch VM state and stores that swallow their own
            // IO, so these two types are the whole recoverable set; a bug
            // inside the router stays visible instead of being muted here.
        }
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
            // Unity parity: the shift-ended page takes over and the tap stops
            // here. The player reads the result, comes back to the control
            // page and taps TAXI again for a fresh shift.
            EndShift(idle);
            return;
        }
        if (!_shift.IsStarted)
        {
            _shift.Start(now);
            _lastShiftSecs = -1;
            _log.LogInfo("taxi shift started");
            // Checkpoint bank: a shift start is the moment a player reads as
            // progress, so the calendar never rewinds past it when the process
            // dies before the next throttled write. The shift itself is banked
            // too: an empty shift must still idle out across a restart.
            SaveClock();
            SaveShift();
        }

        // Built once and reused like the delivery board: the committed radius
        // survives leaving the page, and every visit opens on a fresh
        // proposal (the Unity screen shows a newly generated job per visit).
        _proposal ??= new TaxiJobProposalViewModel(
            _proposals, _addressMap, () => AddressBook.Home(_addressMap, _story.Profile),
            () => _fleet.PassengerCapacity(), _log, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            _acceptJobAction ??= new ParameterizedCommand<TaxiJobState>(TaxiAccepted));
        _proposal.Open();
        SetScreen(_proposal);
        RefreshJob();
        _control?.Refresh();
    }

    // ACCEPT only commits the proposal: the customer is not aboard yet, so
    // the dirt gate and the breakdown roll wait at the pickup door (Unity's
    // TaxiJobLifecycleHandler). The overlay steps aside and the HUD pins the
    // pickup, counting the drive down to the arrival radius.
    private void TaxiAccepted(TaxiJobState? job)
    {
        if (job is null) return;
        _job = job;
        _job.Phase = JobPhase.ToPickup;
        _job.DistanceKm = 0.0;
        _job.Fare = 0.0;
        _job.Patrols = [];
        _job.FinedPatrolIds.Clear();
        _job.WarnedPatrolIds.Clear();
        _job.SpeedChallengeDeadline = null;
        _speedChallengeArmed = false;
        _pendingRequest = null;
        _meter.Reset();
        ShowPickupGuidance();
        Hide();
        _log.LogInfo($"job accepted: {job.Customer.Full} {job.PickupName} -> {job.DropoffName}");
        RefreshJob();
    }

    // Unity's pickup moment: the car is parked at the customer's door. The
    // dirt gate and the trip-start breakdown roll live here, not at ACCEPT,
    // because the customer only sees the car on arrival.
    private void ConfirmPickup()
    {
        if (_job.Phase != JobPhase.ToPickup) return;
        if (RefuseDirtyPickup(_job))
        {
            AbandonJob();
            if (_proposal is { } proposal)
            {
                ShowJobScreen(proposal);
            }
            return;
        }

        // Breakdown roll at the trip start (PORTPLAN 39, Unity
        // BeginTripSession): a triggered trip is cut before it pays — the job
        // never becomes active, the customer loses 1 familiarity, the
        // mechanic's tow fee is charged, and the breakdown page replaces the
        // proposal.
        if (RollTripStart(BreakdownKind.Taxi) is { } breakdown)
        {
            _friends.AddFamiliarity(_job.Customer.BaseName, -1);
            _log.LogInfo($"taxi job cut by breakdown: {_job.Customer.Full} " +
                $"{_job.PickupName} -> {_job.DropoffName}");
            AbandonJob();
            ShowBreakdown(breakdown);
            return;
        }

        _meter.Reset();
        _job.Phase = JobPhase.EnRoute;
        _job.DistanceKm = 0.0;
        _job.Fare = TaxiFare.ComputeBase(0.0, _fares);
        _log.LogInfo($"job pickup: {_job.PickupName}");
        // The drop-off task text goes up first: the player must read where to
        // drive before an ask may replace the HUD line. Unity rolls the special
        // request at the pickup, while the customer is still parked at the
        // door; the ask then rides the companion after the read-in delay
        // (ShowCustomerRequest on the poll). The panel is hidden for this leg.
        ShowDropoffGuidance();
        CustomerRequest? request = _requests?.Roll(
            _job.Customer, CurrentCarSpec(), _game.GameNow.Year,
            _garage.Current?.AskedCategories ?? []);
        if (request is not null)
        {
            _pendingRequest = request;
            _requestDueAt = _clock.UtcNow
                + TimeSpan.FromSeconds(_taxiRules.RequestDelaySeconds);
        }
        RefreshJob();
    }

    // The customer asks once the drop-off task text has had its read-in time.
    // The job only carries the request from this moment, and the forced speed
    // challenge starts its clock here: the player is not timed while reading
    // where to drive.
    private void ShowCustomerRequest(CustomerRequest request)
    {
        _job.Request = request;
        if (request.IsSpeedChallenge)
        {
            BeginSpeedChallenge();
        }

        _requestScreen = new CustomerRequestScreenViewModel(
            request, _job.Customer.Full, _requestTable.TimerSeconds,
            _clock, _beat, OnRequestFinished,
            request.IsSpeedChallenge ? _job.SpeedChallengeDeadline : null);
        SetScreen(_requestScreen);
        _hud?.ShowRequest(_requestScreen);
        _log.LogInfo($"customer request: {request.Kind} for {_job.Customer.Full}");
        RefreshJob();
    }

    // Unity's dirty-car refusal at the pickup door: the customer refuses the
    // ride, familiarity drops by one (the store banks itself), and the
    // proposal page shows the reason. A repeated ACCEPT on the same proposal
    // shows it again but never charges the customer twice
    // (Unity's _tooDirtyArrived).
    private bool RefuseDirtyPickup(TaxiJobState job)
    {
        if (_garage.Current is not { } car || !car.Filth.RefusesPickup)
        {
            _dirtyRefusedJob = null;
            return false;
        }

        string customer = job.Customer.BaseName;
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

    // The customer is at the drop-off door: settle, show the receipt and drop
    // the HUD guidance.
    private void ConfirmDropoff()
    {
        if (_job.Phase != JobPhase.EnRoute) return;
        _job.Phase = JobPhase.Complete;
        _pendingRequest = null;
        _hud?.SetJobGuidance(null);
        SettleReceipt();
        _job.Patrols = [];
        _job.FinedPatrolIds.Clear();
        _job.WarnedPatrolIds.Clear();
    }

    // A refused or broken-down pickup leaves no active job: the HUD line goes
    // away and the command guards close until the next ACCEPT.
    private void AbandonJob()
    {
        _job.Phase = JobPhase.None;
        _job.Patrols = [];
        _job.FinedPatrolIds.Clear();
        _job.WarnedPatrolIds.Clear();
        _job.SpeedChallengeDeadline = null;
        _speedChallengeArmed = false;
        _pendingRequest = null;
        _requestScreen = null;
        _hud?.ClearRequest();
        _hud?.SetJobGuidance(null);
        RefreshJob();
    }

    private void OnJobEntered()
    {
        if (PickupCommand.CanExecute(null))
        {
            PickupCommand.Execute(null);
        }
        else if (DropoffCommand.CanExecute(null))
        {
            DropoffCommand.Execute(null);
        }
    }

    // BACK on the request lands here exactly once: copy the outcome onto the
    // job, bank the quiz history for this car, clear the companion overlay and
    // resume the drop-off guidance. The police patrols arm from the speed flag
    // in the police slice; this host stays the single owner of job state.
    private void OnRequestFinished(CustomerRequestOutcome outcome)
    {
        _requestScreen = null;
        _hud?.ClearRequest();
        _job.TipEligible = outcome.TipEligible;
        _job.QuizAnsweredCorrectly = outcome.QuizAnsweredCorrectly;
        _job.SpeedChallengeAgreed = outcome.SpeedChallengeAgreed;

        if (outcome.SpeedChallengeAgreed)
        {
            // The forced challenge already armed at the pickup; this keeps
            // the old accepted-on-the-page path working without drawing a
            // second plan when BACK closes the forced page.
            ArmSpeedChallenge();
        }

        if (_job.Request?.Quiz is { } quiz && _garage.Current is { } car)
        {
            // Unity marks the category on answer AND on timeout (HandleAnswer
            // covers both), then saves; the at-count reset lives in Core.
            car.MarkAskedCategory(quiz.Category.Key, _requestTable.QuestionCategories.Count);
            SaveGarage();
        }

        ShowDropoffGuidance();
        Hide();
        _log.LogInfo($"customer request done: {_job.Request?.Kind} " +
            $"answered={outcome.Answered} tip={outcome.TipEligible} " +
            $"quiz={outcome.QuizAnsweredCorrectly} speed={outcome.SpeedChallengeAgreed}");
        RefreshJob();
    }

    // The quiz asks about the car in the garage, like Unity's playerCar: the
    // catalogue row behind the current key, never a copy taken at purchase.
    private CarSpec? CurrentCarSpec() =>
        _garage.Current is { } car ? _garage.SpecOf(car) : null;

    private double TrackLengthMeters() =>
        _trackLengthMeters ?? _addressMap.TrackLengthMeters;

    // The forced speed challenge starts with its page, after the drop-off task
    // text: the deadline and patrols are live even while the player is reading
    // the ask. The route is the same closed-lap distance the guidance and
    // patrol planner already use, so no routing service is needed.
    private void BeginSpeedChallenge()
    {
        double routeMeters = SpeedChallengeRouteMeters();
        CarSpec? timedCar = _fleet.XpCar()
            ?? _carIdentity?.FindIn(_cars)
            ?? CurrentCarSpec();
        TimeSpan? allowance = SpeedChallengeTimer.Estimate(
            routeMeters, timedCar?.TopSpeedKmh ?? 0, _taxiRules.SpeedChallenge);
        _job.SpeedChallengeDeadline = allowance is { } span
            ? _clock.UtcNow + span
            : null;
        _job.TipEligible = true;
        _job.SpeedChallengeAgreed = true;
        ArmSpeedChallenge();
        _log.LogInfo(allowance is { } estimated
            ? $"speed challenge deadline: {estimated.TotalSeconds:0}s " +
              $"({routeMeters / 1000.0:0.00} km, " +
              $"{timedCar?.TopSpeedKmh ?? 0} km/h top)"
            : "speed challenge has no deadline: route unavailable");
    }

    private double SpeedChallengeRouteMeters() =>
        _job.PickupAddress is { } pickup && _job.DropoffAddress is { } dropoff
            ? AddressBook.DistanceMeters(
                _addressMap, pickup, dropoff, TrackLengthMeters())
            : double.NaN;

    private void ArmSpeedChallenge()
    {
        if (_speedChallengeArmed || _police is not { } planner)
        {
            return;
        }

        // Unity spawns at the pickup for every job; this loop only needs the
        // plan when the gamble is on, and once per accepted job.
        _job.Patrols = planner.Plan(
            _job.PickupAddress, _job.DropoffAddress,
            TrackLengthMeters(), planner.Rules.PatrolsPerJob);
        _job.FinedPatrolIds.Clear();
        _job.WarnedPatrolIds.Clear();
        _speedChallengeArmed = true;
        _log.LogInfo(
            $"speed challenge armed: {_job.Patrols.Count} patrol(s), " +
            $"limit {planner.Rules.SpeedLimitKmh} km/h");
    }

    // The deadline only removes the ordinary tip; the fare and XP settle as
    // usual. The buffer in the estimate already covers the stop at the door,
    // so the HUD's TOO LATE line and this check agree.
    private void ApplySpeedChallengeDeadline()
    {
        if (!_job.SpeedChallengeAgreed
            || _job.SpeedChallengeDeadline is not { } deadline)
        {
            return;
        }

        if (_clock.UtcNow <= deadline)
        {
            return;
        }

        _job.TipEligible = false;
        _hud?.ShowNotice(
            Strings.Get("HudSpeedChallengeLate"),
            TimeSpan.FromSeconds(_taxiRules.NoticeSeconds));
        _log.LogInfo(
            $"speed challenge late: {(_clock.UtcNow - deadline).TotalSeconds:0}s " +
            $"past the deadline; tip dropped");
    }

    // Speed-challenge police check, once per live poll while the passenger is
    // aboard. A warning shows once per patrol on approach; a fine lands once
    // per patrol, clears the ordinary tip and adds the receipt deduction.
    private void CheckPolice(TelemetrySample sample)
    {
        if (_police is not { } planner)
        {
            return;
        }

        double length = TrackLengthMeters();
        double offset = sample.NormalizedPosition is { } position
            ? LapGeometry.OffsetMeters(position, length)
            : double.NaN;
        if (double.IsFinite(offset))
        {
            foreach (PolicePatrol patrol in _job.Patrols)
            {
                if (_job.FinedPatrolIds.Contains(patrol.Id)
                    || _job.WarnedPatrolIds.Contains(patrol.Id))
                {
                    continue;
                }

                double distance = LapGeometry.DistanceMeters(
                    offset, patrol.LapOffsetMeters, length);
                if (distance <= planner.Rules.WarningRadiusMeters)
                {
                    _job.WarnedPatrolIds.Add(patrol.Id);
                    _hud?.ShowNotice(
                        Strings.Format("HudPoliceWarning", patrol.SpeedLimitKmh),
                        TimeSpan.FromSeconds(_taxiRules.NoticeSeconds));
                }
            }
        }

        PolicePatrol? caught = SpeedChallengeFines.Check(
            _job.Patrols, sample.NormalizedPosition ?? double.NaN,
            length, sample.SpeedKmh, _job.FinedPatrolIds,
            _job.SpeedChallengeAgreed);
        if (caught is not { } fine)
        {
            return;
        }

        _job.FinedPatrolIds.Add(fine.Id);
        _job.SpeedChallengeFines++;
        // Unity's OR tip rule: the fine kills the ordinary ask tip but a
        // correct quiz tip rides its own flag through the settle.
        _job.TipEligible = false;
        _hud?.ShowNotice(
            Strings.Format("HudPoliceFine", _fares.FineAmount),
            TimeSpan.FromSeconds(_taxiRules.NoticeSeconds));
        _log.LogWarning(
            $"speed challenge fine: €{_fares.FineAmount:0} " +
            $"(patrol {fine.Id}, {sample.SpeedKmh:0} km/h in {fine.SpeedLimitKmh} zone)");
    }

    // The HUD line names the customer, not the leg: the templates carry the
    // door's street, number and town from the structured address, so passing
    // an address descriptor here would print it twice (Unity names the
    // passenger on the PICK UP / DROP OFF prompt too). The pickup names the
    // whole party (companion suffix included); the drop-off names the
    // customer alone, like Unity's base-name identity.
    private void ShowPickupGuidance() =>
        _hud?.SetJobGuidance(Guidance(
            _job.Customer.Full, _job.PickupAddress,
            "HudJobPickupDrive", "HudJobPickupArrived", "HudJobPickupAction"));

    private void ShowDropoffGuidance() =>
        _hud?.SetJobGuidance(Guidance(
            _job.Customer.BaseName, _job.DropoffAddress,
            "HudJobDropoffDrive", "HudJobDropoffArrived", "HudJobDropoffAction",
            _job.SpeedChallengeDeadline));

    // A proposal generated on a map that cannot supply two doors has no
    // structured address; the HUD then has nothing to route to and stays
    // unpinned rather than showing a wrong street.
    private HudJobGuidance? Guidance(
        string name, AddressLocation? target, string driveKey,
        string arrivedKey, string actionKey, DateTime? speedDeadline = null) =>
        target is { } door
            ? new HudJobGuidance(
                name, door, driveKey, arrivedKey, actionKey,
                _taxiRules.ArrivalRadiusMeters, speedDeadline)
            : null;

    // Reveals the panel on a job page (refusal, breakdown, receipt, shift
    // ended). Show() deliberately opens the menu instead, so the page must be
    // set first.
    private void ShowJobScreen(object screen)
    {
        RecordActivity();
        _introShown = true;
        SetScreen(screen);
        IsShown = true;
        _log.LogInfo("overlay shown (job)");
    }

    // Prices the live meter, banks it to the wallet, the driver save and the
    // shift, and shows the receipt. The odometer already banked the wear per
    // poll, so the settle must not add the distance a second time.
    private void SettleReceipt()
    {
        _job.Phase = JobPhase.Complete;
        ApplySpeedChallengeDeadline();
        int capacity = _fleet.PassengerCapacity();
        // The car that actually ran the ride. An owned car supplies its real
        // class + global rarity and contributes its km. An unowned free-roam
        // car reads 0 × 0 (no XP) and suppresses every career write below;
        // only its fare still pays.
        CarSpec? xpCar = _fleet.XpCar();
        bool earnsProgress = !_fleet.IsDrivingUnownedCar();
        int carClass = FallbackCarClass;
        int carRarity = FallbackCarRarity;
        if (earnsProgress)
        {
            if (xpCar is { Class: > 0 } knownClass)
            {
                carClass = knownClass.Class;
            }
            if (xpCar is not null)
            {
                carRarity = _rarity.GetRarity(xpCar.Key);
            }
        }
        else
        {
            // An unowned car has no class/rarity in the career ledger, so the
            // receipt's own XP formula reads 0 × 0.
            carClass = 0;
            carRarity = 0;
        }
        string carKey = xpCar?.Key ?? string.Empty;
        int tripKm = TripKm(_job.DistanceKm);
        // Unity keys familiarity off the base name (no companion suffix):
        // every completed ride is +1 for that resident, strangers included
        // (0 -> 1 on the first ride). The outcome rides the receipt so the
        // page renders it; transitions log, quiet rides do not.
        string baseName = _job.Customer.BaseName;
        FriendshipOutcome friendship = _friends.RegisterRide(baseName);
        // Unity banks the trip km into the driven brand before pricing, so
        // the current ride already counts toward its own sponsorship rate.
        string brandName = _fleet.SponsorBrand();
        double brandKm = _brands.RegisterRide(brandName, _job.DistanceKm);
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            _job.DistanceKm, capacity, carClass,
            carRarity, PlaceholderStreetKnowledgePct,
            tipEligible: _job.TipEligible || _job.QuizAnsweredCorrectly,
            fines: _job.SpeedChallengeFines, _fares, _wallet.Xp,
            new FriendshipResult(friendship.OwnerName, friendship.Level, friendship.TierIncreased),
            brandName, brandKm, carKey,
            // The starter bonus decays with the operating zone and the car
            // count (PORTPLAN 47, Unity ProvincialStarterBonus).
            zoneKm: _zone.Km, ownedCars: _garage.Cars.Count);
        if (friendship.TierIncreased)
        {
            _log.LogInfo($"friendship tier up: {friendship.OwnerName} -> {friendship.Level}");
        }
        _job.Fare = receipt.BaseTotal;
        _job.BrandBonus = receipt.BrandBonus;
        _job.StarterBonus = receipt.StarterBonus;
        _job.Tip = receipt.Tip;
        _job.Fine = receipt.FineDeduction;
        _wallet.AddReceipt(receipt);
        // An unowned car banks the fare but no career progress: no global car
        // km, country km, zone job or XP standings. The interest still
        // compounds — it belongs to the bank, not to the car.
        if (earnsProgress)
        {
            if (xpCar is not null && tripKm > 0)
            {
                _rarity.RecordKm(xpCar.Key, tripKm);
            }
            if (tripKm > 0)
            {
                _online.IncrementCountryKm(HomeCountry, tripKm);
            }
            // Zone tick (PORTPLAN 58, Unity expansionJobsCompleted++): every
            // settled taxi ride counts; the interest lands in the bank before
            // the expansion street joins (Unity orders it the same way —
            // counter, interest, street registration at the receipt CONTINUE).
            // The lifetime total (Unity jobsCompleted) rides the same hook and
            // gates the job-opened businesses.
            _zone.RegisterJob();
            _jobs.Register();
        }
        ApplyBankInterest();
        if (earnsProgress)
        {
            TryExpandZone();
            GrowPois();
            RefreshStandings();
        }
        else
        {
            // The settle still banks cash and the shift; only the XP push is
            // skipped. The last known board rides along unchanged.
            SaveDriver(_rivals.LastResult);
        }
        _shift.RecordCompleted(_job, _clock.UtcNow);
        // The dropoff restamps the idle clock; bank it so a process death
        // right after the receipt keeps the shift's 30 minutes honest.
        SaveShift();
        // Dirt tick (PORTPLAN 33, Unity TaxiJobLifecycleHandler's dropoff
        // block): every completed ride is +1 on the car driven now.
        _fleet.AddDirt();
        // Checkpoint bank: a paid job is progress the player can see, so the
        // moment goes to disk now instead of waiting for the throttle. The
        // garage rides along so the dirt tick is not lost with the process.
        SaveClock();
        SaveGarage();
        _log.LogInfo($"job dropoff: {_job.PickupName} -> {_job.DropoffName} " +
                  $"dist={receipt.DistanceKm:0.00}km car={(carKey.Length > 0 ? carKey : "unowned")} " +
                  $"class={carClass} rarity={carRarity} xp={receipt.XpEarned:0.00} " +
                  $"brand={receipt.BrandName} bonus={receipt.BrandBonus:0.00} " +
                  $"starter={receipt.StarterBonus:0.00} " +
                  $"wallet={_wallet.Cash:0.00} shift={_shift.JobsDone} jobs / {_shift.Earned:0.00}");
        ShowJobScreen(new TaxiReceiptScreenViewModel(
            receipt, _job.Customer.Full, _wallet, _shift, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen())), _rivals, _rarity));
        RefreshJob();
    }

    // One driver save the whole VM shares (PORTPLAN 51 interim): every state
    // owner contributes its slice through the saver, so a save from any path
    // can never clobber another owner with stale numbers. Standings ride along
    // from the caller that just refreshed them; null keeps the last known board
    // snapshot (OfflineRivals seeds it from the driver save at boot).
    private void SaveDriver(RivalsResult? standings) => _saver.SaveDriver(standings);

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
        _rivals.PushXp(_wallet.Xp, PlayerName());
        SaveDriver(standings);
        LogStandingsMove(standings);
        return standings;
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
        ShowJobScreen(new BreakdownScreenViewModel(
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
        // Interim length: the placeholder map's mean street, so the zone km
        // (and the starter bonus) keeps decaying until real street metadata
        // arrives (PORTPLAN 58).
        if (_zone.TryExpand(streetId, AddressBook.AverageStreetLengthKm(_addressMap)))
        {
            _log.LogInfo($"[Zone] expanded: {streetId} joins " +
                $"(zone {_zone.StreetIds.Count} streets, {_zone.Km:0.0} km)");
            _online.PushZoneKm(_zone.Km, PlayerName());
        }
    }

    // The business/store side of the marker network (PORTPLAN 24, Unity's
    // CarWash/Bank/Insurance/Dealer/Mechanic generators plus the hub store
    // backfill): every unlocked place opens on a free house. Cold path only
    // (boot + settle + hub close), never the 10 Hz poll. The directory only
    // grows, so a door the player already knows never moves.
    private void GrowPois()
    {
        if (!OnboardingDone)
        {
            return;
        }

        IReadOnlyList<PointOfInterest> grown = _poiGrowth.Spawn(
            _zone.StreetIds.Count, _jobs.Completed, _poiDirectory,
            _career.OwnsCategory);
        if (grown.Count == 0)
        {
            return;
        }

        bool banked = false;
        bool boardChanged = false;
        foreach (PointOfInterest poi in grown)
        {
            if (!_poiDirectory.Add(poi))
            {
                continue;
            }

            banked = true;
            _log.LogInfo($"[Poi] opened {poi.Name} at " +
                AddressFormatter.Format(poi.Address));
            // A placed grocery/coffee store is also a delivery target: the
            // shop interior and the board row share the same POI identity,
            // so the town and the board gain the store together.
            if (PlacedStore.Row(poi, _storeCatalog) is { } store
                && _city.AddStore(store))
            {
                _deliveryCatalog = _deliveryCatalog.WithStore(store);
                boardChanged = true;
            }
        }

        if (banked)
        {
            _poiStore.Save(_poiDirectory.All);
        }

        if (boardChanged)
        {
            _deliveryScreen?.Refresh(_deliveryCatalog);
        }
    }

    // One shift ends at both timeout sites (the poll and the START tap);
    // bank the record once and push the best to the world board. Pushing on
    // every end (not only an improvement) also syncs a best restored from a
    // local save whose earlier push never reached Firestore. The shift-ended
    // page opens from here, so the timeout is never silent (Unity chains the
    // same page before the map).
    private void EndShift(TimeSpan idle)
    {
        ShiftSummary summary = _shift.Summary();
        _log.LogInfo($"shift ended (timeout after {idle.TotalMinutes:0}m idle): " +
            $"{summary.Jobs} jobs / {summary.Earnings:0.00}");
        _lastShiftSecs = -1;
        // The closed state goes to disk before the record push: a shift that
        // timed out between sessions is already over and must never reopen.
        SaveShift();
        // The record is stamped when the idle window closed, not when the
        // process noticed: an expiry discovered at the next launch can be
        // days later, and the world board breaks earnings ties by this stamp.
        DateTime endedAt = _shift.LastJobEnd + TimeSpan.FromMinutes(TaxiShift.TimeoutMinutes);
        _shifts.Complete(summary.Jobs, summary.Earnings, endedAt.Ticks);
        // Refresh before the page reads it, exactly like the receipt's XP
        // block: LastResult then answers with the fresh rank or the restored
        // snapshot, and the page follows the board's event if it is still
        // loading.
        if (_shiftBoard.Refresh(_shifts.BestEarnings, _shifts.BestFirstEarnedTicks)
            is { } standings)
        {
            _shifts.RestoreStandings(standings.CurrentRank, standings.TotalCompetitors);
        }
        if (_shifts.BestEarnings > 0.0)
        {
            _online.PushShiftEarnings(
                _shifts.BestEarnings, _shifts.BestFirstEarnedTicks, PlayerName());
        }
        // The lifetime counters ride the driver save, so bank even a shift
        // that earned nothing.
        SaveDriver(_rivals.LastResult);
        RefreshJob();
        ShowJobScreen(new ShiftEndedScreenViewModel(
            summary, _shifts, _shiftBoard, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen()))));
    }

    // Unity's whole-km trip unit (Mathf.RoundToInt with a 1 km floor): the
    // country/car aggregates never receive a sub-kilometre or negative sample.
    private static int TripKm(double distanceKm) =>
        distanceKm > 0.0 && double.IsFinite(distanceKm)
            ? Math.Max(1, (int)Math.Round(distanceKm))
            : 0;

    private string PlayerName() => _story.Profile?.DisplayName ?? string.Empty;

    private string HomeCountry => _story.Profile?.Country ?? _addressMap.Country;

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

    // The wallet page (PORTPLAN 06/43/48/60): a fresh page per visit, so the
    // cash, XP and bank figures always open on the live wallet and the
    // leaderboard on the latest board result. Read-only — opening never
    // writes; BACK returns to the menu it was opened from.
    private void OpenWallet() =>
        SetScreen(new WalletScreenViewModel(
            _wallet, _rivals,
            new RelayCommand(() => SetScreen(CreateMenuScreen())),
            _beat));

    // The POI router's business families: the root supplies the store handler,
    // the host appends one registration per interior it builds. A new business
    // joins here, never in the entry path (PORTPLAN item 24, OCP).
    private List<IPoiArrivalHandler> BuildPoiHandlers(
        IReadOnlyList<IPoiArrivalHandler>? supplied)
    {
        var handlers = new List<IPoiArrivalHandler>();
        if (supplied is not null)
        {
            handlers.AddRange(supplied);
        }

        handlers.Add(new DelegatePoiHandler(
            PoiTypes.Bank, (_, back, changed) => BuildBankScreen(back, changed)));
        handlers.Add(new DelegatePoiHandler(
            PoiTypes.Dealer, (_, back, _) => BuildDealerScreen(back)));
        handlers.Add(new DelegatePoiHandler(
            PoiTypes.CarWash, (_, back, changed) => BuildCarWashScreen(back, changed)));
        handlers.Add(new DelegatePoiHandler(
            PoiTypes.Mechanic, (_, back, changed) => BuildMechanicScreen(back, changed)));
        handlers.Add(new DelegatePoiHandler(
            PoiTypes.Insurance, (_, back, changed) => BuildInsuranceScreen(back, changed)));
        return handlers;
    }

    // The garage page (PORTPLAN 30, Unity GarageScreen): a fresh page per visit,
    // so a car bought since the last visit is on it. A switch banks the garage
    // through the shared save (Unity SaveManager.Save) — the page never writes.
    // BACK returns to the home page it is opened from (Unity's You menu).
    private void OpenGarage()
    {
        SetScreen(new GarageScreenViewModel(
            _garage,
            new RelayCommand(() => SetScreen(CreateHomeScreen())),
            key =>
            {
                SaveGarage();
                _log.LogInfo($"[Garage] now driving {key} " +
                    $"({_garage.Cars.Count} cars owned)");
            },
            _beat));
    }

    // The bank interior (PORTPLAN 48, Unity BankScript.Show): the VM stamps
    // hasEnteredBank (VisitBank todo once item 51 lands) and opens the saldo
    // live. The page never writes; a withdraw routes through the host save so
    // the saldo never forks.
    private BankScreenViewModel BuildBankScreen(ICommand back, Action onChanged) =>
        new BankScreenViewModel(
            _wallet, back,
            moved =>
            {
                onChanged();
                _log.LogInfo($"[Bank] withdrew {moved:0.00} CR " +
                    $"(cash {_wallet.Cash:0.00}, bank {_wallet.BankBalance:0.00})");
            },
            _beat);

    // The dealer's weekly floor (PORTPLAN 29, Unity CarDealerScreen). The week
    // rides in from the display week, so the floor turns over with the calendar
    // and a re-opened page inside one week shows the same cars (DealerStock).
    // The bought-car choice is the host's page (OnCarBought).
    private DealerScreenViewModel BuildDealerScreen(ICommand back)
    {
        return new DealerScreenViewModel(
            _cars, _garage, _wallet, _displayWeek, back, OnCarBought, _beat);
    }

    // The car wash (PORTPLAN 33, Unity CarWashScreen): a fresh page per visit,
    // so the car driven now and its reading are live. A wash spends the flat
    // price and clears the car's filth; both are banked here (the driver save
    // through onChanged for the cash, garage.json for the reading) and logged —
    // the page never writes (SRP).
    private CarWashScreenViewModel BuildCarWashScreen(ICommand back, Action onChanged) =>
        new CarWashScreenViewModel(
            _garage, _wallet, _beat, back,
            (removed, paid) =>
            {
                onChanged();
                SaveGarage();
                _log.LogInfo($"[CarWash] removed {removed} filth for {paid:0.00} CR " +
                    $"(cash {_wallet.Cash:0.00}, {_garage.CurrentKey ?? "no car"})");
            });

    // The car mechanic (PORTPLAN items 31/32, Unity CarMechanicScreen): a fresh
    // page per visit, so the car driven now, its wear deck and its engine are
    // live. Parts are replaced, the engine repaired or swapped; every action
    // moves cash and mutates the car's condition, so both are banked here (the
    // driver save through onChanged for the cash, garage.json for the
    // condition) and logged — the page never writes (SRP).
    private MechanicScreenViewModel BuildMechanicScreen(ICommand back, Action onChanged) =>
        new MechanicScreenViewModel(
            _garage, _wallet, _game, _beat, back,
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
                onChanged();
                SaveGarage();
                _log.LogInfo($"[Mechanic] {repair.Kind} cost {repair.Cost:0.00} CR " +
                    $"(parts {repair.PartsReplaced}, engine replaced {repair.EngineReplaced}, " +
                    $"cash {_wallet.Cash:0.00}, {_garage.CurrentKey ?? "no car"})");
            });

    // Insurance broker (PORTPLAN 34, Unity InsuranceScreen): a fresh page per
    // visit, so the cover flags and the escrow are live. A toggle and a
    // collection are banked here (the garage for the flag, the driver through
    // onChanged for the cash + escrow) and logged — the page never writes (SRP).
    private InsuranceScreenViewModel BuildInsuranceScreen(ICommand back, Action onChanged) =>
        new InsuranceScreenViewModel(
            _garage, _wallet, _insuranceRules, _wallet.Rating.Overall, _beat, back,
            (key, insured) =>
            {
                SaveGarage();
                _log.LogInfo($"[Insurance] {key} {(insured ? "insured" : "uninsured")} " +
                    $"(cash {_wallet.Cash:0.00})");
            },
            moved =>
            {
                onChanged();
                _log.LogInfo($"[Insurance] collected {moved:0.00} CR (cash {_wallet.Cash:0.00})");
            });

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
    public void SaveGarage() => _saver.SaveGarage();

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
        _fleet.BankJobDistance(PlaceholderDistanceKm);
        // An unowned free-roam car pays the run's money but earns no career
        // progress, exactly like the taxi settle: no XP, country km, zone job
        // or XP standings.
        bool earnsProgress = !_fleet.IsDrivingUnownedCar();
        DeliveryReceipt receipt = DeliveryReceiptBuilder.Build(
            PlaceholderDistanceKm, run.PaidForGoods, run.ReceivedForGoods,
            _deliveryFares, PlaceholderStreetKnowledgePct, _wallet.Xp,
            friendship, credPEarned, _career.CredP, awardsXp: earnsProgress);
        _wallet.AddDelivery(receipt);
        // Delivery's km feed: the owned car contributes to the global rarity
        // board and the country aggregate, exactly like the taxi settle. The
        // delivery XP itself stays class/rarity-free (Unity parity).
        CarSpec? deliveryCar = _fleet.XpCar();
        int deliveryKm = TripKm(PlaceholderDistanceKm);
        if (earnsProgress)
        {
            if (deliveryCar is not null && deliveryKm > 0)
            {
                _rarity.RecordKm(deliveryCar.Key, deliveryKm);
            }
            if (deliveryKm > 0)
            {
                _online.IncrementCountryKm(HomeCountry, deliveryKm);
            }
            // Zone tick, delivery twin (Unity DeliveryJobHandler's settle): the
            // counters, interest and expansion flow exactly like the taxi hook.
            _zone.RegisterJob();
            _jobs.Register();
        }
        ApplyBankInterest();
        if (earnsProgress)
        {
            TryExpandZone();
            GrowPois();
            RefreshStandings();
        }
        else
        {
            SaveDriver(_rivals.LastResult);
        }
        // Dirt tick, delivery twin (PORTPLAN 33, Unity DeliveryJobHandler's
        // store arrival): a completed run is +1 on the car driven now.
        _fleet.AddDirt();
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

    private void RefreshJob()
    {
        JobLine = _job.Phase switch
        {
            JobPhase.None => Strings.Get("NoJob"),
            JobPhase.Proposed => Strings.Format("JobProposed", _job.PickupName, _job.DropoffName),
            JobPhase.ToPickup => Strings.Format("JobToPickup", _job.PickupName, _job.DropoffName),
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
