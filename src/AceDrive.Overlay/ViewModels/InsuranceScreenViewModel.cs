using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The insurance broker (PORTPLAN item 34, port of Unity InsuranceScreen): one
/// screen with the broker's landing page — MANAGE and COLLECT — and the two
/// blocks behind them. MANAGE lists every owned car with its ADD/REMOVE cover,
/// the selected car's new price and daily premium (base minus the driver
/// rating) and the garage's total daily cost; COLLECT shows what a covered
/// repair paid into the escrow and moves it to cash. BACK closes a block first
/// and only leaves the shop from the landing page, exactly like the source.
///
/// Parity: the toggle flips OwnedCar.hasInsurance and banks through the host,
/// the collection moves the escrow to cash, and the premium math is the shared
/// InsurancePricing table, so the page and the nightly charge always agree. The
/// page writes nothing itself (SRP). The Unity familiarity bump on the broker's
/// owner stays parked until the marker network (items 24/59) gives the broker an
/// owner, like the wash's.
/// Voice parity: the block's verbs act at once like a click; BACK navigates, so
/// its tap glows teal first and waits out the shared beat.
/// </summary>
public sealed class InsuranceScreenViewModel : ViewModelBase, IVoiceScreen, IPoiScreen
{
    private readonly Garage _garage;
    private readonly DriverWallet _wallet;
    private readonly InsuranceRules _rules;
    private readonly double _driverRating;
    private readonly VoiceBeat _beat;
    private readonly ICommand _backCommand;
    private readonly Action<string, bool> _onToggled;
    private readonly Action<double> _onCollected;

    private InsuranceView _view = InsuranceView.Landing;

    // Unity keeps the tapped car highlighted and paints its details under the
    // rows; a car that leaves the garage hands the selection to the first row.
    private string? _selectedKey;

    public InsuranceScreenViewModel(
        Garage garage, DriverWallet wallet, InsuranceRules rules, double driverRating,
        string storeName, VoiceBeat beat, ICommand backCommand,
        Action<string, bool> onToggled, Action<double> onCollected)
    {
        _garage = garage;
        _wallet = wallet;
        _rules = rules;
        _driverRating = driverRating;
        _beat = beat;
        _backCommand = backCommand;
        _onToggled = onToggled;
        _onCollected = onCollected;
        StoreLine = string.IsNullOrWhiteSpace(storeName)
            ? Strings.Get("InsuranceTitle")
            : storeName.ToUpperInvariant();
        SelectCommand = new ParameterizedCommand<InsuranceCarRow>(Select);
        ToggleCommand = new ParameterizedCommand<InsuranceCarRow>(Toggle);
        ManageCommand = new RelayCommand(OpenManage);
        OpenCollectCommand = new RelayCommand(OpenCollect);
        CollectItCommand = new RelayCommand(CollectIt);
        BackCommand = new RelayCommand(Back);
        Reload();
    }

    /// <summary>Unity's "_textStore": the broker's own name over the page.</summary>
    public string StoreLine { get; }

    /// <summary>Opens the cover list (Unity OnManageClicked).</summary>
    public ICommand ManageCommand { get; }

    /// <summary>Opens the escrow block (Unity OnCollectClicked).</summary>
    public ICommand OpenCollectCommand { get; }

    /// <summary>Moves the escrow to cash (Unity OnCollectItClicked).</summary>
    public ICommand CollectItCommand { get; }

    /// <summary>Highlights one car and paints its price and premium.</summary>
    public ICommand SelectCommand { get; }

    /// <summary>Flips one car's cover (Unity OnAddClicked).</summary>
    public ICommand ToggleCommand { get; }

    public ICommand BackCommand { get; }

    /// <summary>The broker's landing page is up: the two verbs and BACK.</summary>
    public bool IsLandingView
    {
        get => _isLandingView;
        private set => SetField(ref _isLandingView, value);
    }
    private bool _isLandingView = true;

    /// <summary>The cover list is up.</summary>
    public bool IsManageView
    {
        get => _isManageView;
        private set => SetField(ref _isManageView, value);
    }
    private bool _isManageView;

    /// <summary>The escrow block is up.</summary>
    public bool IsCollectView
    {
        get => _isCollectView;
        private set => SetField(ref _isCollectView, value);
    }
    private bool _isCollectView;

    public IReadOnlyList<InsuranceCarRow> Rows
    {
        get => _rows;
        private set => SetField(ref _rows, value);
    }
    private IReadOnlyList<InsuranceCarRow> _rows = [];

    /// <summary>A garage with no car at all: the hint keeps the page honest.</summary>
    public bool IsEmpty => Rows.Count == 0;

    public string EmptyLine { get; } = Strings.Get("InsuranceEmpty");

    /// <summary>Unity's "_textValueCar": the selected car's new price.</summary>
    public string ValueCarLine
    {
        get => _valueCarLine;
        private set => SetField(ref _valueCarLine, value);
    }
    private string _valueCarLine = "";

    /// <summary>Unity's "_textInsuranceCost": the selected car's daily premium.</summary>
    public string DailyCostLine
    {
        get => _dailyCostLine;
        private set => SetField(ref _dailyCostLine, value);
    }
    private string _dailyCostLine = "";

    /// <summary>Unity's "_textInsuranceCostTotal": the combined daily premium.</summary>
    public string TotalLine
    {
        get => _totalLine;
        private set => SetField(ref _totalLine, value);
    }
    private string _totalLine = "";

    /// <summary>Unity's "_textTotalRepairCost": the bill behind the escrow.</summary>
    public string CollectRepairLine
    {
        get => _collectRepairLine;
        private set => SetField(ref _collectRepairLine, value);
    }
    private string _collectRepairLine = "";

    /// <summary>Unity's "_textToCollect": what the insurance pays out.</summary>
    public string CollectPayLine
    {
        get => _collectPayLine;
        private set => SetField(ref _collectPayLine, value);
    }
    private string _collectPayLine = "";

    /// <summary>Unity's "_textTotalCash": the cash after the transfer.</summary>
    public string CollectCashLine
    {
        get => _collectCashLine;
        private set => SetField(ref _collectCashLine, value);
    }
    private string _collectCashLine = "";

    /// <summary>An empty escrow has nothing to collect: the verb goes inert.</summary>
    public bool CanCollect
    {
        get => _canCollect;
        private set => SetField(ref _canCollect, value);
    }
    private bool _canCollect;

    /// <summary>
    /// The COLLECT IT verb is part of the escrow block, not the page: it only
    /// shows once COLLECT was clicked (Unity's button lives in the block), and
    /// only while the escrow holds something to move.
    /// </summary>
    public bool ShowCollectIt => IsCollectView && CanCollect;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash
    {
        get => _backFlash;
        private set => SetField(ref _backFlash, value);
    }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // Unity OnManageClicked: the landing verbs step aside and the rows build.
    private void OpenManage()
    {
        ClearBackFlash();
        Reload();
        SetView(InsuranceView.Manage);
    }

    // Unity OnCollectClicked: the escrow block opens on live numbers.
    private void OpenCollect()
    {
        ClearBackFlash();
        Reload();
        SetView(InsuranceView.Collect);
    }

    // Unity OnCollectItClicked/CollectPayout: the escrow moves to cash and the
    // screen resets to its landing page with the verb gone.
    private void CollectIt()
    {
        ClearBackFlash();
        double moved = _wallet.CollectInsurancePayout();
        if (moved <= 0.0)
        {
            return;
        }

        _onCollected(moved);
        Reload();
        SetView(InsuranceView.Landing);
    }

    // Unity OnCarNameClicked: the tapped car owns the detail block.
    private void Select(InsuranceCarRow? row)
    {
        if (row is null)
        {
            return;
        }

        _selectedKey = row.Key;
        Reload();
    }

    // Unity OnAddClicked: flips the cover and banks (Unity SaveManager.Save);
    // the page repaints from the same rule the nightly charge reads.
    private void Toggle(InsuranceCarRow? row)
    {
        if (row is null || _garage.Find(row.Key) is not { } car)
        {
            return;
        }

        car.SetInsurance(!car.HasInsurance);
        _selectedKey = row.Key;
        Reload();
        _onToggled(row.Key, car.HasInsurance);
    }

    // Unity OnBackClicked: a block closes before the shop does.
    private void Back()
    {
        ClearBackFlash();
        if (_view != InsuranceView.Landing)
        {
            SetView(InsuranceView.Landing);
            return;
        }

        _backCommand.Execute(null);
    }

    private void SetView(InsuranceView view)
    {
        _view = view;
        IsLandingView = view == InsuranceView.Landing;
        IsManageView = view == InsuranceView.Manage;
        IsCollectView = view == InsuranceView.Collect;
        OnPropertyChanged(nameof(ShowCollectIt));
        RebuildVoice();
    }

    private void Reload()
    {
        OwnedCar? chosen = ChosenCar();
        var rows = new List<InsuranceCarRow>(_garage.Cars.Count);
        double total = 0.0;
        foreach (OwnedCar car in _garage.Cars)
        {
            CarSpec? spec = _garage.SpecOf(car);
            if (car.HasInsurance)
            {
                total += DailyCostOf(car, spec);
            }
            rows.Add(new InsuranceCarRow(
                car.Key,
                CarTitles.Of(car.Key, spec),
                Strings.Get(car.HasInsurance ? "InsuranceRemove" : "InsuranceAdd"),
                car.HasInsurance,
                string.Equals(car.Key, chosen?.Key, StringComparison.Ordinal)));
        }

        Rows = rows;
        PaintSelected(chosen);
        TotalLine = Strings.Format("InsuranceTotal", total);
        PaintCollect();
        OnPropertyChanged(nameof(IsEmpty));
        RebuildVoice();
    }

    // Unity opens the manage block on the first car and keeps the tapped one;
    // a car that left the garage hands the selection to the new first row.
    private OwnedCar? ChosenCar()
    {
        if (_selectedKey is { } key && _garage.Find(key) is { } kept)
        {
            return kept;
        }

        _selectedKey = _garage.Cars.Count > 0 ? _garage.Cars[0].Key : null;
        return _selectedKey is null ? null : _garage.Find(_selectedKey);
    }

    private double DailyCostOf(OwnedCar car, CarSpec? spec) =>
        spec is { Price: > 0.0 }
            ? InsurancePricing.DiscountedDailyCost(spec.Price, _driverRating, _rules)
            : _rules.MinimumDailyCost;

    // Unity OnCarNameClicked's detail block: the price every car has, and the
    // premium only a covered one costs (CR0.00/day while uninsured, like Unity).
    private void PaintSelected(OwnedCar? car)
    {
        if (car is null || _garage.SpecOf(car) is not { } spec)
        {
            ValueCarLine = string.Empty;
            DailyCostLine = string.Empty;
            return;
        }

        ValueCarLine = Strings.Format("InsuranceCarValue", spec.Price);
        if (!car.HasInsurance || spec.Price <= 0.0)
        {
            DailyCostLine = Strings.Format("InsuranceDailyCar", 0.0);
            return;
        }

        // Unity prints the base and the discount beside the discounted cost, so
        // the player sees where the number comes from.
        DailyCostLine = Strings.Format(
                "InsuranceDailyCar",
                InsurancePricing.DiscountedDailyCost(spec.Price, _driverRating, _rules)) +
            Strings.Format(
                "InsuranceDailyDiscount",
                InsurancePricing.BaseDailyCost(spec.Price, _rules),
                _driverRating);
    }

    // Unity OnCollectClicked's three lines: the bill the escrow came from (the
    // escrow is the payout share of it), what will be paid, and the cash after.
    private void PaintCollect()
    {
        double pending = _wallet.PendingInsurance;
        CanCollect = pending > 0.0;
        OnPropertyChanged(nameof(ShowCollectIt));
        CollectRepairLine = Strings.Format(
            "InsuranceCollectRepair", pending / InsuranceRules.RepairPayoutFraction);
        CollectPayLine = Strings.Format("InsuranceCollectPay", pending);
        CollectCashLine = Strings.Format("InsuranceCollectCash", _wallet.Cash + pending);
    }

    // The face the page shows owns the words the mic may answer: the landing
    // verbs, the rows and their verb while the cover list is up, COLLECT IT
    // while there is an escrow. BACK glows first and acts at the beat's end.
    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>(Rows.Count + 3);
        switch (_view)
        {
            case InsuranceView.Landing:
                actions[Strings.Get("InsuranceManage")] = OpenManage;
                actions[Strings.Get("InsuranceCollect")] = OpenCollect;
                break;

            case InsuranceView.Manage:
                foreach (InsuranceCarRow row in Rows)
                {
                    InsuranceCarRow hit = row;
                    actions[hit.Title] = () => Select(hit);
                }
                // The one ADD/REMOVE label on the page belongs to the selected
                // car, so a voice player toggles it without reaching for the row.
                if (Rows.FirstOrDefault(row => row.IsSelected) is { } selected)
                {
                    InsuranceCarRow hit = selected;
                    actions[selected.ToggleLabel] = () => Toggle(hit);
                }
                break;

            case InsuranceView.Collect:
                if (CanCollect)
                {
                    actions[Strings.Get("InsuranceCollectIt")] = CollectIt;
                }
                break;
        }

        actions[Strings.Get("DeliveryBack")] = () =>
        {
            BackFlash = true;
            _beat.Defer(Back);
        };
        VoiceActions = actions;
    }

    private void ClearBackFlash() => BackFlash = false;
}
