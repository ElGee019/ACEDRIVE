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
    private readonly IFriendshipStore _friends;
    // The car pool behind the road-test ledger, reused for the receipt's
    // passenger lookup (the driven car's ks id resolves against it).
    private readonly IReadOnlyList<RoadTestCar> _cars;
    private readonly IOnboardingStore _onboarding;
    private readonly IContentSource _content;
    // WHY read through on demand: Data/*.json edits (wake aliases, transcript
    // confusions) take effect while the overlay runs; screens built later pick
    // up the new snapshot automatically.
    private OnboardingContent Content => _content.Content;
    private readonly TodoTable _todos;
    private readonly IVoiceListener _voice;
    private readonly ICarBrandSource _carBrands;
    private readonly DeliveryCatalog _deliveryCatalog;
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
    private readonly IGameClockStore _clocks;
    private OnboardingState _progress;
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

    // Companion parity: hidden until "hey {brand}", idle hides after 15 s,
    // one utterance fires one button (muted until the final fragment).
    private const double IdleHideSec = 15.0;
    private const double WakeCooldownSec = 3.0;
    private const string DefaultBrand = "mazda";
    private DateTime _lastActivity;
    private DateTime _lastWake = DateTime.MinValue;
    private string? _mutedLabel;
    private Action? _mutedAction;
    private string _mutedText = string.Empty;
    private bool _resizing;

    // Wake brand = the car currently driven in ACE (watcher-published),
    // never the saved favorite.
    private string _carBrand = DefaultBrand;
    // ks id behind the brand, when the logs announced one: the receipt's
    // passenger lookup resolves against the car pool, never the brand
    // (one brand spans 1- and 4-seaters).
    private string? _carKsId;
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick;
    private bool _disposed;

    private TaxiJobState _job = new();
    private int _lastShiftSecs = -1;

    // Dropoff placeholders until the driving slice feeds real inputs: the
    // agreed 8.2 km ride with every extra on (tip eligible, one speeding
    // fine). Car class has no live source yet either (the log watcher only
    // publishes the brand, never the ks id), so it stays 1 with rarity and
    // street knowledge until active-car tracking lands.
    private const double PlaceholderDistanceKm = 8.2;
    private const int PlaceholderCarClass = 1;
    private const int PlaceholderCarRarity = 1;
    private const int PlaceholderStreetKnowledgePct = 100;
    private const int PlaceholderFines = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(
        ITelemetrySource telemetry, IClock clock, GameClock game,
        TaxiShift shift, ISettingsStore store,
        ILog log, FareTable fares, TaxiJobProposer proposals,
        IOnboardingStore onboarding, IContentSource content,
        IVoiceListener voice, ICarBrandSource carBrands,
        TodoTable todos, DeliveryCatalog deliveryCatalog, DeliveryCareer career,
        CityEconomy city, ICityStore cityStore,
        RoadTestLedger roadTests, IRoadTestStore roadTestStore,
        IGameClockStore clockStore, int startWeek,
        VoiceBeat beat, DriverWallet wallet, IDriverStore driverStore,
        IFriendshipStore friendships, IReadOnlyList<RoadTestCar> cars)
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
        _todos = todos;
        _voice = voice;
        _carBrands = carBrands;
        _deliveryCatalog = deliveryCatalog;
        _career = career;
        _city = city;
        _cityStore = cityStore;
        _wallet = wallet;
        _driverStore = driverStore;
        _friends = friendships;
        _cars = cars;
        _carBrands.BrandChanged += (_, e) => OnCarBrand(e.Brand, e.KsId);
        _progress = _onboarding.Load();
        _lastActivity = _clock.UtcNow;
        _voice.Heard += (_, e) => OnVoiceHeard(e.Text, e.IsFinal);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Poll();
        _lastTick = _clock.UtcNow;
        // WHY not DateTime.MinValue: the first throttled write must land a full
        // interval into the session, so a startup read that failed for a
        // transient reason (locked file) can never be immediately clobbered.
        _lastClockSave = _clock.UtcNow;

        TaxiShiftCommand = new RelayCommand(OpenTaxiShift);
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

        // Calling the app (boot, Ctrl+Shift+C, wake word) opens on the
        // introduction (PORTPLAN Career Start items 2-5): brand page, grandma's
        // car, voice info, birthday dialogue, and only then the main control
        // page (PORTPLAN item 54). The replay re-saves the picks, so a returning
        // player walks the same pages as a first-timer. Missing content degrades
        // to the control page (past the birthday) or a blank screen (before it)
        // instead of trapping the user on a broken page.

        SetScreen(HomeScreen());

        // With a mic we start hidden until the wake word; without one the
        // window stays up so the app remains usable (and testable).
        IsShown = !voice.IsAvailable;
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
    /// size may be restored: the introduction replays on every call, so the
    /// page on screen no longer says whether the player is past onboarding.
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
        _log.LogInfo($"grandma car: {carId}");
        SetScreen(new VoiceInfoScreenViewModel(this, _beat));
    }

    public void ShowDialogue()
    {
        _log.LogInfo("voice info acknowledged: showing birthday dialogue");
        SetScreen(new DialogueScreenViewModel(this, Content.Dialogue, _beat));
    }

    public void FinishDialogue()
    {
        _progress = _progress with { BirthdayDone = true };
        _onboarding.Save(_progress);
        _log.LogInfo("birthday dialogue done: main control page unlocked");
        SetScreen(CreateControlScreen());
    }

    // Introduction home: the first page of the walk every call to the app
    // opens on. The chain it starts (brand -> grandma's car -> voice info ->
    // birthday dialogue) ends on the main control page, so the replay is never
    // a dead end, and its taps re-save the picks like a first run.
    // No new IScreenController verbs: the control page takes its job
    // commands by constructor (composition over interface growth).
    private object? HomeScreen()
    {
        if (Content.Brands.Count == 0)
        {
            // Missing content degrades instead of trapping the player on a
            // page with nothing to tap: past the birthday the control page
            // still opens, before it there is no home to show.
            return _progress.IsComplete ? CreateControlScreen() : null;
        }
        return new BrandScreenViewModel(this, Content.Brands);
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
                _beat, _game, _clock, _roads, _todos,
                CalendarStamp.Default with { Week = _displayWeek },
                _shift,
                TaxiShiftCommand, new RelayCommand(OpenDelivery),
                new RelayCommand(OnCarHaul),
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
        _roads.Current is { } car ? $"{car.Year} {car.Brand} {car.Model}".ToUpperInvariant() : "none";

    // Built once and reused: the pickup cart survives leaving the page, like
    // Unity's player.pickupCart. Back lands on the control page. The run
    // button is a navigation seam only: the pickup/drop-off wizard needs the
    // freeroam map, so it just logs until that page lands. The hub button
    // opens the real OPEN NEW CAREER page (item 18).
    private void OpenDelivery()
    {
        _deliveryScreen ??= new DeliveryJobScreenViewModel(
            _deliveryCatalog, _career, _city, _log,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            new RelayCommand(OnDeliveryRun),
            new RelayCommand(OpenHub),
            _beat);
        SetScreen(_deliveryScreen);
    }

    private void OnDeliveryRun() =>
        _log.LogInfo("deliver tapped: pickup wizard lands with the freeroam map");

    // Car-hauling contracts (PORTPLAN 20) have no flow yet: the button is a
    // placeholder that only answers with the teal tap glow, so a tap logs and
    // does nothing else — same stand-in shape as the calendar day rows.
    private void OnCarHaul() =>
        _log.LogInfo("car haul tapped: contracts land with PORTPLAN 20");

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
        _carKsId = _carBrands.CurrentKsId;
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
        if (IsShown && !_resizing && !dialogueOpen && (now - _lastActivity).TotalSeconds >= IdleHideSec)
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
            return;
        }

        IsLive = true;
        StatusText = Strings.Format("EvoLive", s.PacketId);
        SpeedText = Strings.Format("SpeedKmh", s.SpeedKmh);
        GearText = TelemetryConventions.GearDisplay(s.Gear);
        DetailText = Strings.Format("EvoDetail", s.Rpm, s.Gas, s.Brake);

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
        // The single "the app was called" seam: the Ctrl+Shift+C hotkey and the
        // wake word both land here, so either call replays the introduction
        // pages instead of resuming whatever page was last open. It rides the
        // hidden -> shown edge only — a Show() while already visible must not
        // yank the player off the page they are using.
        SetScreen(HomeScreen());
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
    private void OnCarBrand(string brand, string? ksId)
    {
        try
        {
            _timer.Dispatcher.Invoke(() =>
            {
                _carBrand = brand;
                _carKsId = ksId;
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
    // ride (8.2 km, tip eligible, one fine) settles here and the receipt
    // screen shows at once. Pickup()/Dropoff() below are the driving
    // slice's hooks and stay dormant until telemetry feeds them.
    private void TaxiAccepted(TaxiJobState? job)
    {
        if (job is null) return;
        _job = job;
        _log.LogInfo($"job accepted: {job.Customer.Full} {job.PickupName} -> {job.DropoffName}");
        SettleReceipt();
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
        int capacity = ResolveCapacity();
        // Unity keys familiarity off the base name (no companion suffix):
        // every completed ride is +1 for that resident, strangers included
        // (0 -> 1 on the first ride). The outcome rides the receipt so the
        // page renders it; transitions log, quiet rides do not.
        string baseName = BaseCustomerName(_job.Customer);
        FriendshipOutcome friendship = _friends.RegisterRide(baseName);
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            _job.DistanceKm, capacity, PlaceholderCarClass,
            PlaceholderCarRarity, PlaceholderStreetKnowledgePct,
            tipEligible: true, PlaceholderFines, _fares, _wallet.Xp,
            new FriendshipResult(friendship.OwnerName, friendship.Level, friendship.TierIncreased));
        if (friendship.TierIncreased)
        {
            _log.LogInfo($"friendship tier up: {friendship.OwnerName} -> {friendship.Level}");
        }
        _job.Fare = receipt.BaseTotal;
        _job.Tip = receipt.Tip;
        _job.Fine = receipt.FineDeduction;
        _wallet.AddReceipt(receipt);
        _driverStore.Save(_wallet.Snapshot());
        _shift.RecordCompleted(_job, _clock.UtcNow);
        // Checkpoint bank: a paid job is progress the player can see, so the
        // moment goes to disk now instead of waiting for the throttle.
        SaveClock();
        _log.LogInfo($"job dropoff: {_job.PickupName} -> {_job.DropoffName} " +
                  $"dist={receipt.DistanceKm:0.00}km total={receipt.Total:0.00} xp={receipt.XpEarned:0.00} " +
                  $"wallet={_wallet.Cash:0.00} shift={_shift.JobsDone} jobs / {_shift.Earned:0.00}");
        SetScreen(new TaxiReceiptScreenViewModel(
            receipt, _job.Customer.Full, _wallet, _shift, _beat,
            new RelayCommand(() => SetScreen(CreateControlScreen()))));
        RefreshJob();
    }

    // Passenger seats of the driven car (Unity: passengerCapacity scales
    // the distance charge). Unknown car (EVO quiet, pool gap) rides solo;
    // a 0-seat race entry still takes one customer. Builder clamps too.
    private int ResolveCapacity()
    {
        if (_carKsId is not null)
        {
            foreach (RoadTestCar car in _cars)
            {
                if (string.Equals(car.Id, _carKsId, StringComparison.Ordinal))
                {
                    return Math.Max(1, car.Passengers);
                }
            }
        }
        return 1;
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
