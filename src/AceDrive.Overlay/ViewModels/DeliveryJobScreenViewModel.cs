using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Delivery career page (PORTPLAN items 16-19, port of Unity
/// DeliveryJobScreen + StoreListView): stats header, the restockable store
/// list and the phase-flipping main button. A row tap confirms an order for
/// as many of the store's missing units as the player's cash and the car's
/// free trunk litres still cover (both minus what the cart already
/// committed), then flips the page between OPEN NEW CAREER and DELIVER; the
/// units left over stay on the shelf for a later run. A tap on a store that
/// is already full is refused with a three-second notice naming it, and so
/// is a tap that cannot afford — or cannot fit — a single unit. The back
/// button becomes CANCEL ORDER while the cart holds orders, mirroring the
/// Unity screen. Only stores whose wholesale career has been opened show on
/// the board (Unity's HasHubForCategory eligibility), so the board starts
/// empty and the hub page fills it. The run crosses to the map, so both
/// actions come in as injected commands (composition, no IScreenController
/// growth). DELIVER re-checks the trunk, charges the goods and hands the run
/// to the host, which drives the hub pickups and store drop-offs on the
/// companion and settles the receipt after the last stop; each store arrival
/// raises only the ordered units, never the whole shelf. The shelves are
/// filled at each stop, never on this tap. Voice parity with the control
/// page: every button answers to its text.
/// </summary>
public sealed class DeliveryJobScreenViewModel : ViewModelBase, IVoiceScreen
{
    // Unity order-screen rule: a fully stocked store takes no order, and a
    // store the player cannot afford takes none either. The refusal names the
    // store for three real seconds, like the control page's COMING SOON hold,
    // so the tap is never silent.
    private const double NoticeSeconds = 3;
    private const string FullyStockedKey = "DeliveryFullyStocked";
    private const string NoFundsKey = "DeliveryNoFunds";
    private const string NoSpaceKey = "DeliveryNoSpace";
    private const string RunUnfundedKey = "DeliveryNoRunFunds";
    private const string RunNoSpaceKey = "DeliveryRunNoSpace";

    private DeliveryCatalog _catalog;
    private readonly DeliveryCareer _career;
    private readonly CityEconomy _city;
    private readonly DriverWallet _wallet;
    private readonly Trunk _trunk;
    private readonly Func<double> _trunkCapacity;
    private readonly DeliveryCart _cart = new();
    private readonly ILog _log;
    private readonly IClock _clock;
    private readonly VoiceBeat _beat;
    private readonly ICommand _backCommand;
    private readonly Func<DeliveryRun, bool> _startRun;
    private readonly ICommand _hubCommand;

    public DeliveryJobScreenViewModel(
        DeliveryCatalog catalog, DeliveryCareer career, CityEconomy city, DriverWallet wallet,
        Trunk trunk, Func<double> trunkCapacity,
        ILog log, ICommand backCommand, Func<DeliveryRun, bool> startRun, ICommand hubCommand,
        IClock clock, VoiceBeat beat)
    {
        _catalog = catalog;
        _career = career;
        _city = city;
        _wallet = wallet;
        _trunk = trunk;
        _trunkCapacity = trunkCapacity;
        _log = log;
        _clock = clock;
        _beat = beat;
        _backCommand = backCommand;
        _startRun = startRun;
        _hubCommand = hubCommand;

        ToggleOrderCommand = new ParameterizedCommand<DeliveryRow>(ToggleOrder);
        MainButtonCommand = new RelayCommand(OnMainButton);
        BackCommand = new RelayCommand(OnBack);
        RebuildRows();
    }

    public string CredPLine { get => _credPLine; private set => SetField(ref _credPLine, value); }
    private string _credPLine = "";

    public string CitizensLine { get => _citizensLine; private set => SetField(ref _citizensLine, value); }
    private string _citizensLine = "";

    public string CityGrowthLine { get => _cityGrowthLine; private set => SetField(ref _cityGrowthLine, value); }
    private string _cityGrowthLine = "";

    public IReadOnlyList<DeliveryRow> Rows { get => _rows; private set => SetField(ref _rows, value); }
    private IReadOnlyList<DeliveryRow> _rows = [];

    /// <summary>Unity's empty board copy; true once a hub career connects stock.</summary>
    public bool HasStores { get => _hasStores; private set => SetField(ref _hasStores, value); }
    private bool _hasStores;

    /// <summary>Points a fresh career at Max's grocery: no CredP and no hub
    /// bought, so OPEN NEW CAREER has nothing to spend.</summary>
    public bool ShowFirstCredPHint { get => _showFirstCredPHint; private set => SetField(ref _showFirstCredPHint, value); }
    private bool _showFirstCredPHint;

    public string MainButtonLabel { get => _mainButtonLabel; private set => SetField(ref _mainButtonLabel, value); }
    private string _mainButtonLabel = "";

    public string BackButtonLabel { get => _backButtonLabel; private set => SetField(ref _backButtonLabel, value); }
    private string _backButtonLabel = "";

    public ICommand ToggleOrderCommand { get; }
    public ICommand MainButtonCommand { get; }
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool MainFlash { get => _mainFlash; private set => SetField(ref _mainFlash, value); }
    private bool _mainFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per button.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    /// <summary>Copy naming the event that refused the tap (a full store or an
    /// unaffordable one); empty the rest of the time, so the view collapses
    /// the line.</summary>
    public string NoticeLine { get => _noticeLine; private set => SetField(ref _noticeLine, value); }
    private string _noticeLine = string.Empty;

    // The refusal hold's deadline. Latest tap wins (a second refusal restarts
    // it), like the control page's COMING SOON hold.
    private DateTime _noticeUntil;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>Wholesale money the cart has already earmarked (for logs/tests;
    /// DELIVER charges it and the receipt nets it out).</summary>
    public double CartTotal => _cart.TotalCost;

    /// <summary>Unity re-shows this screen after the hub page closes, so it
    /// re-pulls career state (spent CredP, newly connected stores). This
    /// page is cached across navigation, hence the explicit refresh. A placed
    /// store appends a board row, so the host passes the grown catalogue too.</summary>
    public void Refresh(DeliveryCatalog? catalog = null)
    {
        if (catalog is not null)
        {
            _catalog = catalog;
        }

        RebuildRows();
    }

    private void ToggleOrder(DeliveryRow? row)
    {
        if (row is null) return;
        if (row.Store.UnitsNeeded == 0)
        {
            // Unity order-screen rule: a fully stocked store takes no order.
            // The refused tap is answered, not just logged: the page names
            // the store for three seconds.
            ShowNotice(FullyStockedKey, row.Store.Name);
            _log.LogInfo($"order skipped, fully stocked: {row.Store.Name}");
            return;
        }
        // A tap that stages or drops an order is the player moving on, so an
        // older refusal must not linger beside it.
        ClearNotice();
        string key = row.Store.Key;
        if (_cart.Contains(key))
        {
            _cart.Remove(key);
            _log.LogInfo($"order dropped: {row.Store.Name}");
            RebuildRows();
            return;
        }

        // The cart may only earmark resources that are still free: earlier
        // orders already hold their wholesale sum and their trunk litres, so
        // this one gets the remainder of both. The lower cap wins.
        double cashBudget = _wallet.Cash - _cart.TotalCost;
        double litreBudget = _trunk.Remaining(_trunkCapacity()) - _cart.TotalVolume;
        int byCash = _catalog.AffordableUnits(row.Store, cashBudget);
        int bySpace = DeliveryCatalog.AffordableUnitsByVolume(row.Store, litreBudget);
        int units = Math.Min(byCash, bySpace);
        if (units <= 0)
        {
            ShowNotice(byCash <= 0 ? NoFundsKey : NoSpaceKey, row.Store.Name);
            _log.LogInfo(byCash <= 0
                ? $"order skipped, not enough cash: {row.Store.Name} " +
                  $"needs €{_catalog.OrderCost(row.Store, 1):0.00} per unit"
                : $"order skipped, trunk too full: {row.Store.Name} " +
                  $"needs {row.Store.LitresPerUnit:0.###} L per unit, " +
                  $"{litreBudget:0.###} L free");
            return;
        }

        DeliveryOrder order = _catalog.Order(row.Store, units);
        _cart.Confirm(order);
        _log.LogInfo($"order confirmed: {row.Store.Name} " +
            $"{units}/{row.Store.UnitsNeeded} units €{order.WholesaleCost:0.00} " +
            $"({order.Volume:0.###} L)");
        RebuildRows();
    }

    private void OnMainButton()
    {
        // Any action on this button ends a pending voice glow (click after
        // a voice tap, or the deferred tap itself): no stale teal on revisit.
        ClearVoiceFlash();
        // Phase flip only routes; what each phase opens lives behind the
        // injected commands so this page never grows map/hub branches.
        if (_cart.Phase == DeliveryPhase.OpenCareer)
        {
            _hubCommand.Execute(null);
            return;
        }
        // The run leaves this page: the host charges the goods, takes over the
        // drive, pins the first hub/store leg and settles the receipt after
        // the last stop (the taxi ACCEPT hand-over's delivery twin).
        StartRun();
    }

    // The ordered stores are the delivered ones: a full store takes no order,
    // so the cart never holds a shelf that does not need the drop-off. The
    // host charges the run's wholesale sum before the trip risk and fills
    // each shelf by the ordered units at each confirmed stop. Clearing the
    // cart reflects that it became a live run; a refused start keeps it so
    // the player can drop an order and try again.
    private void StartRun()
    {
        DeliveryRun? run = _cart.Run();
        if (run is null)
        {
            return;
        }

        // The trunk could have changed since the lines were staged (cargo
        // eaten, a keepsake dropped in, another car selected): never start a
        // run the car cannot physically carry.
        if (!run.FitsIn(_trunk.Remaining(_trunkCapacity())))
        {
            ShowNotice(RunNoSpaceKey);
            return;
        }

        if (!_startRun(run))
        {
            ShowNotice(RunUnfundedKey);
            return;
        }
        _log.LogInfo($"delivery run started: €{run.PaidForGoods:0.00} -> " +
            $"{string.Join(", ", _cart.Orders.Select(OrderName))}");
        _cart.Clear();
        RebuildRows();
    }

    // An order line always has a catalog row (a row tap is the only way in);
    // falling back to the key keeps the log line whole if content changed
    // under a staged cart.
    private string OrderName(DeliveryOrder order) =>
        _catalog.Find(order.StoreKey)?.Name ?? order.StoreKey;

    private void OnBack()
    {
        ClearVoiceFlash();
        // A cached page must not re-open mid-hold: the way out drops the
        // refusal copy, like the control page's COMING SOON label.
        ClearNotice();
        if (!_cart.IsEmpty)
        {
            // Nothing was charged yet (the goods leave cash at DELIVER), so
            // cancelling only releases the cart's earmarked sum.
            _log.LogInfo($"orders cancelled: €{_cart.TotalCost:0.00} released");
            _cart.Clear();
            RebuildRows();
        }
        _backCommand.Execute(null);
    }

    // Navigating voice taps glow first, then act at the end of the shared
    // beat so the teal acknowledgment shows before the flip. The actions
    // clear the glow when they run, so a click during a beat (or the beat
    // itself) never leaves a stale teal on this cached page.
    private void FlashThen(string target, Action act)
    {
        MainFlash = target == "main";
        BackFlash = target == "back";
        _beat.Defer(act);
    }

    private void ClearVoiceFlash()
    {
        MainFlash = false;
        BackFlash = false;
    }

    /// <summary>
    /// Clears the expired refusal notice. Called from the 10 Hz poll (a
    /// refresh guard would swallow a same-second expiry): one length check per
    /// tick while idle, one DateTime compare while it shows, allocations only
    /// on the edge.
    /// </summary>
    public void UpdateNotice()
    {
        if (NoticeLine.Length > 0 && _clock.UtcNow >= _noticeUntil)
        {
            ClearNotice();
        }
    }

    // The refusal slot: a resx sentence (with the store's own name where the
    // copy has a slot), shown for three real seconds. The deadline is stamped
    // once; the poll only compares.
    private void ShowNotice(string key, string? storeName = null)
    {
        NoticeLine = storeName is null
            ? Strings.Get(key)
            : Strings.Format(key, storeName);
        _noticeUntil = _clock.UtcNow + TimeSpan.FromSeconds(NoticeSeconds);
    }

    private void ClearNotice() => NoticeLine = string.Empty;

    // Confirmed rows first (cart order, like Unity's staged list), then the
    // rest by stock percentage ascending: emptiest stores need help most.
    // Stores whose wholesale career is not open yet stay off the board
    // (Unity eligibility rule) — buying a hub on the page behind the main
    // button is what fills it. Header and rows read the town's live citizens
    // and shelves (CityEconomy, PORTPLAN 25), not the printed content.
    private void RebuildRows()
    {
        IReadOnlyList<DeliveryStore> visible = _city.Live(_career.VisibleStores(_catalog));
        CredPLine = Strings.Format("DeliveryCredP", _career.CredP);
        CitizensLine = Strings.Format("DeliveryCitizens", _city.TotalCitizens(visible));
        CityGrowthLine = DeliveryCopy.CityGrowth(_city.TotalGrowthPerHour(visible));

        var rows = new List<DeliveryRow>(visible.Count);
        int position = 1;
        foreach (DeliveryOrder order in _cart.Orders)
        {
            DeliveryStore? store = _catalog.Find(order.StoreKey);
            if (store is null || !_career.Connects(store)) continue;
            rows.Add(BuildRow(_city.Live(store), confirmed: true, position++));
        }
        foreach (DeliveryStore store in visible
            .Where(s => !_cart.Contains(s.Key))
            .OrderBy(s => s.StockPercent))
        {
            rows.Add(BuildRow(store, confirmed: false, position++));
        }
        Rows = rows;
        HasStores = rows.Count > 0;
        ShowFirstCredPHint = _career.NeedsIntroCredP;
        bool deliver = _cart.Phase == DeliveryPhase.Deliver;
        MainButtonLabel = Strings.Get(deliver ? "DeliveryDeliver" : "DeliveryOpenCareer");
        BackButtonLabel = Strings.Get(deliver ? "DeliveryCancelOrder" : "DeliveryBack");
        RebuildVoice();
    }

    private DeliveryRow BuildRow(DeliveryStore store, bool confirmed, int position)
    {
        DeliveryOrder? order = confirmed ? _cart.Find(store.Key) : null;
        return new(
            store,
            NumberWord(position),
            store.Name,
            store.Owner.ToUpperInvariant(),
            Strings.Format("DeliveryUnits", store.StockedUnits, store.UnitCapacity),
            StoreGrowthLine(_city.GrowthPerHour(store)),
            Strings.Format("DeliveryPercent", store.StockPercent),
            store.StockPercent,
            store.Level,
            store.IsUrgent && !confirmed,
            confirmed,
            order is null ? string.Empty : ConfirmedLine(store, order));
    }

    // A budget-capped line names the units it bought so the player can see
    // what stayed on the shelf; a full line keeps the shorter copy.
    private static string ConfirmedLine(DeliveryStore store, DeliveryOrder order) =>
        order.Units < store.UnitsNeeded
            ? Strings.Format(
                "DeliveryConfirmedPartial", order.Units, store.UnitsNeeded, order.WholesaleCost)
            : Strings.Format("DeliveryConfirmed", order.WholesaleCost);

    // One resx key per row position: translators own the spoken word. A
    // catalog grown past the last key degrades to plain digits rather than
    // showing a raw resource key.
    private static readonly string[] NumberWordKeys =
    [
        "DeliveryNumber1", "DeliveryNumber2", "DeliveryNumber3", "DeliveryNumber4",
    ];

    private static string NumberWord(int position) =>
        position <= NumberWordKeys.Length
            ? Strings.Get(NumberWordKeys[position - 1])
            : position.ToString(CultureInfo.CurrentCulture);

    // Unity StoreManager display mapping for a store row: ▲ +n / ▼ -n / ▬ 0
    // as three resx variants so translators own the copy. The city headline
    // shares the same rule and lives in DeliveryCopy.
    private static string StoreGrowthLine(int growthPerHour) => growthPerHour switch
    {
        > 0 => Strings.Format("DeliveryStoreGrowthUp", growthPerHour),
        < 0 => Strings.Format("DeliveryStoreGrowthDown", growthPerHour),
        _ => Strings.Get("DeliveryStoreGrowthFlat"),
    };

    private void RebuildVoice()
    {
        // Both phase names answer in either state (taxi shift-label
        // precedent): the shown label trails the state by the glow, and
        // one shared delegate per button keeps the caller's mute working.
        // Main and back navigate, so a voice tap flashes teal first and the
        // page waits out the shared beat; row taps stay on this page and act
        // at once like a click.
        Action main = () => FlashThen("main", OnMainButton);
        Action back = () => FlashThen("back", OnBack);
        var actions = new Dictionary<string, Action>
        {
            [Strings.Get("DeliveryOpenCareer")] = main,
            [Strings.Get("DeliveryDeliver")] = main,
            [Strings.Get("DeliveryBack")] = back,
            [Strings.Get("DeliveryCancelOrder")] = back,
            // One word fires what prints "CANCEL ORDER" (the user's request,
            // taxi keywords' precedent): the general matcher stays strict —
            // a lone word answers only where a page registers it on purpose.
            // With an empty cart this same delegate is plain BACK; accepted
            // because unconditional keys beat conditional ones, exactly why
            // the phase names above answer in either state.
            ["cancel"] = back,
        };
        foreach (DeliveryRow row in Rows)
        {
            DeliveryRow hit = row;
            actions[hit.NameLine] = () => ToggleOrder(hit);
            // The number button is the row's button now: "ONE" triggers the
            // top store, exactly like clicking its ONE button.
            actions[hit.NumberWord] = () => ToggleOrder(hit);
        }
        VoiceActions = actions;
    }
}
