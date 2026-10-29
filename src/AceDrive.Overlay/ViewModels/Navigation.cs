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
    private readonly Hunger _hunger;
    private readonly StoreCatalog _stores;
    private readonly PoiDirectory _directory;
    private readonly ITelemetryHud? _hud;
    private readonly DeliveryBoard _board;
    private readonly DeliveryCareer _career;
    private readonly CityEconomy _city;

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
    // centre. The ledger rides along for the cached calendar page's label.
    private readonly WhereIsWillyFlow _willy;
    private readonly WhereIsWillyLedger _willyLedger;

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

    // Weekday number of the Saturday Touristenfahrten run, read off the same
    // calendar data so the strip keeps owning which weekday hosts the event.
    private readonly int _touristenDay = TouristenDayOfWeek();

    // TEMPORARY (testing): forwarded to the calendar page so every event row
    // answers on any day and hour (see CalendarScreenViewModel).
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
        Trunk trunk, Hunger hunger, StoreCatalog stores,
        PoiDirectory directory, ITelemetryHud? hud,
        DeliveryBoard board, DeliveryCareer career, CityEconomy city,
        TaxiJobFlow taxi, DeliveryFlow delivery, CarHaulFlow haul, PoiWorld world,
        DayCycle day, OperatingZone zone, IReadOnlyList<CentoTier> centoTiers,
        AddressMap addressMap, Func<string?> playerStreetId, CentoRaceFlow cento,
        ValuationFlow valuation, WhereIsWillyFlow willy, WhereIsWillyLedger willyLedger,
        RoadTestFlow roadTest, TouristenfahrtenFlow touristen,
        Func<string> carBrand, Random rng, ILog log, bool ignoreEventGates = false)
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
        _hunger = hunger;
        _stores = stores;
        _directory = directory;
        _hud = hud;
        _board = board;
        _career = career;
        _city = city;
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
        _willyLedger = willyLedger;
        _roadTest = roadTest;
        _touristen = touristen;
        _carBrand = carBrand;
        _rng = rng;
        _log = log;
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
    // of Unity DeliveryJobScreen); the haul settles its placeholder run and
    // shows the flow's receipt; the police entry is the page's own
    // placeholder, so it takes no command from here. The weekly strip lives on
    // its own page (see CalendarPage).
    public MainControlScreenViewModel ControlPage()
    {
        _control ??= new MainControlScreenViewModel(
            _beat, _clock, _todos, _shift,
            new ControlActions(
                TaxiShiftCommand, new RelayCommand(ShowDelivery),
                new RelayCommand(SettleHaul), _todos.AcknowledgeCommand),
            MenuCommand);
        return _control;
    }

    /// <summary>Re-reads the cached control page (the taxi flow's tick does).</summary>
    public void RefreshControl() => _control?.Refresh();

    // Unity JobSelectionScreen's OnStartTaxiShift: the tap owns the shift
    // bookkeeping (start fresh, or end an idle-timed-out one first), then opens
    // the proposal page — the job itself only exists once ACCEPTed. A shift that
    // already timed out is the coordinator's to close and show (it owns the
    // shift record and its summary page).
    private void OpenTaxiShift()
    {
        // Unity's blocked job centre (ToWhereIsWilly.md §2.1): a missing-person
        // run in flight starts no second job, so the shift button is refused
        // until the receipt or a give-up clears it.
        if (_willy.InFlight)
        {
            _log.LogInfo("taxi shift refused: the missing-person run is in flight");
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
        // The same lock the shift reads: the delivery career is the other job a
        // missing-person run may not start beside (Unity's blocked job centre).
        if (_willy.InFlight)
        {
            _log.LogInfo("delivery career refused: the missing-person run is in flight");
            return;
        }

        // A hub bought on the previous visit may have opened store doors; the
        // board must include them before the cached page paints.
        _world.Grow();
        ShowScreen(_delivery.OpenBoard());
    }

    // The haul tap settles the placeholder run and puts the flow's own receipt
    // up; CONTINUE is the control page it was opened from. Nothing banks here:
    // the flow owns its wallet, calendar checkpoint and receipt.
    private void SettleHaul() => ShowScreen(_haul.Settle(ControlCommand));

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
    /// delivery receipt's CONTINUE).
    /// </summary>
    public void ShowDeliveryBoard() =>
        ShowScreen(_delivery.Board ?? (object)ControlPage());

    /// <summary>
    /// The poll's page maintenance: the flash edges and the minute-guarded
    /// refreshes of the three cached pages. Warm path only — each page does one
    /// int compare per tick at worst (hot-path discipline).
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
    }

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
            _openDay, MenuCommand, _willyLedger,
            () => WhereIsWillyRules.HasCoffeeCareer(_directory, _career),
            _ignoreEventGates);
        return _calendar;
    }

    private void OpenCalendar() => ShowCalendarPage();

    /// <summary>Opens the cached calendar page (a run's receipt CONTINUE lands here).</summary>
    public void ShowCalendarPage() => ShowScreen(CalendarPage());

    private void OnDay(int day)
    {
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

        if (day == _roadTestDay)
        {
            // The road test is a drive now (PORTPLAN 49): the row sends the
            // player to La Strada and the flow owns the 0-60 leg and its result.
            // The pick is only banked as tested once the whole test (accel,
            // brake, photo) is driven, so the row keeps naming the same car.
            _roadTest.Begin();
            return;
        }

        // The Cento Miglia page (PORTPLAN 49): Unity JobSelectionScreen's
        // Sunday tap, which the calendar's own gate has already checked against
        // the day and the 8am-8pm window.
        if (day == _centoDay)
        {
            OpenCento();
            return;
        }

        // The weekly market valuation (PORTPLAN 49): the Monday row is the
        // show's job-centre button. The tap pins the venue and sends the
        // player off; the flow owns the window gate as well, so a caller can
        // never jump it.
        if (day == _valuationDay)
        {
            _valuation.BeginDrive();
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
            if (_taxi.InProgress || _delivery.InProgress || _cento.InProgress)
            {
                _log.LogInfo("touristenfahrten refused: another job owns the companion");
                return;
            }

            _touristen.Begin();
            return;
        }

        // Only placeholder rows reach this: an event whose page or leg has not
        // landed yet (Grandma's pancakes).
        _log.LogInfo($"calendar day {day} tapped: no page behind this event yet");
    }

    // The event page per visit, never cached: the reputation it prints and the
    // garage size it counts are the driver's current ones, and the route it
    // plans grows as the zone does. BACK returns to the calendar it was opened
    // from; the built event starts the race flow, which owns the drive to the
    // start, the registration check and the finish.
    private void OpenCento()
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
            return;
        }

        var facts = new CentoMigliaFacts(
            stamp.Week,
            stamp.Year,
            _wallet.ClubReputation,
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
    // and RESET GAME), so it travels through the host.
    public MenuScreenViewModel MenuPage() =>
        _menu ??= new MenuScreenViewModel(
            new RelayCommand(OpenWallet),
            ControlCommand,
            new RelayCommand(OpenCalendar),
            new RelayCommand(OpenTrunk),
            new RelayCommand(_host.ShowSettings),
            new RelayCommand(OpenHome),
            _hunger, _garage, _carBrand,
            _beat,
            _game,
            _clock,
            _day.DisplayWeek);

    // The home page (Unity YouScreen): the player's own doors, starting with
    // the garage. A fresh page per visit like the wallet, so nothing stale
    // survives between openings; BACK returns to the menu it was opened from.
    private void OpenHome() => ShowScreen(CreateHomeScreen());

    private HomeScreenViewModel CreateHomeScreen() =>
        new(GarageCommand,
            new RelayCommand(OpenAddresses),
            MenuCommand,
            _beat);

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
    /// contract without a map. The board is ranked against the new area first,
    /// so the page's standings block renders a fresh result instead of polling.
    /// </summary>
    public void ShowZoneExpansion(
        ZoneExpansion zone, IReadOnlyList<PointOfInterest> added, Action next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _zoneStandings.Refresh(zone.NewKm);
        var page = new ZoneExpansionScreenViewModel(
            zone, added, _zoneStandings, _beat,
            new RelayCommand(next),
            new RelayCommand(() =>
                ShowScreen(AddressesPage(new RelayCommand(next)))));
        ShowScreen(page);
    }

    // The trunk page (PORTPLAN 26, Unity YouScreen's TrunkScreen) is rebuilt on
    // every visit like Unity's RebuildTrunkScreen: a shop purchase made since
    // the last look must show, so the page is never cached. EAT banks the trunk
    // and the appetite together — they live in different files, and a bite
    // moves both.
    private void OpenTrunk() =>
        ShowScreen(new TrunkScreenViewModel(
            _trunk, _hunger, _garage, _stores,
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
}
