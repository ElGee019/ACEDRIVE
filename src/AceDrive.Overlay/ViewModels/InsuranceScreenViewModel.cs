using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The insurance broker (PORTPLAN item 34, port of Unity InsuranceScreen): every
/// owned car with its daily premium and an ADD/REMOVE toggle, the combined daily
/// cost, and the escrow a covered repair paid into with a COLLECT button. Unity
/// parity: the toggle flips OwnedCar.hasInsurance and banks through the host;
/// the premium is price × rate × the general increase minus the driver-rating
/// percent, floored at the rules' minimum. The driver rating is the host's
/// placeholder until item 06 lands (passed in, so the page never hardcodes it).
/// The Unity familiarity bump on the broker POI's owner is parked until the
/// marker network (items 24/59) gives the broker an owner, like the wash's.
/// The page writes nothing itself: the host banks the toggle and the collection
/// (SRP). Voice parity with the other pages: ADD/REMOVE and COLLECT stay on the
/// page and act at once like a click; BACK navigates, so its voice tap glows
/// teal first and waits out the shared beat.
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

    public InsuranceScreenViewModel(
        Garage garage, DriverWallet wallet, InsuranceRules rules, double driverRating,
        VoiceBeat beat, ICommand backCommand, Action<string, bool> onToggled, Action<double> onCollected)
    {
        _garage = garage;
        _wallet = wallet;
        _rules = rules;
        _driverRating = driverRating;
        _beat = beat;
        _backCommand = backCommand;
        _onToggled = onToggled;
        _onCollected = onCollected;
        ToggleCommand = new ParameterizedCommand<InsuranceCarRow>(Toggle);
        BackCommand = new RelayCommand(Back);
        CollectCommand = new RelayCommand(Collect);
        Reload();
    }

    /// <summary>Flips one car's cover (Unity OnAddClicked).</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>Moves the escrow to cash (Unity OnCollectItClicked).</summary>
    public ICommand CollectCommand { get; }

    public ICommand BackCommand { get; }

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    public IReadOnlyList<InsuranceCarRow> Rows { get => _rows; private set => SetField(ref _rows, value); }
    private IReadOnlyList<InsuranceCarRow> _rows = [];

    public string CashLine { get => _cashLine; private set => SetField(ref _cashLine, value); }
    private string _cashLine = "";

    /// <summary>Unity's "_textInsuranceCostTotal": the combined daily premium.</summary>
    public string TotalLine { get => _totalLine; private set => SetField(ref _totalLine, value); }
    private string _totalLine = "";

    /// <summary>Unity's "What the insurance will pay you": the escrow.</summary>
    public string PendingLine { get => _pendingLine; private set => SetField(ref _pendingLine, value); }
    private string _pendingLine = "";

    public bool PendingVisible { get => _pendingVisible; private set => SetField(ref _pendingVisible, value); }
    private bool _pendingVisible;

    /// <summary>A garage with no car at all: the hint keeps the page honest.</summary>
    public bool IsEmpty => Rows.Count == 0;

    public string EmptyLine { get; } = Strings.Get("InsuranceEmpty");

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    // Unity OnAddClicked: flips the cover and banks (Unity SaveManager.Save);
    // the page repaints from the same rule the next visit would read.
    private void Toggle(InsuranceCarRow? row)
    {
        if (row is null || _garage.Find(row.Key) is not { } car)
        {
            return;
        }
        car.SetInsurance(!car.HasInsurance);
        Reload();
        _onToggled(row.Key, car.HasInsurance);
    }

    // Unity OnCollectItClicked/CollectPayout: the escrow moves to cash.
    private void Collect()
    {
        ClearBackFlash();
        double moved = _wallet.CollectInsurancePayout();
        if (moved <= 0.0)
        {
            return;
        }
        Reload();
        _onCollected(moved);
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out (bank-page precedent).
    private void Back()
    {
        ClearBackFlash();
        _backCommand.Execute(null);
    }

    private void Reload()
    {
        CashLine = Strings.Format("InsuranceCash", _wallet.Cash);
        var rows = new List<InsuranceCarRow>(_garage.Cars.Count);
        double total = 0.0;
        foreach (OwnedCar car in _garage.Cars)
        {
            CarSpec? spec = _garage.SpecOf(car);
            double daily = spec is { Price: > 0.0 }
                ? InsurancePricing.DiscountedDailyCost(spec.Price, _driverRating, _rules)
                : _rules.MinimumDailyCost;
            if (car.HasInsurance)
            {
                total += daily;
            }
            rows.Add(new InsuranceCarRow(
                car.Key,
                CarTitles.Of(car.Key, spec),
                car.HasInsurance ? Strings.Format("InsuranceDaily", daily) : Strings.Get("InsuranceNotCovered"),
                car.HasInsurance,
                Strings.Get(car.HasInsurance ? "InsuranceRemove" : "InsuranceAdd")));
        }
        Rows = rows;
        TotalLine = Strings.Format("InsuranceTotal", total);
        double pending = _wallet.PendingInsurance;
        PendingVisible = pending > 0.0;
        PendingLine = Strings.Format("InsurancePending", pending);
        OnPropertyChanged(nameof(IsEmpty));
        RebuildVoice();
    }

    // A car's title is the voice key for its row (toggling stays on this page,
    // so it acts at once like a click); COLLECT acts at once too; BACK glows
    // first and acts at the beat's end. Rebuilt with the page so the actions
    // follow the current garage.
    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>(Rows.Count + 2);
        foreach (InsuranceCarRow row in Rows)
        {
            InsuranceCarRow hit = row;
            actions[hit.Title] = () => Toggle(hit);
        }
        actions[Strings.Get("InsuranceCollect")] = Collect;
        // Back navigates: glow first, act at the end of the shared beat.
        actions[Strings.Get("DeliveryBack")] = () =>
        {
            BackFlash = true;
            _beat.Defer(Back);
        };
        VoiceActions = actions;
    }

    private void ClearBackFlash() => BackFlash = false;
}
