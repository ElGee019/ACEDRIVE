using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Delivery career page (PORTPLAN items 16-19, port of Unity
/// DeliveryJobScreen + StoreListView): stats header, the restockable store
/// list and the phase-flipping main button. A row tap confirms a
/// full-restock order into the pickup cart and flips the page between
/// OPEN NEW CAREER and DELIVER; the back button becomes CANCEL ORDER while
/// the cart holds orders, mirroring the Unity screen. Only stores whose
/// wholesale career has been opened show on the board (Unity's
/// HasHubForCategory eligibility), so the board starts empty and the hub
/// page fills it. Per-item quantities arrive with the order screen (item
/// 17); the run crosses to the map, so both actions come in as
/// injected commands (composition, no IScreenController growth). DELIVER
/// freezes the cart's money on the live shelves and hands the run to the
/// host, which drives the hub pickups and store drop-offs on the companion
/// and settles the receipt after the last stop (the taxi ACCEPT hand-over's
/// delivery twin); the shelves are filled at each stop, never on this tap.
/// Voice parity with the control page: every button answers to its text.
/// </summary>
public sealed class DeliveryJobScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    private DeliveryCatalog _catalog;
    private readonly DeliveryCareer _career;
    private readonly CityEconomy _city;
    private readonly DeliveryCart _cart = new();
    private readonly ILog _log;
    private readonly VoiceBeat _beat;
    private readonly ICommand _backCommand;
    private readonly ParameterizedCommand<DeliveryRun> _runCommand;
    private readonly ICommand _hubCommand;

    public DeliveryJobScreenViewModel(
        DeliveryCatalog catalog, DeliveryCareer career, CityEconomy city, ILog log,
        ICommand backCommand, ParameterizedCommand<DeliveryRun> runCommand, ICommand hubCommand,
        VoiceBeat beat)
    {
        _catalog = catalog;
        _career = career;
        _city = city;
        _log = log;
        _beat = beat;
        _backCommand = backCommand;
        _runCommand = runCommand;
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

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>Total wholesale money committed by the cart (for logs/tests;
    /// the escrow wallet itself lands with the save, PORTPLAN 51).</summary>
    public double CartTotal => _cart.TotalCost(_catalog, _city);

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
            _log.LogInfo($"order skipped, fully stocked: {row.Store.Name}");
            return;
        }
        string key = row.Store.Key;
        bool confirmed = !_cart.Contains(key);
        if (confirmed) _cart.Confirm(key);
        else _cart.Remove(key);
        _log.LogInfo($"{(confirmed ? "order confirmed" : "order dropped")}: " +
            $"{row.Store.Name} €{_catalog.OrderCost(row.Store):0.00}");
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
        // The run leaves this page: the host takes over the drive, pins the
        // first hub/store leg on the companion and settles the receipt after
        // the last stop (the taxi ACCEPT hand-over's delivery twin).
        StartRun();
    }

    // The ordered stores are the delivered ones: a full store takes no order,
    // so the cart never holds a shelf that does not need the drop-off. The
    // money is frozen here on the live pre-run shelves and handed to the host;
    // filling the shelves is CityEconomy's rule at each confirmed stop, never
    // this page's job. Clearing the cart reflects that it became a live run —
    // the escrow ledger that pre-books the debit at confirm lands with the
    // save (PORTPLAN 51).
    private void StartRun()
    {
        DeliveryRun? run = _cart.Run(_catalog, _city);
        if (run is null)
        {
            return;
        }

        _log.LogInfo($"delivery run started: €{run.PaidForGoods:0.00} -> " +
            $"{string.Join(", ", _cart.ConfirmedKeys.Select(NameOf))}");
        _cart.Clear();
        RebuildRows();
        _runCommand.Execute(run);
    }

    // A cart key always has a catalog row (a row tap is the only way in);
    // falling back to the key keeps the log line whole if content changed
    // under a staged cart.
    private string NameOf(string key) => _catalog.Find(key)?.Name ?? key;

    private void OnBack()
    {
        ClearVoiceFlash();
        if (!_cart.IsEmpty)
        {
            // Unity: leaving with a pending cart is CANCEL ORDER, which
            // refunds the escrow first. No wallet yet: clearing the cart is
            // the whole refund until the save lands (PORTPLAN 51).
            _log.LogInfo($"orders cancelled: €{_cart.TotalCost(_catalog, _city):0.00} refunded");
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
        foreach (string key in _cart.ConfirmedKeys)
        {
            DeliveryStore? store = _catalog.Find(key);
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

    private DeliveryRow BuildRow(DeliveryStore store, bool confirmed, int position) => new(
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
        confirmed ? Strings.Format("DeliveryConfirmed", _catalog.OrderCost(store)) : string.Empty);

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
