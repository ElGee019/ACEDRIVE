using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The panel's page graph (Unity's ScreenManager): the control page, the menu,
/// every page a tile opens — calendar, home, wallet, trunk, garage, addresses,
/// the delivery board and the hub — and the bought-car choice.
///
/// It owns the page caches (control, menu and calendar are built once so their
/// state survives leaving the page, exactly like Unity's cached screens; the
/// rest are rebuilt on every visit) and the one "swap the page" seam, which
/// also drops a pending voice tap so an old beat can never fire after the
/// player moved on.
///
/// Pages take commands and data, never policy: the entry whose domain lives
/// elsewhere (the settings page with RESET GAME) and the outcomes the
/// coordinator banks (a timed-out shift, the readouts) travel through
/// <see cref="INavigationHost"/>. The display week is read from
/// <see cref="DayCycle"/>, which owns the day flip that rolls it (S5).
/// </summary>
public sealed class Navigation
{
    private readonly OverlayShell _shell;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    // Page data: each page reads the live instance, never a snapshot.
    private readonly GameClock _game;
    private readonly IClock _clock;
    private readonly TaxiShift _shift;
    private readonly TodoBoard _todos;
    private readonly RoadTestLedger _roads;
    private readonly DriverWallet _wallet;
    private readonly Garage _garage;
    private readonly IRivalsService _rivals;
    // The operating-zone board: the expansion page shows its rank, and the
    // navigator ranks the new area before opening the page, so the block reads
    // a fresh result on construct (never polls).
    private readonly IZoneStandings _zoneStandings;
    private readonly Trunk _trunk;
    private readonly Glovebox _glovebox;
    private readonly Hunger _hunger;
    private readonly StoreCatalog _stores;
    private readonly PoiDirectory _directory;
    private readonly ITelemetryHud? _hud;
    private readonly DeliveryBoard _board;
    private readonly DeliveryCareer _career;
    private readonly CityEconomy _city;
    // The hired crew and the finished-job flags the employees page paints.
    // The crew is the same roster the community centre hires into, so a hire
    // shows up on both pages without a second copy.
    private readonly EmployeeRoster _employeeRoster;
    private readonly EmployeeJobBoard _employeeJobs;
    private readonly EmployeeAssignments _employeeAssignments;
    private readonly CarShareJobs _carShareJobs;
    private readonly CarShareRequestPlanner _carShareRequests;
    private readonly EmployeeRides _employeeRides;
    private readonly EmployeeDeliveries _employeeDeliveries;
    private readonly EmployeeDeliveryPayout _employeeDeliveryPayout;

    // The collaborators these pages belong to: the proposal page and the board
    // are theirs, the doors behind a delivery stop are the world's, and the
    // haul receipt is the haul's.
    private readonly TaxiJobFlow _taxi;
    private readonly DeliveryFlow _delivery;
    private readonly CarHaulFlow _haul;
    private readonly PoiWorld _world;

    // The session's calendar: the display week every page stamps is read from
    // it, and a stale tap closes through it, so no mirror of the week lives
    // here.
    private readonly DayCycle _day;

    // The operating zone, read only for the Cento Miglia gates: the event opens
    // no tier before the driver has discovered a street (Unity's
    // discoveredStreets check).
    private readonly OperatingZone _zone;

    // The Cento Miglia tier table (Data/cento_miglia.json), loaded once at
    // startup: content, so the page never reads a file itself.
    private readonly IReadOnlyList<CentoTier> _centoTiers;

    // The route the event page builds is planned here, from the zone's streets
    // on the shared address map (PORTPLAN 49): the page only reports it.
    private readonly AddressMap _addressMap;

    // The profile's street id: the home street leads the zone's streets while
    // the placeholder map has no per-street zone identity.
    private readonly Func<string?> _playerStreetId;

    // The entry's race flow: the page's built event starts it, and the flow then
    // owns the drive, the registration check and the finish.
    private readonly CentoRaceFlow _cento;

    // The weekly market valuation (PORTPLAN 49): the calendar's Monday row is
    // its job-centre button, so the tap starts the drive leg and the flow owns
    // the venue, the showcase page and the comparison from there.
    private readonly ValuationFlow _valuation;

    // The weekly missing-person run (ToWhereIsWilly.md): the calendar's
    // Wednesday row starts it, and while it is in flight the other event rows,
    // the taxi shift and the delivery career refuse — Unity's blocked job
    // centre.
    private readonly WhereIsWillyFlow _willy;

    // Friday's pancake visit (PORTPLAN 52): the row opens its briefing, START
    // pins grandma's house and the ENTER at her door serves the plate that keeps
    // the appetite's one-shot event alive. The flow owns the leg and its pages,
    // so the row is one call beside the other events.
    private readonly PancakeFlow _pancakes;

    // The care-package run (PORTPLAN 21): the jobs page's DELIVER CARE PACKAGES
    // starts it, and while it owns the companion the other job entries refuse,
    // exactly like the missing-person run.
    private readonly CarePackageFlow _care;

    // The calendar's once-per-week stamps for the events that keep no ledger of
    // their own (the road test, Cento Miglia and the Touristenfahrten run): a
    // row already opened this week refuses instead of opening a second page.
    private readonly WeeklyEventLedger _weeklyEvents;

    // The availability rule the strip's rows read: the same one the day cycle
    // watches for the menu's calendar tile, so a taken row and a resting tile
    // can never disagree.
    private readonly CalendarGate _gate;

    // The road test (PORTPLAN 49): the Tuesday row is its job-centre button, so
    // the tap sends the player to La Strada and the flow owns the drive, the
    // 0-60 leg and the result page from there.
    private readonly RoadTestFlow _roadTest;

    // Saturday's Touristenfahrten delivery run: the row starts the shop-to-corner
    // run, and the flow owns the pickup, the dropoffs and their receipts.
    private readonly TouristenfahrtenFlow _touristen;

    // The event page's registration draw, the one randomness its ENTER flow
    // owns: a fresh number per entry, never a shared global.
    private readonly Random _rng;

    // The brand the wake word listens for: the menu's reminder reads it live,
    // so the cached menu never names the car the player already sold.
    private readonly Func<string> _carBrand;
    // The call the setup page assigned, read live by the cached menu's "you
    // can also press …" reminder.
    private readonly Func<string> _callLabel;

    // The pages' banks: the garage file, the road-test pick, the shift start and
    // the trunk's driver save all go through the one saver the root built.
    private readonly GameStateSaver _saver;

    // Built once and reused: the control page flips its shift button, the menu
    // keeps its clock ticking behind another page, and the calendar keeps its
    // week rows, all like Unity's cached screens.
    private MainControlScreenViewModel? _control;
    private CalendarScreenViewModel? _calendar;
    private MenuScreenViewModel? _menu;
    private ParameterizedCommand<int>? _openDay;
    private readonly ICommand _deliverCarePackagesCommand;

    // Weekday number of the road-test event (Unity: Tuesday), read off the
    // calendar data by RoadTestDayOfWeek, never hardcoded here.
    private readonly int _roadTestDay = RoadTestDayOfWeek();

    // Weekday number of the Cento Miglia event (Unity: Sunday), read the same
    // way: the strip owns which weekday hosts the event.
    private readonly int _centoDay = CentoDayOfWeek();

    // Weekday number of the weekly market valuation (Unity: Monday), read off
    // the calendar data like the other two events.
    private readonly int _valuationDay = ValuationDayOfWeek();

    // Weekday number of the weekly missing-person run (Unity: Wednesday), read
    // the same way: the strip owns which weekday hosts the event.
    private readonly int _willyDay = WillyDayOfWeek();

    // Weekday number of Friday's pancake visit, read off the same calendar data
    // so the strip keeps owning which weekday hosts the event.
    private readonly int _pancakeDay = PancakeDayOfWeek();

    // Weekday number of the Saturday Touristenfahrten run, read off the same
    // calendar data so the strip keeps owning which weekday hosts the event.
    private readonly int _touristenDay = TouristenDayOfWeek();

    // TEMPORARY (testing): forwarded to the calendar page so every event row
    // answers on any day and hour (see CalendarScreenViewModel). Only the tests
    // set it; the app keeps the shipping gates.
    private readonly bool _ignoreEventGates;

    // WHY late: the host is the coordinator, which is built after this
    // navigator. Every member below is only reachable from a page command, so
    // the attach in the coordinator's constructor always comes first.
    private INavigationHost _host = null!;

    public Navigation(
        OverlayShell shell, VoiceBeat beat,
        GameClock game, IClock clock, TaxiShift shift, TodoBoard todos,
        RoadTestLedger roads, GameStateSaver saver,
        DriverWallet wallet, Garage garage, IRivalsService rivals,
        IZoneStandings zoneStandings,
        Trunk trunk, Glovebox glovebox, Hunger hunger, StoreCatalog stores,
        PoiDirectory directory, ITelemetryHud? hud,
        DeliveryBoard board, DeliveryCareer career, CityEconomy city,
        EmployeeRoster employeeRoster, EmployeeJobBoard employeeJobs,
        EmployeeAssignments employeeAssignments, CarShareJobs carShareJobs,
        CarShareRequestPlanner carShareRequests,
        EmployeeRides employeeRides, EmployeeDeliveries employeeDeliveries,
        EmployeeDeliveryPayout employeeDeliveryPayout,
        TaxiJobFlow taxi, DeliveryFlow delivery, CarHaulFlow haul, PoiWorld world,
        DayCycle day, OperatingZone zone, IReadOnlyList<CentoTier> centoTiers,
        AddressMap addressMap, Func<string?> playerStreetId, CentoRaceFlow cento,
        ValuationFlow valuation, WhereIsWillyFlow willy, PancakeFlow pancakes,
        CarePackageFlow care, RoadTestFlow roadTest, TouristenfahrtenFlow touristen,
        WeeklyEventLedger weeklyEvents, CalendarGate gate,
        Func<string> carBrand, Func<string> callLabel,
        Random rng, ILog log, bool ignoreEventGates = false)
    {
        _shell = shell;
        _beat = beat;
        _game = game;
        _clock = clock;
        _shift = shift;
        _todos = todos;
        _roads = roads;
        _saver = saver;
        _wallet = wallet;
        _garage = garage;
        _rivals = rivals;
        _zoneStandings = zoneStandings;
        _trunk = trunk;
        _glovebox = glovebox;
        _hunger = hunger;
        _stores = stores;
        _directory = directory;
        _hud = hud;
        _board = board;
        _career = career;
        _city = city;
        _employeeRoster = employeeRoster;
        _employeeJobs = employeeJobs;
        _employeeAssignments = employeeAssignments;
        _carShareJobs = carShareJobs;
        _carShareRequests = carShareRequests;
        _employeeRides = employeeRides;
        _employeeDeliveries = employeeDeliveries;
        _employeeDeliveryPayout = employeeDeliveryPayout;
        _taxi = taxi;
        _delivery = delivery;
        _haul = haul;
        _world = world;
        _day = day;
        _zone = zone;
        _centoTiers = centoTiers;
        _addressMap = addressMap;
        _playerStreetId = playerStreetId;
        _cento = cento;
        _valuation = valuation;
        _willy = willy;
        _pancakes = pancakes;
        _care = care;
        _weeklyEvents = weeklyEvents;
        _gate = gate;
        _roadTest = roadTest;
        _touristen = touristen;
        _carBrand = carBrand;
        _callLabel = callLabel;
        _rng = rng;
        _log = log;
        _deliverCarePackagesCommand = new RelayCommand(DeliverCarePackages);
        _ignoreEventGates = ignoreEventGates;

        ControlCommand = new RelayCommand(ShowControl);
        MenuCommand = new RelayCommand(ShowMenuPage);
        GarageCommand = new RelayCommand(OpenGarage);
        TaxiShiftCommand = new RelayCommand(OpenTaxiShift);
    }

    /// <summary>The control page's START/CONTINUE TAXI entry.</summary>
    public ICommand TaxiShiftCommand { get; }

    /// <summary>Attaches the coordinator that banks the shift and builds the settings page.</summary>
    public void Attach(INavigationHost host) => _host = host;

    /// <summary>Opens the control page (the BACK target of every job page).</summary>
    public ICommand ControlCommand { get; }

    /// <summary>Opens the menu (the BACK target of every menu page).</summary>
    public ICommand MenuCommand { get; }

    /// <summary>Opens the garage page (the home page's tile).</summary>
    public ICommand GarageCommand { get; }

    /// <summary>
    /// Swaps the visible page. WHY the beat cancel rides here: a real screen
    /// change (wake-word re-home, mouse navigation) supersedes a pending voice
    /// tap, so one utterance can never flip the page after the player moved on.
    /// </summary>
    public void ShowScreen(object? screen)
    {
        _beat.Cancel();
        _shell.ShowScreen(screen);
    }

    /// <summary>Swaps to the menu page without touching visibility.</summary>
    public void ShowMenuPage() => ShowScreen(MenuPage());

    /// <summary>Swaps to the control page without touching visibility.</summary>
    public void ShowControl() => ShowScreen(ControlPage());

    // Built once and reused. The taxi entry opens the job proposal page (port
    // of Unity TaxiJobProposalScreen); delivery opens the career page (port
    // of Unity DeliveryJobScreen); the haul opens its offer/active-leg page;
    // the police entry is the page's own placeholder, so it takes no command
    // from here. The weekly strip lives on its own page (see CalendarPage).
    public MainControlScreenViewModel ControlPage()
    {
        _control ??= new MainControlScreenViewModel(
            _beat, _clock, _game, _todos, _shift,
            new ControlActions(
                TaxiShiftCommand, new RelayCommand(ShowDelivery),
                new RelayCommand(OpenHaul), _deliverCarePackagesCommand,
                CareDeliveryAvailable),
            MenuCommand, _day.DisplayWeek);
        return _control;
    }

    // The bag threshold is the flow's (Unity TipsCompletedScreen): the completed
    // block must list five tips, and the town must hold the kits, before the run
    // is offered — a leftover kit from an earlier run cannot raise the button on
    // its own. A previous session's kits already in the car still offer the run.
    private bool CareDeliveryAvailable() => _care.CanOffer;

    // DELIVER CARE PACKAGES: the flow starts the run (or resumes the drop-off)
    // and hides the panel; the tips the run just carried are acknowledged like
    // Unity's popup dismissal, so the block does not linger behind a delivered
    // run. A refused start leaves the block and its button exactly as they were.
    private void DeliverCarePackages()
    {
        if (RefuseWhenTooHungry("care packages"))
        {
            return;
        }

        if (!_care.Begin())
        {
            return;
        }

        _todos.AcknowledgeCommand.Execute(null);
    }

    // Unity's blocked job centre (PORTPLAN item 26, MapScreen's HungerBlockPopup):
    // at hunger >= maxHunger - 1 every job entry refuses, and the tap still
    // answers — the popup says why, names the two ways to eat and jumps to the
    // trunk in one tap. WHY only the job entries and not the whole calendar:
    // stores stay reachable, and so does Friday's pancake visit, so a broke
    // driver with an empty trunk is never locked out of the one free meal the
    // game hands over. A silent refusal would read as a broken button.
    private bool RefuseWhenTooHungry(string entry)
    {
        if (!_hunger.Blocks)
        {
            return false;
        }

        _log.LogInfo(
            $"{entry} refused: TOO HUNGRY at {_hunger.Value}/{_hunger.Max}");
        ShowScreen(new TooHungryScreenViewModel(
            _hunger, new RelayCommand(OpenTrunk), new RelayCommand(ShowControl), _beat));
        return true;
    }

    /// <summary>Re-reads the cached control page (the taxi flow's tick does).</summary>
    public void RefreshControl() => _control?.Refresh();

    /// <summary>
    /// Names a refused haul tap on the control page's own button (the page
    /// owns the three-second hold). The page is always built by the time a haul
    /// tap reaches here: the tap comes from it.
    /// </summary>
    public void ShowHaulGate(string text) => _control?.ShowHaulGate(text);

    // Unity JobSelectionScreen's OnStartTaxiShift: the tap owns the shift
    // bookkeeping (start fresh, or end an idle-timed-out one first), then opens
    // the proposal page — the job itself only exists once ACCEPTed. A shift that
    // already timed out is the coordinator's to close and show (it owns the
    // shift record and its summary page).
    private void OpenTaxiShift()
    {
        if (RefuseWhenTooHungry("taxi shift"))
        {
            return;
        }

        // Unity's blocked job centre (ToWhereIsWilly.md §2.1): a missing-person
        // run in flight starts no second job, so the shift button is refused
        // until the receipt or a give-up clears it.
        if (_willy.InFlight)
        {
            _log.LogInfo("taxi shift refused: the missing-person run is in flight");
            return;
        }

        // The same lock for the care-package run: the companion guides one
        // drive at a time, so a live pickup route starts no second job.
        if (_care.InFlight)
        {
            _log.LogInfo("taxi shift refused: the care-package run is in flight");
            return;
        }

        DateTime now = _clock.UtcNow;
        if (_shift.IsTimedOut(now) && _shift.TryEndTimedOut(now, out TimeSpan idle))
        {
            _host.ShiftTimedOut(idle);
            return;
        }

        if (!_shift.IsStarted)
        {
            _shift.Start(now);
            _log.LogInfo("taxi shift started");
            // Checkpoint bank: a shift start is the moment a player reads as
            // progress, so the calendar never rewinds past it when the process
            // dies before the next throttled write. The shift itself is banked
            // too: an empty shift must still idle out across a restart.
            _saver.SaveClock(_day.DisplayWeek);
            _saver.SaveShift();
        }

        // The proposal page is the flow's: built once and reused like the
        // delivery board, so the committed radius survives leaving the page.
        ShowScreen(_taxi.OpenProposal());
        _host.ReadoutsChanged();
        RefreshControl();
    }

    // Built once and reused: the pickup cart survives leaving the page, like
    // Unity's player.pickupCart. The run button carries the whole cart run to
    // the flow, which drives the hub pickups and store drop-offs on the
    // companion before it settles the receipt. The hub button opens the real
    // OPEN NEW CAREER page (item 18).
    private void ShowDelivery()
    {
        if (RefuseWhenTooHungry("delivery career"))
        {
            return;
        }

        // The same lock the shift reads: the delivery career is the other job a
        // missing-person run may not start beside (Unity's blocked job centre).
        if (_willy.InFlight)
        {
            _log.LogInfo("delivery career refused: the missing-person run is in flight");
            return;
        }

        if (_care.InFlight)
        {
            _log.LogInfo("delivery career refused: the care-package run is in flight");
            return;
        }

        // A hub bought on the previous visit may have opened store doors; the
        // board must include them before the cached page paints.
        _world.Grow();
        ShowScreen(_delivery.OpenBoard());
    }

    // The haul tap opens the flow's page (offer or active leg); CONTINUE/BACK
    // is the control page it was opened from. The flow owns the run, the HUD
    // leg and the receipt.
    private void OpenHaul()
    {
        if (RefuseWhenTooHungry("car hauling"))
        {
            return;
        }

        if (_care.InFlight)
        {
            _log.LogInfo("haul refused: the care-package run is in flight");
            return;
        }

        ShowScreen(_haul.Open(ControlCommand, ControlPage()));
    }

    /// <summary>Opens the hub page (Unity's OPEN NEW CAREER, PORTPLAN 18).</summary>
    public void ShowHub() =>
        ShowScreen(new DeliveryHubScreenViewModel(
            _career, _log,
            new RelayCommand(CloseHub),
            _beat,
            hub =>
            {
                _world.PlaceHubDoor(hub);
                // A bought hub is career state the player can see, so it goes
                // to disk now: closing the overlay before the first settled run
                // must not lose the career that was just paid for.
                _saver.SaveDriver(_rivals.LastResult);
            }));

    /// <summary>
    /// The board itself, for the paths that come back to it from elsewhere (the
    /// hub page's BACK after a purchase).
    /// </summary>
    public void ShowDeliveryBoard() =>
        ShowScreen(_delivery.Board ?? (object)ControlPage());

    /// <summary>
    /// The poll's page maintenance: the flash edges, the board's timed refusal
    /// and the minute-guarded refreshes of the three cached pages. Warm path
    /// only — each page does one int compare per tick at worst (hot-path
    /// discipline).
    /// </summary>
    public void Tick()
    {
        _control?.UpdateFlash();
        _control?.Refresh();
        _calendar?.UpdateFlash();
        _calendar?.Refresh();
        _menu?.UpdateFlash();
        _menu?.UpdateReveal();
        _menu?.Refresh();
        // The cached board's three-second refusal outlives its own page, so
        // it clears here whether or not the board is the shown screen.
        _delivery.UpdateNotice();
    }

    /// <summary>
    /// The day cycle's event gate opened or closed: the cached menu swaps its
    /// calendar tile artwork. A page that is not built yet reads the gate from
    /// its owner when it is (<see cref="MenuPage"/>), so a flip that lands
    /// while the menu is cached away is never lost.
    /// </summary>
    public void EventGateChanged(bool playable) => _menu?.IsCalendarEventPlayable = playable;

    /// <summary>
    /// The day cycle's week roll: the calendar strip and the menu header
    /// repaint now instead of on the next tick, and the road-test pick is named
    /// in the log. The week itself stays with its owner, so only the cached
    /// pages take it as a parameter.
    /// </summary>
    public void DisplayWeekChanged(int week)
    {
        if (_calendar is not null)
        {
            _calendar.DisplayWeek = week;
            _calendar.Refresh();
        }

        if (_menu is not null)
        {
            _menu.DisplayWeek = week;
        }

        // The jobs page carries the same clock header (its setter refreshes the
        // repaint, like the menu's).
        if (_control is not null)
        {
            _control.DisplayWeek = week;
        }

        // The road-test pick deliberately does NOT rotate with the week — an
        // untested car stays on offer until it is actually driven (PORTPLAN 49).
        _log.LogInfo($"week {week} opened: road test stays {DescribeRoadTest()}");
    }

    // Unity CarDealerScreen.OnBuyClicked's tail: the car is already in the
    // garage (the page added it after the cash moved), the purchase is banked,
    // and the follow-up choice opens. Unity shows the summary block only when
    // this was the garage's second car — the starter plus this purchase.
    public void CarBought(CarSpec car)
    {
        _saver.SaveGarage();
        _log.LogInfo($"[Dealer] bought {car.Key}: {CarTitles.Of(car)} " +
            $"for {car.Price:0} CR (cash {_wallet.Cash:0.00}, " +
            $"{_garage.Cars.Count} cars owned)");
        bool second = _garage.Cars.Count == 2;
        ShowScreen(new BoughtFirstCarScreenViewModel(
            car, second,
            new RelayCommand(() => DriveNewCar(car)),
            new RelayCommand(KeepCurrentCar)));
    }

    // Built once and reused; boots on the resumed week (if any), not the
    // stamp default, so the header is right on the first paint. Every event row
    // that owns a page or a leg gets a branch below — the road test sends the
    // player to La Strada, the Cento Miglia day opens the event page, the
    // Monday row starts the valuation leg and the Wednesday row the search —
    // and a placeholder event without a page just logs. Off-gate taps never
    // reach this action: the page's own day command warns them before
    // dispatching.
    private CalendarScreenViewModel CalendarPage()
    {
        _openDay ??= new ParameterizedCommand<int>(OnDay);
        _calendar ??= new CalendarScreenViewModel(
            _beat, _game, _clock, _roads,
            CalendarStamp.Default with { Week = _day.DisplayWeek },
            _openDay, MenuCommand, _gate,
            () => WhereIsWillyRules.HasCoffeeCareer(_directory, _career),
            () => WhereIsWillyRules.OwnsCoffeeCareer(_career),
            _ignoreEventGates);
        return _calendar;
    }

    private void OpenCalendar() => ShowCalendarPage();

    /// <summary>Opens the cached calendar page (a run's receipt CONTINUE lands here).</summary>
    public void ShowCalendarPage() => ShowScreen(CalendarPage());

    private void OnDay(int day)
    {
        // The care-package run owns the companion until the community centre
        // takes the kits: no calendar event may steal its leg.
        if (_care.InFlight)
        {
            _log.LogInfo(
                $"calendar day {day} refused: the care-package run is in flight");
            return;
        }

        // The missing-person lock (ToWhereIsWilly.md §2.1/§3.6): Unity blocks
        // the whole job centre while the run is in flight, so no other event
        // row may dispatch. The Willy row itself is left to the flow, which
        // reopens its own page (or refuses a terminal phase).
        if (_willy.InFlight && day != _willyDay)
        {
            _log.LogInfo(
                $"calendar day {day} refused: the missing-person run is in flight");
            return;
        }

        // The road test owns the companion while its visit runs (Unity's test
        // centre is modal): another event cannot start on top of its drive or
        // its clock. Its own row stays open — that is how a stalled leg is
        // abandoned and the page reopened.
        if (_roadTest.InFlight && day != _roadTestDay)
        {
            _log.LogInfo($"calendar day {day} refused: the road test is in flight");
            return;
        }

        // Friday's pancake visit owns the companion while its door is pinned:
        // the leg is one pinned house and its ENTER completes it, so a second
        // event must not repin the HUD underneath it. The row itself stays open
        // — that is how a stalled briefing is reopened.
        if (_pancakes.InFlight && day != _pancakeDay)
        {
            _log.LogInfo($"calendar day {day} refused: the pancake visit is in flight");
            return;
        }

        if (day == _roadTestDay)
        {
            // The road test is a drive now (PORTPLAN 49): the row sends the
            // player to La Strada and the flow owns the 0-60 leg and its result.
            // The pick is only banked as tested once the whole test (accel,
            // brake, photo) is driven, so the row keeps naming the same car.
            OpenWeekly(ControlCalendar.RoadTest, _roadTest.Begin);
            return;
        }

        // The Cento Miglia page (PORTPLAN 49): Unity JobSelectionScreen's
        // Sunday tap, which the calendar's own gate has already checked against
        // the day and the 8am-8pm window.
        if (day == _centoDay)
        {
            OpenWeekly(ControlCalendar.CentoMiglia, OpenCento);
            return;
        }

        // The weekly market valuation (PORTPLAN 49): the Monday row is the
        // show's job-centre button. The tap opens the week's briefing; its
        // START pins the venue and sends the player off. The flow owns the
        // window gate as well, so a caller can never jump it.
        if (day == _valuationDay)
        {
            _valuation.Begin();
            return;
        }

        // The weekly missing-person run (ToWhereIsWilly.md): the Wednesday row
        // is Unity's job-centre button. The flow owns the gate, the draw and
        // the page; a run already in flight reopens its page.
        if (day == _willyDay)
        {
            _willy.Begin();
            return;
        }

        // Saturday's Touristenfahrten run: the row is the event's only entry,
        // so the tap starts the shop-to-corner delivery. The companion can
        // guide one job at a time, so a live taxi/delivery/Cento job refuses
        // this row instead of stealing its line.
        if (day == _touristenDay)
        {
            // The row has its own first week (the career's first Saturday must
            // be over). The page paints no button before then; the dispatcher
            // refuses it too, so a cached page or a voice tap cannot open it.
            if (!_ignoreEventGates
                && !ControlCalendar.IsOpenInWeek(day, _day.DisplayWeek))
            {
                _log.LogInfo(
                    "touristenfahrten refused: the run opens from the second week");
                return;
            }

            if (_taxi.InProgress || _delivery.InProgress || _cento.InProgress)
            {
                _log.LogInfo("touristenfahrten refused: another job owns the companion");
                return;
            }

            OpenWeekly(ControlCalendar.Touristenfahrten, _touristen.Begin);
            return;
        }

        // Friday's pancake visit (PORTPLAN 52): the row opens the briefing, and
        // its START pins grandma's house. The flow re-checks the weekday, the
        // 8am-8pm window and the week's stamp, so a cached page or a voice tap
        // cannot jump them; the meal's own stamp is written when it is served.
        if (day == _pancakeDay)
        {
            _pancakes.Begin();
            return;
        }

        // Only placeholder rows reach this: an event whose page or leg has not
        // landed yet.
        _log.LogInfo($"calendar day {day} tapped: no page behind this event yet");
    }

    // Unity's per-event "one entry a week" (the valuation's lastValuationGameDay
    // shape) for the events that keep no ledger of their own. The row opens the
    // event once a game day, and the stamp is banked immediately, so a restart
    // inside the same week cannot reopen what was already used — the day-of-week
    // gate above then keeps that day the week's only slot. A refused or empty
    // draw leaves the week untouched, so a missing venue or route costs nothing.
    private void OpenWeekly(string eventId, Func<bool> open)
    {
        DateTime now = _game.GameNow;
        if (_weeklyEvents.TakenOn(eventId, now))
        {
            _log.LogInfo($"calendar event {eventId} refused: already taken this week");
            return;
        }

        if (!open())
        {
            return;
        }

        _weeklyEvents.MarkTaken(eventId, now);
        _saver.SaveDriver(_rivals.LastResult);
    }

    // The event page per visit, never cached: the reputation it prints and the
    // garage size it counts are the driver's current ones, and the route it
    // plans grows as the zone does. BACK returns to the calendar it was opened
    // from; the built event starts the race flow, which owns the drive to the
    // start, the registration check and the finish.
    private bool OpenCento()
    {
        // The year is not persisted (PORTPLAN 51): the game's fixed 1924 seed is
        // what every page stamps, and Unity prints the same pair.
        CalendarStamp stamp = CalendarStamp.Default with { Week = _day.DisplayWeek };
        IReadOnlyList<AddressStreet> streets = CentoStreets();
        if (CentoRoutePlanner.LongestRoute(_addressMap, streets) is not { } route)
        {
            // A map without two doors cannot build a race: refuse the event
            // instead of opening a page whose ENTER would have no route.
            _log.LogWarning(
                $"cento miglia not opened: {streets.Count} zone street(s) hold no two doors");
            return false;
        }

        var facts = new CentoMigliaFacts(
            stamp.Week,
            stamp.Year,
            _wallet.ClubReputation,
            _wallet.Assertivity,
            _zone.StreetIds.Count > 0,
            _garage.Cars.Count,
            CurrentCarClass(),
            route);
        ShowScreen(new CentoMigliaScreenViewModel(
            _centoTiers, facts, new RelayCommand(() => ShowScreen(CalendarPage())),
            _clock, _rng, _cento.Start));
        _log.LogInfo(
            $"cento miglia opened: week {facts.Week}, rep {facts.ClubReputation:0.00}, " +
            $"{facts.CarsOwned} cars, class {facts.CarClass}, {_centoTiers.Count} tiers, " +
            $"route {route.DistanceKm:0.000} km over {streets.Count} street(s)");
        return true;
    }

    // The zone's streets on the address map, home street first (the interim
    // mapping AddressBook.ZoneStreets documents). The race is planned over
    // exactly these, so a fresh one-street career never races on a street it
    // has not discovered.
    private IReadOnlyList<AddressStreet> CentoStreets() =>
        AddressBook.ZoneStreets(_addressMap, _zone.StreetIds.Count, _playerStreetId());

    // The class of the car being driven: Unity reads CarData.Get(player.grandmaCar),
    // the car the player took out of the garage — here the garage's current car.
    // 0 when the garage cannot name it, which is the fresh-driver reading.
    private int CurrentCarClass() =>
        _garage.Current is { } owned && _garage.SpecOf(owned) is { } spec ? spec.Class : 0;

    // Cento Miglia weekday number, read off the calendar data like the road
    // test's: no Unity day literal here.
    private static int CentoDayOfWeek()
    {
        foreach (CalendarDay day in ControlCalendar.Week)
        {
            if (day.EventId == ControlCalendar.CentoMiglia)
            {
                return day.DayOfWeek;
            }
        }

        return -1;
    }

    // Market-valuation weekday number, read off the same calendar data: the
    // strip owns which weekday hosts the event, the flow never assumes Monday.
    private static int ValuationDayOfWeek()
    {
        foreach (CalendarDay day in ControlCalendar.Week)
        {
            if (day.EventId == ControlCalendar.Valuation)
            {
                return day.DayOfWeek;
            }
        }

        return -1;
    }

    // Missing-person weekday number, read off the same calendar data: the
    // strip owns which weekday hosts the event, the flow never assumes
    // Wednesday on its own.
    private static int WillyDayOfWeek()
    {
        foreach (CalendarDay day in ControlCalendar.Week)
        {
            if (day.EventId == ControlCalendar.Willy)
            {
                return day.DayOfWeek;
            }
        }

        return -1;
    }

    private string DescribeRoadTest() =>
        _roads.Current is { } car ? CarTitles.Of(car) : "none";

    // Built once and reused so its clock keeps ticking behind another page. The
    // settings tile is the coordinator's page (it owns the preferences writer
    // and RESET GAME), so it travels through the host. The event gate is the day
    // cycle's to watch, so the page is seeded from it and only hears the flips
    // (<see cref="EventGateChanged"/>) instead of deriving it on the poll; an
    // event taken while the menu was cached away is re-read here, on the reveal.
    public MenuScreenViewModel MenuPage()
    {
        _menu ??= new MenuScreenViewModel(
            new RelayCommand(OpenWallet),
            ControlCommand,
            new RelayCommand(OpenCalendar),
            new RelayCommand(OpenTrunk),
            new RelayCommand(OpenEmployees),
            new RelayCommand(_host.ShowSettings),
            new RelayCommand(OpenHome),
            _hunger, _garage, _carBrand, _callLabel,
            _beat,
            _game,
            _clock,
            _day.DisplayWeek);
        _day.RefreshEventGate();
        _menu.IsCalendarEventPlayable = _day.EventPlayable;
        return _menu;
    }

    // The employees page (PORTPLAN 23, the menu's EMPLOYEES tile): the hired
    // crew only. Each row's NOT ON JOB button opens the assign page for that
    // employee; ON JOB text shows while they are on one, and ARRIVED once a
    // timed job's due minute passed. The player's own car and every running job
    // take a car, so an idle row only offers the button while the garage has an
    // unused one. A fresh page per visit, so a hire and an assignment are both
    // live. The page arms a car-sharing row that is still waiting for work from
    // the day's batch, so it needs the planner's batch.
    private void OpenEmployees()
    {
        ShowScreen(new EmployeesScreenViewModel(
            _employeeRoster, _garage, _employeeAssignments, _carShareJobs,
            _employeeDeliveryPayout,
            CarShareRequestsToday, _game, _clock, BankEmployeeXp, BankEmployeeCr,
            new ParameterizedCommand<string>(OpenAssignJob),
            MenuCommand, _beat,
            // TEMP: car gate off while the assign flow is tested on a one-car
            // save. Drop this argument to restore the rule.
            enforceUnusedCars: false));
    }

    // The assign-a-job page a NOT ON JOB button opens: three work-family buttons
    // plus BACK. A fresh page per employee, carrying that employee's name so a
    // pick assigns the right one. BACK (with or without a pick) returns to a
    // fresh employees page, where the row now reads ON JOB after an assignment.
    private void OpenAssignJob(string? employeeName)
    {
        if (string.IsNullOrWhiteSpace(employeeName))
        {
            return;
        }

        ShowScreen(new AssignJobScreenViewModel(
            employeeName, _employeeRides, _employeeDeliveries, _carShareJobs,
            _employeeJobs, CarShareRequestsToday(),
            new RelayCommand(OpenEmployees), _beat));
    }

    // The employees page's ARRIVED tap banks the job's XP into the driver file
    // through the same saver the other settles use, so a restart keeps it.
    private void BankEmployeeXp(double xp)
    {
        _wallet.AddXp(xp);
        _saver.SaveDriver(_rivals.LastResult);
    }

    // The flat-pay families (taxi, delivery) return CR: the wallet books it as
    // earned income, so the "earn CR" reading counts it the way a settled job
    // does, and the driver file saves it on the same tap.
    private void BankEmployeeCr(double cr)
    {
        _wallet.AddBonus(cr);
        _saver.SaveDriver(_rivals.LastResult);
    }

    // The day's car-sharing batch the assign page shows above the CAR SHARING
    // button. The planner owns the draw; this site only supplies the town's
    // live facts (population, the car being driven, the driver's XP), so the
    // page never reaches for another service.
    private IReadOnlyList<CarShareRequest> CarShareRequestsToday() =>
        _carShareRequests.ForDay(
            _game.GameDay,
            _city.TotalCitizens(_board.Catalog.Stores),
            CurrentCarClass(),
            _wallet.Xp);

    // The home page (Unity YouScreen): the player's own doors, starting with
    // the garage. A fresh page per visit like the wallet, so nothing stale
    // survives between openings; BACK returns to the menu it was opened from.
    private void OpenHome() => ShowScreen(CreateHomeScreen());

    private HomeScreenViewModel CreateHomeScreen() =>
        new(GarageCommand,
            new RelayCommand(OpenAddresses),
            new RelayCommand(OpenSuggestCarMod),
            MenuCommand,
            _beat);

    // The car-mod suggestion page: one centred question, with SUGGEST above
    // BACK in the bottom band. The page is the whole feature for now: there is
    // no submission path yet, so SUGGEST runs an inert command that the future
    // submission flow replaces here, without touching the page.
    private void OpenSuggestCarMod() =>
        ShowScreen(new SuggestCarModScreenViewModel(
            new RelayCommand(OpenHome), new RelayCommand(() => { }), _beat));

    // The addresses page (PORTPLAN 24/27): the discovered POIs alphabetic, the
    // temporary stand-in for markers on the map. A row pins the HUD's drive-to
    // line; driving there and pressing ENTER enters the place. The zone travels
    // along because the page carries its readout (TOZONES §3.5).
    private void OpenAddresses() =>
        ShowScreen(AddressesPage(new RelayCommand(OpenHome)));

    // One builder for the page's two entry points: the home tile (BACK to the
    // home page) and the expansion page's FIND STREET (BACK runs the same
    // continuation the expansion was going to run).
    private AddressesScreenViewModel AddressesPage(ICommand back) =>
        new(_directory, _zone, _hud, back, _beat, _host.DestinationPinned);

    /// <summary>
    /// The operating-zone expansion page (TOZONES §4.5, Unity
    /// ZoneExpansionScreen): the receipt's CONTINUE opens it before the page it
    /// would have shown, and its CONTINUE runs <paramref name="next"/>.
    /// FIND STREET opens the addresses page (the port's map stand-in) with its
    /// BACK running that same continuation, so the button keeps Unity's map
    /// contract without a map. The newly opened business's explanation page sits
    /// in front of that continuation, so every way off the page passes through
    /// it. The first expansion also earns the hunger explanation (see
    /// <see cref="WithHungerIntro"/>). The board is ranked against the new area
    /// first, so the page's standings block renders a fresh result instead of
    /// polling. <paramref name="grandmaCar"/> reaches those pages' copy (the
    /// broker closes on the line only Grandma Sonja's car earns).
    /// </summary>
    public void ShowZoneExpansion(
        ZoneExpansion zone, IReadOnlyList<PointOfInterest> added, Action next,
        bool grandmaCar = false)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(added);
        ArgumentNullException.ThrowIfNull(next);
        _zoneStandings.Refresh(zone.NewKm);
        Action continuation = WithHungerIntro(
            WithPoiIntros(added, next, grandmaCar));
        var page = new ZoneExpansionScreenViewModel(
            zone, added, _zoneStandings, _beat,
            new RelayCommand(continuation),
            new RelayCommand(() =>
                ShowScreen(AddressesPage(new RelayCommand(continuation)))));
        ShowScreen(page);
    }

    // The first street a career discovers also earns the hunger explanation:
    // its page opens ahead of the business pages and the target, and TO TRUNK
    // jumps straight to the trunk page the copy names. A later expansion skips
    // it — the lesson belongs to the first growth only.
    private Action WithHungerIntro(Action next) =>
        _zone.ExpansionCount == 1
            ? () => ShowScreen(new HungerIntroScreenViewModel(
                next, new RelayCommand(OpenTrunk), _beat))
            : next;

    // The explanation pages this growth earned, one per family, ahead of the
    // continuation. A family with no authored copy (a store door) adds none, so
    // an expansion that opened no explained business leaves the target alone.
    private Action WithPoiIntros(
        IReadOnlyList<PointOfInterest> added, Action next, bool grandmaCar)
    {
        IReadOnlyList<PoiIntro> intros = PoiIntroCopy.ForAdded(added, grandmaCar);
        return intros.Count == 0 ? next : () => ShowPoiIntro(intros, 0, next);
    }

    // One page at a time: CONTINUE opens the next family's page, the last one
    // runs the continuation the expansion page would have run.
    private void ShowPoiIntro(
        IReadOnlyList<PoiIntro> intros, int index, Action next)
    {
        if (index >= intros.Count)
        {
            next();
            return;
        }

        ShowScreen(new PoiIntroScreenViewModel(
            intros[index], () => ShowPoiIntro(intros, index + 1, next), _beat));
    }

    /// <summary>
    /// The explanation page for one family whose door opened outside an
    /// expansion (the insurance broker the first crash repair earns): the page
    /// the expansion flow announces, built here so no caller news a page. Null
    /// when the family has no authored copy, so the caller hands straight back
    /// instead of painting a page that would say nothing. Revealing it (and
    /// handing the player back afterwards) stays the caller's policy.
    /// <paramref name="grandmaCar"/> adds the closing line only Grandma Sonja's
    /// car earns (see <see cref="PoiIntroCopy.For"/>).
    /// </summary>
    public object? PoiIntroPage(string type, Action next, bool grandmaCar = false) =>
        PoiIntroCopy.For(type, grandmaCar) is { } intro
            ? new PoiIntroScreenViewModel(intro, next, _beat)
            : null;

    // The trunk page (PORTPLAN 26, Unity YouScreen's TrunkScreen) is rebuilt on
    // every visit like Unity's RebuildTrunkScreen: a shop purchase made since
    // the last look must show, so the page is never cached. EAT banks the trunk
    // and the appetite together — they live in different files, and a bite
    // moves both.
    private void OpenTrunk() =>
        ShowScreen(new TrunkScreenViewModel(
            _trunk, _glovebox, _hunger, _garage, _stores,
            MenuCommand,
            OnTrunkItemEaten,
            _beat));

    private void OnTrunkItemEaten()
    {
        _saver.SaveDriver(_rivals.LastResult);
        _saver.SaveHunger();
    }

    // The wallet page (PORTPLAN 06/43/48/60): a fresh page per visit, so the
    // cash, XP and bank figures always open on the live wallet and the
    // leaderboard on the latest board result. Read-only — opening never writes;
    // BACK returns to the menu it was opened from.
    private void OpenWallet()
    {
        // Re-rank locally against the current display name: the boot board may
        // have landed before the opening screen confirmed it, and the wallet
        // must not keep showing the old 1-of-1 fallback.
        _rivals.Refresh(_wallet.Xp);
        ShowScreen(new WalletScreenViewModel(
            _wallet, _rivals,
            MenuCommand,
            _beat));
    }

    // The garage page (PORTPLAN 30, Unity GarageScreen): a fresh page per visit,
    // so a car bought since the last visit is on it. A switch banks the garage
    // through the shared save (Unity SaveManager.Save) — the page never writes.
    // BACK returns to the home page it is opened from (Unity's You menu).
    private void OpenGarage() =>
        ShowScreen(new GarageScreenViewModel(
            _garage,
            new RelayCommand(OpenHome),
            OnCarSwitched,
            _beat));

    private void OnCarSwitched(string key)
    {
        _saver.SaveGarage();
        _log.LogInfo($"[Garage] now driving {key} ({_garage.Cars.Count} cars owned)");
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

        _saver.SaveGarage();
        ShowControl();
    }

    // Unity BoughtFirstCar.OnCurrentCarClicked: the new car stays parked; the
    // car driven does not change.
    private void KeepCurrentCar()
    {
        _log.LogInfo($"[Dealer] keeping {_garage.CurrentKey ?? "no car"}");
        _saver.SaveGarage();
        ShowControl();
    }

    private void CloseHub()
    {
        // Opening a career is the fact the follow-up tip reads (PORTPLAN 46):
        // sync as the page closes so the tip completes without waiting for the
        // next cold edge. The tip's own store banks the change; the career
        // itself stays in memory until the persistent save lands (PORTPLAN 51).
        _ = _todos.Sync();
        // Unity's backfill runs when the career opens: the newly owned category
        // places its first store before the board is shown again.
        _world.Grow();
        _delivery.RefreshBoard();
        ShowDeliveryBoard();
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

    // Touristenfahrten weekday number, read off the same calendar data: the
    // strip owns which weekday hosts the run, the flow never assumes Saturday.
    private static int TouristenDayOfWeek()
    {
        foreach (CalendarDay day in ControlCalendar.Week)
        {
            if (day.EventId == ControlCalendar.Touristenfahrten)
            {
                return day.DayOfWeek;
            }
        }

        return -1;
    }

    // Pancake-visit weekday number, read off the same calendar data: the strip
    // owns which weekday hosts Friday's visit, the flow never assumes Friday.
    private static int PancakeDayOfWeek()
    {
        foreach (CalendarDay day in ControlCalendar.Week)
        {
            if (day.EventId == ControlCalendar.Pancakes)
            {
                return day.DayOfWeek;
            }
        }

        return -1;
    }
}
