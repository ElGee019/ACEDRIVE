using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>One store row, preformatted: the view stays dumb (Unity StoreListView).</summary>
public sealed record DeliveryRow(
    DeliveryStore Store,
    string NameLine,
    string OwnerLine,
    string UnitsLine,
    string GrowthLine,
    string PercentLine,
    double Percent,
    StockLevel Level,
    bool IsUrgent,
    bool IsConfirmed,
    string ConfirmedLine);

/// <summary>
/// Delivery career page (PORTPLAN items 16-19, port of Unity
/// DeliveryJobScreen + StoreListView): stats header, the restockable store
/// list and the phase-flipping main button. A row tap confirms a
/// full-restock order into the pickup cart and flips the page between
/// OPEN NEW CAREER and DELIVER; the back button becomes CANCEL ORDER while
/// the cart holds orders, mirroring the Unity screen. Per-item quantities
/// arrive with the order screen (item 17), hub purchase with item 18, the
/// pickup run with the map — those actions cross screens and so come in as
/// injected commands (composition, no IScreenController growth).
/// Voice parity with the control page: every button answers to its text.
/// </summary>
public sealed class DeliveryJobScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    private readonly DeliveryCatalog _catalog;
    private readonly DeliveryCart _cart = new();
    private readonly ILog _log;
    private readonly ICommand _backCommand;
    private readonly ICommand _runCommand;
    private readonly ICommand _hubCommand;

    public DeliveryJobScreenViewModel(
        DeliveryCatalog catalog, ILog log,
        ICommand backCommand, ICommand runCommand, ICommand hubCommand)
    {
        _catalog = catalog;
        _log = log;
        _backCommand = backCommand;
        _runCommand = runCommand;
        _hubCommand = hubCommand;

        // Max's intro gift is the only CredP that exists until the persistent
        // save lands (PORTPLAN 5/51): the number rides the catalog as data.
        CredPLine = Strings.Format("DeliveryCredP", catalog.StartCredP);
        CitizensLine = Strings.Format("DeliveryCitizens", catalog.TotalCitizens);
        CityGrowthLine = GrowthHeadline(catalog.CityGrowthPerHour);

        ToggleOrderCommand = new ParameterizedCommand<DeliveryRow>(ToggleOrder);
        MainButtonCommand = new RelayCommand(OnMainButton);
        BackCommand = new RelayCommand(OnBack);
        RebuildRows();
    }

    public string CredPLine { get; }
    public string CitizensLine { get; }
    public string CityGrowthLine { get; }

    public IReadOnlyList<DeliveryRow> Rows { get => _rows; private set => SetField(ref _rows, value); }
    private IReadOnlyList<DeliveryRow> _rows = [];

    /// <summary>Unity's empty board copy; true once a hub career connects stock.</summary>
    public bool HasStores { get => _hasStores; private set => SetField(ref _hasStores, value); }
    private bool _hasStores;

    public string MainButtonLabel { get => _mainButtonLabel; private set => SetField(ref _mainButtonLabel, value); }
    private string _mainButtonLabel = "";

    public string BackButtonLabel { get => _backButtonLabel; private set => SetField(ref _backButtonLabel, value); }
    private string _backButtonLabel = "";

    public ICommand ToggleOrderCommand { get; }
    public ICommand MainButtonCommand { get; }
    public ICommand BackCommand { get; }

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>Total wholesale money committed by the cart (for logs/tests;
    /// the escrow wallet itself lands with the save, PORTPLAN 51).</summary>
    public double CartTotal => _cart.TotalCost(_catalog);

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
        // Phase flip only routes; what each phase opens lives behind the
        // injected commands so this page never grows map/hub branches.
        (_cart.Phase == DeliveryPhase.OpenCareer ? _hubCommand : _runCommand).Execute(null);
    }

    private void OnBack()
    {
        if (!_cart.IsEmpty)
        {
            // Unity: leaving with a pending cart is CANCEL ORDER, which
            // refunds the escrow first. No wallet yet: clearing the cart is
            // the whole refund until the save lands (PORTPLAN 51).
            _log.LogInfo($"orders cancelled: €{_cart.TotalCost(_catalog):0.00} refunded");
            _cart.Clear();
            RebuildRows();
        }
        _backCommand.Execute(null);
    }

    // Confirmed rows first (cart order, like Unity's staged list), then the
    // rest by stock percentage ascending: emptiest stores need help most.
    private void RebuildRows()
    {
        var rows = new List<DeliveryRow>(_catalog.Stores.Count);
        foreach (string key in _cart.ConfirmedKeys)
        {
            DeliveryStore? store = _catalog.Find(key);
            if (store is not null) rows.Add(BuildRow(store, confirmed: true));
        }
        rows.AddRange(_catalog.Stores
            .Where(s => !_cart.Contains(s.Key))
            .OrderBy(s => s.StockPercent)
            .Select(s => BuildRow(s, confirmed: false)));
        Rows = rows;
        HasStores = _catalog.Stores.Count > 0;
        bool deliver = _cart.Phase == DeliveryPhase.Deliver;
        MainButtonLabel = Strings.Get(deliver ? "DeliveryDeliver" : "DeliveryOpenCareer");
        BackButtonLabel = Strings.Get(deliver ? "DeliveryCancelOrder" : "DeliveryBack");
        RebuildVoice();
    }

    private DeliveryRow BuildRow(DeliveryStore store, bool confirmed) => new(
        store,
        store.Name,
        store.Owner.ToUpperInvariant(),
        Strings.Format("DeliveryUnits", store.StockedUnits, store.UnitCapacity),
        StoreGrowthLine(store.GrowthPerHour),
        Strings.Format("DeliveryPercent", store.StockPercent),
        store.StockPercent,
        store.Level,
        store.IsUrgent && !confirmed,
        confirmed,
        confirmed ? Strings.Format("DeliveryConfirmed", _catalog.OrderCost(store)) : string.Empty);

    // Unity StoreManager display mapping: ▲ +n / ▼ -n / ▬ 0, as data sign ->
    // three resx variants so translators own the copy.
    private static string GrowthHeadline(int growthPerHour) => growthPerHour switch
    {
        > 0 => Strings.Format("DeliveryGrowthUp", growthPerHour),
        < 0 => Strings.Format("DeliveryGrowthDown", growthPerHour),
        _ => Strings.Get("DeliveryGrowthFlat"),
    };

    private static string StoreGrowthLine(int growthPerHour) => growthPerHour switch
    {
        > 0 => Strings.Format("DeliveryStoreGrowthUp", growthPerHour),
        < 0 => Strings.Format("DeliveryStoreGrowthDown", growthPerHour),
        _ => Strings.Get("DeliveryStoreGrowthFlat"),
    };

    private void RebuildVoice()
    {
        // Both phase names answer in either state (taxi shift-label
        // precedent): the shown label trails the state by the 1 s glow, and
        // one shared delegate per button keeps the caller's mute working.
        Action main = OnMainButton;
        Action back = OnBack;
        var actions = new Dictionary<string, Action>
        {
            [Strings.Get("DeliveryOpenCareer")] = main,
            [Strings.Get("DeliveryDeliver")] = main,
            [Strings.Get("DeliveryBack")] = back,
            [Strings.Get("DeliveryCancelOrder")] = back,
        };
        foreach (DeliveryRow row in Rows)
        {
            DeliveryRow hit = row;
            actions[hit.NameLine] = () => ToggleOrder(hit);
        }
        VoiceActions = actions;
    }
}
