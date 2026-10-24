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
public sealed class MainViewModel
    : ViewModelBase, IVoiceHost, IStoryHost, IJobSettlementHost, ITaxiJobHost,
      IDeliveryHost, IPoiWorldHost, INavigationHost, IDayCycleHost, IDisposable
{
    private readonly ITelemetrySource _telemetry;
    private readonly IClock _clock;
    private readonly VoiceBeat _beat;
    private readonly GameClock _game;
    // The single writer of every save file: the stores, the snapshot assembly
    // and the write throttles live there, never in this class (SRP).
    private readonly GameStateSaver _saver;
    // Prices and banks one finished job: the taxi receipt and the delivery run
    // (Unity's effect order lives there, not here).
    private readonly JobSettlement _settlement;
    // The panel, the page it shows and the activity stamp.
    private readonly OverlayShell _shell;
    // The five shop interiors: assembly only, the banking callbacks included.
    private readonly ShopInteriors _shops;
    // The taxi job from proposal to receipt: owns the job, the request and the
    // patrols; the shell's half arrives through ITaxiJobHost.
    private readonly TaxiJobFlow _taxi;
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
    private readonly IFriendshipStore _friends;
    // XP world standings backend (PORTPLAN 60, Unity RivalsService): the
    // receipt's rivals block reads through it on every settle.
    private readonly IRivalsService _rivals;
    // The dynamic car-rarity board behind the XP multiplier (PORTPLAN 35,
    // Unity CarRarityService): reads are cached, the km write is fire-and-forget.
    // Zone, best-shift and country pushes (Unity FirestoreService + CountryKm).
    private readonly IPlayerTelemetry _online;
    // Lifetime km per driven brand behind the receipt's sponsorship rate
    // (PORTPLAN 35, Unity PlayerData.brandDistanceDriven).
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
    private readonly IVoiceListener _voice;
    private readonly ICarBrandSource _carBrands;
    // The delivery distance-charge table (no forfait/tip/fine): the dropped
    // goods are paid by distance alone (Data/delivery_fares.json).
    private readonly DeliveryFareTable _deliveryFares;
    // CredP balance + owned hubs, shared by the board and the hub page.
    // In-memory until the persistent save lands (PORTPLAN 51).
    private readonly DeliveryCareer _career;
    // The delivery board. A placed grocery/coffee store appends its row at
    // runtime, so the live catalogue is a holder the board page, the run
    // planner and the settle all read.
    private readonly DeliveryBoard _deliveryBoard;
    // The POI world: the business/store/hub doors and the arrival handlers.
    private readonly PoiWorld _poiWorld;
    // The delivery run from DELIVER to receipt: owns the live run and the board
    // page; the panel, the HUD line and the settle arrive through IDeliveryHost.
    private readonly DeliveryFlow _delivery;
    // The proposal draw behind the taxi page: two map addresses, the
    // persisted resident at each door and the home/residence flip
    // (Data/name_pools.json, Germany pools + Unity suffix list, PORTPLAN
    // 11/13/45/61).
    // The customer request draw (PORTPLAN item 11): suffix/passive, speed
    // challenge and the car-stat quiz, with its own seeded stream.
    // Timer length, tier chances and quiz categories; the host also uses the
    // category count for the per-car asked-history reset.
    // Speed-challenge patrols (Phase C): one seeded plan per accepted job.
    // The last live track length, so a patrol planned after the request page
    // sits on the same lap offsets the HUD resolves.

    // The placeholder free-roam map: the HUD, the onboarding home and the
    // taxi draw all read the same streets and lap geometry (PORTPLAN Step 1).
    private readonly AddressMap _addressMap;
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
    // The seeded roll behind the streak's random skill gains (PORTPLAN 06/47,
    // Unity ApplyRandomSkillDelta): the root owns the seed, so a test can pin
    // the skills a resumed streak lands on.
    private readonly Random _rng;
    // The 200 px companion: the poll pushes it one snapshot so
    // the HUD never opens its own timer or shared-memory handle.
    private readonly ITelemetryHud? _hud;
    // The page graph: the control page, the menu and every tile behind it. It
    // owns the page caches, so this class keeps only panel policy.
    private readonly Navigation _nav;
    // The readout formatter: the painted strings and their change caches. The
    // poll hands it the sample and copies back what changed, so no formatting
    // lives on the hot path (S5).
    private readonly TelemetryReadouts _readouts;
    // The session's calendar: the day flip, the display week and the idle shift
    // timeout with its ended page. The pages read the week from it.
    private readonly DayCycle _day;

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

    // A job is live from ACCEPT (the drive to the pickup) until the drop-off
    // settles, or from DELIVER until the last store is served. The idle-shift
    // rule and the overlay call both read this one definition, so a new leg
    // cannot mean two different things.
    private bool JobInProgress => _taxi.InProgress || _delivery.InProgress;
    // The passenger-leg distance. Lives outside the job so a refused pickup or
    // a breakdown can discard the ride without leaving a half-run meter.
    private readonly TaxiDriveMeter _meter;

    public MainViewModel(
        ITelemetrySource telemetry, IClock clock, GameClock game,
        TaxiShift shift, ISettingsStore store,
        ILog log, FareTable fares,
        IOnboardingStore onboarding, IContentSource content,
        IProfileStore profiles, IPlayerNameRegistry playerNames,
        AddressMap addressMap, string? detectedPlayerName,
        IVoiceListener voice, ICarBrandSource carBrands,
        DeliveryBoard deliveryBoard, DeliveryCareer career,
        DeliveryFareTable deliveryFares,
        CityEconomy city,
        VoiceBeat beat, DriverWallet wallet,
        DailyStreak streak, OperatingZone zone, IFriendshipStore friendships, IReadOnlyList<CarSpec> cars,
        IRivalsService rivals,
        IPlayerTelemetry playerTelemetry,
        Hunger hunger,
        Garage garage, InsuranceRules insuranceRules,
        Random rng,
        PoiGenerator pois,
        PoiDirectory poiDirectory, IPoiDirectoryStore poiStore,
        IPreferencesStore preferencesStore, PlayerPreferences preferences, ICultureService culture,
        Trunk trunk,
        TaxiRules taxiRules,
        GameStateSaver saver, FleetService fleet, JobSettlement settlement,
        OverlayShell shell, TaxiDriveMeter meter, ShopInteriors shops,
        TaxiJobFlow taxi, DeliveryFlow delivery, PoiWorld poiWorld,
        PoiArrivalRouter poiArrivals, Navigation navigation,
        TelemetryReadouts readouts, DayCycle day,
        ITelemetryHud? hud = null,
        ISaveDataResetter? saveResets = null,
        IAppRestarter? restarter = null)
    {
        _telemetry = telemetry;
        _clock = clock;
        _beat = beat;
        _game = game;
        _shift = shift;
        _store = store;
        _saveResets = saveResets;
        _restarter = restarter;
        _log = log;
        _fares = fares;
        _addressMap = addressMap;
        _taxiRules = taxiRules;
        _content = content;
        _voice = voice;
        _carBrands = carBrands;
        _deliveryBoard = deliveryBoard;
        _deliveryFares = deliveryFares;
        _career = career;
        _city = city;
        _hunger = hunger;
        _garage = garage;
        _insuranceRules = insuranceRules;
        _rng = rng;
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
        _friends = friendships;
        _rivals = rivals;
        _online = playerTelemetry;
        _cars = cars;
        // The persistence collaborator and the fleet are built by the root: the
        // shell may not own a stateful service (AGENTS.md), and both are handed
        // in so the settles and the poll share one instance.
        _saver = saver;
        _settlement = settlement;
        _fleet = fleet;
        _shell = shell;
        _shops = shops;
        _taxi = taxi;
        _delivery = delivery;
        _poiWorld = poiWorld;
        // The readout formatter and the session calendar are built by the root
        // too; neither reaches back, so the day cycle is the fourth late edge
        // (it shows the shift-ended page the poll's timeout opens).
        _readouts = readouts;
        _day = day;
        _day.Attach(this);
        // The page graph is built by the root; the navigator only reaches back
        // for the entries whose domain is not a page (shift close, readouts,
        // settings, haul), so the attach closes before any page command can
        // fire — the same late edge the three flows use.
        _nav = navigation;
        _nav.Attach(this);
        // All three collaborators are built before the shell they report to;
        // these are the late edges, and they close here before anything can
        // fire a command or enter a door.
        _taxi.Attach(this);
        _delivery.Attach(this);
        _poiWorld.Attach(this);
        // The view binds IsShown/CurrentScreen on this VM, but the shell owns
        // them: forward its notifications, and re-derive the page-shape flags.
        _shell.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        _shell.ScreenChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsOnboarding));
            OnPropertyChanged(nameof(JobsVisible));
        };
        // The meter is shared with the settle: the poll feeds it and the
        // receipt reads (and resets) the same accumulator.
        _meter = meter;
        // The story collaborator owns the persisted walk + profile + errand POI;
        // it is built before the garage seed below reads its progress. The POI
        // router arrives from the root, which built it from PoiWorld's handlers.
        _story = new OnboardingFlow(
            onboarding, content, profiles, playerNames, addressMap, detectedPlayerName,
            pois, poiDirectory, poiStore, poiArrivals, trunk,
            career, deliveryBoard.Catalog.StartCredP,
            preferencesStore, preferences,
            culture, beat, hud, garage, () => _day.DisplayWeek, SaveGarage,
            () => _carBrand, log, this);
        _carBrands.BrandChanged += (_, e) => OnCarBrand(e.Brand, e.Identity);
        // Business catch-up at boot (Unity MapScreen.Show's generator checks):
        // a save whose street/job counters already crossed a threshold opens
        // the missing doors now. A fresh career is still in onboarding, so the
        // gate keeps the business list empty until the introduction ends.
        _poiWorld.Grow();
        // Grandma's car is the first OwnedCar (PORTPLAN Career Start item 3 +
        // item 30): a save whose onboarding already ran but whose garage is
        // still empty — every save written before this slice — seeds the car
        // it picked instead of opening on an empty garage. Unity creates the
        // starter OwnedCar at the pick itself, so this is the same state one
        // session later; the key resolves through the catalogue for specs.
        if (OnboardingDone && _garage.Cars.Count == 0 && _story.Progress.GrandmaCarId is { Length: > 0 } starter)
        {
            _garage.Add(starter, _day.DisplayWeek);
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
            // Unity's streak tail (Source MapScreen.GrantDailyBonusIfDue): day
            // N hands N random skill points out, silently, before the save.
            for (int i = 0; i < days; i++)
            {
                _wallet.AddRandomSkill(_rng, 1.0);
            }
            SaveDriver(_rivals.LastResult);
            _log.LogInfo($"[Streak] consecutive-day bonus granted: {bonus} CR (day {days}, " +
                $"{days} random skill point(s))");
        }
        _shell.RecordActivity(_clock.UtcNow);
        // The router's warn-free reads go through IVoiceHost (see the explicit
        // implementations below), so the host surface stays small.
        _voiceRouter = new VoiceRouter(this, clock, log, hud);
        _voice.Heard += (_, e) => OnVoiceHeard(e.Text, e.IsFinal);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Poll();
        _lastTick = _clock.UtcNow;

        ToggleShowCommand = new RelayCommand(() =>
        {
            if (IsShown) Hide(); else Show();
        });

        RefreshJob();
        // The first paint of the session: no physics has been read yet, so the
        // waiting line is the honest one; the gear and detail lines keep their
        // unknown placeholders until the first tick lands.
        Apply(_readouts.PaintWaiting(gameRunning: false));
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
        _nav.ShowScreen(OnboardingDone ? _nav.MenuPage() : _story.StartPage());

        // The VM's default: hidden until called when a mic is present, visible
        // otherwise so the shell stays usable (and testable). A save that is
        // still in the post-birthday tour starts hidden even without a mic:
        // the tour is driven on the HUD, and the panel only opens once it is
        // finished. The app root calls Show() at launch for the other starts,
        // so a real start opens on the intro either way.
        _shell.SetShown(!voice.IsAvailable && !IntroTourInProgress);
        // A visible start has already shown the intro; a hidden start keeps
        // the intro on the first wake (see Show).
        _introShown = IsShown;
    }

    // ---- bindable state -------------------------------------------------

    // Panel + page live in the shell; these forward so the view keeps binding
    // the same names. The shell raises the change notifications.
    public bool IsShown => _shell.IsShown;

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

    public ICommand PickupCommand => _taxi.PickupCommand;
    public ICommand DropoffCommand => _taxi.DropoffCommand;
    public ICommand ToggleShowCommand { get; }

    /// <summary>The control page's START/CONTINUE TAXI entry (the navigator builds it).</summary>
    public ICommand TaxiShiftCommand => _nav.TaxiShiftCommand;

    // ---- onboarding navigation -----------------------------------------------

    /// <summary>Active screen: an onboarding page, a jobs page or null.</summary>
    public object? CurrentScreen => _shell.CurrentScreen;

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
    /// True while the post-birthday guided tour is still running. The shell
    /// starts hidden and does not pop the panel open until the tour ends;
    /// the companion HUD carries the driving instructions instead.
    /// </summary>
    public bool IntroTourInProgress => _story.IntroTourInProgress;

    /// <summary>
    /// True for the whole introduction: the walk pages, the birthday, the
    /// guided tour and its closing "how to call the panel" page. The shell
    /// suppresses the idle countdown for all of it, because hiding a walk page
    /// would skip the introduction instead of parking the overlay.
    /// </summary>
    public bool IntroInProgress => _story.IntroInProgress;

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
    void IStoryHost.ShowScreen(object? screen) => _nav.ShowScreen(screen);
    void IStoryHost.Present(object screen) => Present(screen);
    void IStoryHost.Hide() => Hide();
    object IStoryHost.ControlScreen() => _nav.ControlPage();
    object IStoryHost.MenuScreen() => _nav.MenuPage();
    void IStoryHost.ShowMenu()
    {
        _nav.ShowMenuPage();
        Show();
    }
    void IStoryHost.SaveDriver() => SaveDriver(_rivals.LastResult);

    // The host slice the settle sees: it prices and banks the job, the shell
    // owns the page, the saves and the career growth the receipt triggers.
    void IJobSettlementHost.ShowReceipt(object screen) => ShowJobScreen(screen);
    void IJobSettlementHost.RefreshJob() => RefreshJob();
    void IJobSettlementHost.SaveDriver(RivalsResult? standings) => SaveDriver(standings);
    void IJobSettlementHost.SaveShift() => SaveShift();
    void IJobSettlementHost.SaveClock() => SaveClock();
    void IJobSettlementHost.SaveGarage() => SaveGarage();
    void IJobSettlementHost.OnProgressBanked()
    {
        TryExpandZone();
        _poiWorld.Grow();
    }
    RivalsResult? IJobSettlementHost.RefreshStandings() => RefreshStandings();
    void IJobSettlementHost.GoToControlPage() => _nav.ShowControl();
    void IJobSettlementHost.RefreshDeliveryBoard() => _delivery.RefreshBoard();
    void IJobSettlementHost.ClearDeliveryRun() => _delivery.Stop();
    void IJobSettlementHost.GoToDeliveryBoard() => _nav.ShowDeliveryBoard();

    // The host slice the taxi flow sees: the flow owns the job and the ask; the
    // shell owns the panel and the settle that prices the ride.
    void ITaxiJobHost.ShowPage(object screen) => _nav.ShowScreen(screen);
    void ITaxiJobHost.RevealPage(object screen) => ShowJobScreen(screen);
    void ITaxiJobHost.HidePanel() => Hide();
    void ITaxiJobHost.RefreshJob() => RefreshJob();
    void ITaxiJobHost.RefreshControlPage() => _nav.RefreshControl();
    object ITaxiJobHost.ControlPage() => _nav.ControlPage();
    AddressLocation? ITaxiJobHost.HomeAddress() => AddressBook.Home(_addressMap, _story.Profile);
    void ITaxiJobHost.SettleRide(TaxiJobState job) =>
        _settlement.SettleTaxiRide(job, HomeCountry, _carBrand, this);

    // The host slice the delivery run sees: the flow owns the run and the board;
    // the shell owns the panel, the companion's line, the hub page and the settle.
    void IDeliveryHost.ShowPage(object screen) => _nav.ShowScreen(screen);
    void IDeliveryHost.RevealPage(object screen) => ShowJobScreen(screen);
    void IDeliveryHost.HidePanel() => Hide();
    object IDeliveryHost.ControlPage() => _nav.ControlPage();
    void IDeliveryHost.OpenHub() => _nav.ShowHub();
    void IDeliveryHost.ClearGuidance() => _hud?.SetJobGuidance(null);
    bool IDeliveryHost.PinStop(DeliveryStop leg)
    {
        if (DeliveryGuidance(leg) is not { } guidance)
        {
            return false;
        }
        _hud?.SetJobGuidance(guidance);
        return true;
    }
    void IDeliveryHost.Settle(DeliveryRun run) =>
        _settlement.SettleDeliveryRun(run, HomeCountry, this);

    // The host slice the POI world sees: the world owns the doors and the
    // board growth; the navigator owns the board page and the dealer's follow-up.
    void IPoiWorldHost.RefreshBoard() => _delivery.RefreshBoard();
    void IPoiWorldHost.CarBought(CarSpec car) => _nav.CarBought(car);

    // The host slice the navigator sees: the entries whose work is not a page.
    // A shift that already timed out is the day cycle's to close (it owns the
    // record and its summary page), and the settings page is the story flow's.
    void INavigationHost.ShiftTimedOut(TimeSpan idle) => _day.EndShift(idle);
    void INavigationHost.ReadoutsChanged() => RefreshJob();
    void INavigationHost.ShowSettings() => OpenSettings();
    void INavigationHost.SettleHaul() => OnCarHaul();

    // The host slice the day cycle sees: the week roll repaints the cached
    // pages, the countdown forces a readout, and the shift-end page arrives
    // here to be revealed (the panel policy stays in this class).
    void IDayCycleHost.DisplayWeekChanged(int week) => _nav.DisplayWeekChanged(week);
    void IDayCycleHost.ReadoutsChanged() => RefreshJob();
    void IDayCycleHost.ShowShiftEnded(object page) => ShowJobScreen(page);
    void IDayCycleHost.GoToControlPage() => _nav.ShowControl();

    // Reveal the panel on the requested POI screen. Show() deliberately
    // resets a hidden start to the control page; entering a shop must land
    // on the shop instead, so set the screen before flipping visibility.
    private void Present(object screen)
    {
        RecordActivity();
        _introShown = true;
        _shell.Reveal(screen);
        _log.LogInfo("overlay shown (POI)");
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
        int carClass = car is { Class: > 0 } ? car.Class : JobSettlement.FallbackCarClass;
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
        _nav.ShowScreen(new CarHaulReceiptScreenViewModel(
            receipt, _wallet, _beat, _nav.ControlCommand));
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
        _saver.SaveRoadTest();
    }

    public WindowBounds? LoadPlacement() => _store.Load();
    public void SavePlacement(WindowBounds bounds) => _store.Save(bounds);

    /// <summary>
    /// Persists the game moment + display week and restamps the poll throttle,
    /// so every bank path (day flip, checkpoint, interval, window close) keeps
    /// the interval honest. Never throws (the store swallows IO).
    /// </summary>
    public void SaveClock() => _saver.SaveClock(_day.DisplayWeek);

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
        // The introduction owns the panel until its closing page is dismissed:
        // hiding a walk page would skip the introduction (a later call opens
        // the menu, not the page), so its 15 s countdown never starts.
        bool introOpen = IntroInProgress;
        if (IsShown && !introOpen && !_resizing && !dialogueOpen && !poiOpen && !keepOpen
            && (now - _shell.LastActivity).TotalSeconds >= IdleHideSec)
        {
            Hide();
        }

        // The request countdown is part of the one allowed loop: integer
        // seconds only, and a timeout finishes the ask exactly once. It runs
        // whether or not EVO is still attached so a closed game cannot freeze
        // an open request. The read-in delay that lets the drop-off task text
        // show first rides the same call.
        _taxi.Tick(now);

        // The session clock's own work: the idle shift timeout (Unity's 30-min
        // rule) so the taxi button flips back to START even when the player
        // never taps it, the game-day flip with the week it completes, and the
        // second-guarded shift countdown. Both job reads arrive as values: an
        // accepted job must not idle the shift out mid-ride, and a live leg
        // repaints the readouts by itself.
        _day.Tick(now, JobInProgress, _taxi.LegActive);

        // Voice acknowledgment: the beat drains first so a due tap acts at
        // its end, then flash expiry runs on the clock, not the minute
        // guard: a half-second teal glow must clear inside the same minute
        // it started. The cached pages' own minute guards live with them.
        // The control page date stays live whether or not EVO is attached;
        // its refresh self-guards on the minute: no per-tick allocations.
        _beat.Poll();
        _nav.Tick();

        // The town ticks on game minutes, whether or not a page is open: one
        // DateTime subtraction per poll, all work on a whole-minute boundary
        // (hot-path discipline). Demand needs an owned hub, like Unity's gate.
        if (_city.AdvanceTo(_game.GameNow, _career.Owned.Count > 0))
        {
            // The log reports the town that gets banked (every cell), not the
            // career-gated header the pages show: a fresh career sees 0 on
            // screen while its stores are still drifting behind it.
            _log.LogInfo($"city: {_city.TotalCitizens(_deliveryBoard.Catalog.Stores)} citizens, " +
                $"{_city.TotalGrowthPerHour(_deliveryBoard.Catalog.Stores)}/hr");
            // Unity repaints the board from OnCitizensChanged; only the cached
            // page needs the push, the hub page is rebuilt on every visit.
            _delivery.RefreshBoard();
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
        // while EVO is closed, and after the session clock's own saves because
        // a banked moment is exactly what a kill between sessions would
        // otherwise lose. SaveClock restamps, so this costs one write per
        // ClockSaveSec.
        if ((now - _saver.LastClockSave).TotalSeconds >= ClockSaveSec)
        {
            SaveClock();
        }

        // One read per tick: the formatter decides what the shown values say,
        // and hands back only the strings that changed (no per-tick work on a
        // parked car or a paused game).
        var s = _telemetry.TryRead();
        Apply(_readouts.Paint(s, _telemetry.GameRunning));
        if (s is null)
        {
            _hud?.Update(null, _garage.Current?.Odometer, _telemetry.GameRunning);
            return;
        }

        if (s.Track is { LengthMeters: > 0.0 } track && double.IsFinite(track.LengthMeters))
        {
            _taxi.SetTrackLength(track.LengthMeters);
        }

        // Odometer feed (PORTPLAN items 31/32, Unity DrivingManager's odometer
        // integration): every live tick banks the distance the current car
        // covered into the wear deck's input, and the engine inspections ride
        // the drive. The garage file is banked on the same wall-time throttle
        // as the clock, so a kill costs at most the window.
        _fleet.IntegrateOdometer(s, dt, OnboardingDone);
        _hud?.Update(
            s, _garage.Current?.Odometer, gameRunning: true, _fleet.IsDrivingWrongCar());
        if ((now - _saver.LastGarageSave).TotalSeconds >= OdometerSaveSec)
        {
            SaveGarage();
        }

        // Meter runs while a paid leg is live: the taxi passenger leg and the
        // delivery store legs share the one accumulator (the two jobs cannot
        // overlap). The stalled-frame guard and the speed clamp keep a torn
        // read from inflating a fare or a delivery charge. The delivery run
        // resets it when the first store leg starts, so hub pickups are not
        // charged as delivery distance.
        if (_taxi.LegActive)
        {
            // The flow owns the leg: meter, distance, fare and the patrols.
            _taxi.AdvanceLeg(s, dt);
        }
        else if (_delivery.InProgress)
        {
            // The delivery twin: the same shared accumulator, reset by the flow
            // at the first store leg.
            _delivery.Advance(s, dt);
        }
    }

    // Copies the paint the readout formatter returned: a null member means the
    // shown value did not change, so nothing is formatted or notified twice.
    private void Apply(ReadoutPaint paint)
    {
        if (paint.Live is { } live) IsLive = live;
        if (paint.Status is { } status) StatusText = status;
        if (paint.Speed is { } speed) SpeedText = speed;
        if (paint.Gear is { } gear) GearText = gear;
        if (paint.Detail is { } detail) DetailText = detail;
        if (paint.Job is { } job) JobLine = job;
        if (paint.Fare is { } fare) FareText = fare;
        if (paint.Shift is { } shift) ShiftText = shift;
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
    public void RecordActivity() => _shell.RecordActivity(_clock.UtcNow);

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
        _shell.SetShown(true);
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
        _nav.ShowMenuPage();
        _log.LogInfo("overlay shown (menu)");
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
        _nav.ShowScreen(_story.CreateSettingsPage(_nav.ShowMenuPage, resetGame));
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
        _shell.SetShown(false);
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

    private void OnJobEntered()
    {
        if (_delivery.InProgress)
        {
            _delivery.Deliver();
        }
        else if (PickupCommand.CanExecute(null))
        {
            PickupCommand.Execute(null);
        }
        else if (DropoffCommand.CanExecute(null))
        {
            DropoffCommand.Execute(null);
        }
    }

    // Reveals the panel on a job page (refusal, breakdown, receipt, shift
    // ended). Show() deliberately opens the menu instead, so the page must be
    // set first.
    private void ShowJobScreen(object screen)
    {
        RecordActivity();
        _introShown = true;
        _shell.Reveal(screen);
        _log.LogInfo("overlay shown (job)");
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

    private string PlayerName() => _story.Profile?.DisplayName ?? string.Empty;

    private string HomeCountry => _story.Profile?.Country ?? _addressMap.Country;

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

    // The map side of one stop: the door PoiWorld placed or the shop's own
    // address. The flow asks for the pin; the world owns the door, the shell
    // only formats the companion line and the arrival radius.
    private HudJobGuidance? DeliveryGuidance(DeliveryStop leg)
    {
        if (_poiWorld.StopDoor(leg) is not { } target)
        {
            return null;
        }

        return leg.Kind == DeliveryStopKind.Hub
            ? new HudJobGuidance(
                leg.Name, target,
                "HudDeliveryHubDrive", "HudDeliveryHubArrived", "HudDeliveryHubAction",
                _taxiRules.ArrivalRadiusMeters)
            : new HudJobGuidance(
                leg.Name, target,
                "HudDeliveryStoreDrive", "HudDeliveryStoreArrived", "HudDeliveryStoreAction",
                _taxiRules.ArrivalRadiusMeters);
    }

    // The job, fare and shift lines the formatter paints, plus the two job
    // commands: their can-execute reads the flow's phase, so the same edge that
    // repaints the line re-arms the buttons.
    private void RefreshJob()
    {
        Apply(TelemetryReadouts.PaintJob(_taxi.Job, _shift, _clock.UtcNow));
        ((RelayCommand)PickupCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DropoffCommand).RaiseCanExecuteChanged();
    }

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
