using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The car mechanic (PORTPLAN items 31/32, port of Unity CarMechanicScreen): the
/// car driven now, its breakdown chance and engine condition, the eight part
/// rows with their efficiency and class-scaled price, an add-only repair queue,
/// and the engine repair/replace pair while the engine is stranded. Parts are
/// REPLACED, not repaired — the price is the full catalogue price, whatever the
/// wear (Unity's rule), so the ADD button is a full-price buy in 1000-km steps.
/// Unity parity: the repair-parts button appears only with a queue, the engine
/// buttons only while the engine is broken, the price summary shows the cash
/// left, and a new engine zeroes the odometer and every part baseline. The page
/// writes nothing itself: the host banks the cash and the condition (SRP).
/// The Unity popularity bump on the mechanic POI's owner is parked until the
/// marker network (items 24/59) gives the mechanic an owner, like the wash and
/// dealer bumps. Voice parity: an ADD or a repair stays on the page and acts at
/// once like a click; BACK navigates, so its voice tap glows teal first and
/// waits out the shared beat.
/// </summary>
public sealed class MechanicScreenViewModel : ViewModelBase, IVoiceScreen, IPoiScreen
{
    private readonly Garage _garage;
    private readonly DriverWallet _wallet;
    private readonly GameClock _game;
    private readonly VoiceBeat _beat;
    private readonly ICommand _backCommand;
    private readonly Action<MechanicRepair> _onRepaired;
    private readonly MaintenanceRules _rules;
    private readonly HashSet<string> _queued = new(StringComparer.Ordinal);
    private readonly RelayCommand _repairParts;
    private readonly RelayCommand _repairEngine;
    private readonly RelayCommand _newEngine;

    public MechanicScreenViewModel(
        Garage garage, DriverWallet wallet, GameClock game, VoiceBeat beat,
        ICommand backCommand, Action<MechanicRepair> onRepaired)
    {
        _garage = garage;
        _wallet = wallet;
        _game = game;
        _beat = beat;
        _backCommand = backCommand;
        _onRepaired = onRepaired;
        // The parts/prices the fleet's cars were built with (PORTPLAN items
        // 31/32), so the page never carries a second copy of the balance numbers.
        _rules = garage.MaintenanceRules;
        AddCommand = new ParameterizedCommand<MechanicPartRow>(Add);
        BackCommand = new RelayCommand(Back);
        _repairParts = new RelayCommand(RepairParts, CanRepairParts);
        _repairEngine = new RelayCommand(RepairEngine, CanFixEngine);
        _newEngine = new RelayCommand(NewEngine, CanFixEngine);
        RepairPartsCommand = _repairParts;
        RepairEngineCommand = _repairEngine;
        NewEngineCommand = _newEngine;
        Reload();
    }

    /// <summary>Queues one part for replacement.</summary>
    public ICommand AddCommand { get; }

    public ICommand RepairPartsCommand { get; }

    public ICommand RepairEngineCommand { get; }

    public ICommand NewEngineCommand { get; }

    public ICommand BackCommand { get; }

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    public IReadOnlyList<MechanicPartRow> Rows { get => _rows; private set => SetField(ref _rows, value); }
    private IReadOnlyList<MechanicPartRow> _rows = [];

    /// <summary>The car driven now plus its class, or the no-car line.</summary>
    public string CarLine { get => _carLine; private set => SetField(ref _carLine, value); }
    private string _carLine = "";

    public string CashLine { get => _cashLine; private set => SetField(ref _cashLine, value); }
    private string _cashLine = "";

    /// <summary>Unity's "_textBreakdown": "Breakdown chance currently: {0:F4}%".</summary>
    public string BreakdownLine { get => _breakdownLine; private set => SetField(ref _breakdownLine, value); }
    private string _breakdownLine = "";

    /// <summary>Unity's "_textEngineCondition", shown while the engine runs.</summary>
    public string EngineConditionLine { get => _engineConditionLine; private set => SetField(ref _engineConditionLine, value); }
    private string _engineConditionLine = "";

    /// <summary>Unity's "_textEngineFailure": the stranded block, shown only when broken.</summary>
    public bool EngineFailureVisible { get => _engineFailureVisible; private set => SetField(ref _engineFailureVisible, value); }
    private bool _engineFailureVisible;

    public string RepairEngineCostLine { get => _repairEngineCostLine; private set => SetField(ref _repairEngineCostLine, value); }
    private string _repairEngineCostLine = "";

    public string NewEngineCostLine { get => _newEngineCostLine; private set => SetField(ref _newEngineCostLine, value); }
    private string _newEngineCostLine = "";

    /// <summary>Unity's "_textPrice": the queued total and the cash left after.</summary>
    public string TotalLine { get => _totalLine; private set => SetField(ref _totalLine, value); }
    private string _totalLine = "";

    public bool TotalVisible { get => _totalVisible; private set => SetField(ref _totalVisible, value); }
    private bool _totalVisible;

    /// <summary>The repair-parts button, hidden until at least one part is queued.</summary>
    public bool RepairPartsVisible { get => _repairPartsVisible; private set => SetField(ref _repairPartsVisible, value); }
    private bool _repairPartsVisible;

    /// <summary>The engine buttons, hidden until the engine is stranded.</summary>
    public bool EngineButtonsVisible => EngineFailureVisible;

    /// <summary>A garage with no car at all: the hint keeps the page honest.</summary>
    public bool HasCar { get => _hasCar; private set => SetField(ref _hasCar, value); }
    private bool _hasCar;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    // Unity OnAddToggleClicked: adds the part to the queue; the button is gone
    // afterwards (the queue cannot be reversed). Nothing is spent until REPAIR.
    private void Add(MechanicPartRow? row)
    {
        if (row is null || !row.AddEnabled || _queued.Contains(row.Key))
        {
            return;
        }
        _queued.Add(row.Key);
        Reload();
    }

    // Unity OnRepairPartsClicked: every queued part is replaced in one go, the
    // combined price moves, and each part's baseline resets to the odometer.
    private void RepairParts()
    {
        ClearBackFlash();
        if (_garage.Current is not { } car || !CanRepairParts())
        {
            return;
        }
        double total = QueuedTotal(ClassOf(car));
        if (!_wallet.TrySpend(total))
        {
            return;
        }
        int replaced = 0;
        foreach (MaintenancePart part in _rules.Parts)
        {
            if (_queued.Contains(part.Key))
            {
                car.Condition.ServicePart(part.Key, car.Odometer);
                replaced++;
            }
        }
        _queued.Clear();
        Reload();
        _onRepaired(new MechanicRepair("parts", total, replaced, EngineReplaced: false));
    }

    // Unity OnRepairEngineClicked: clears the broken state and resets the wear
    // counter to the current odometer (the km are kept).
    private void RepairEngine()
    {
        ClearBackFlash();
        if (_garage.Current is not { } car || !CanFixEngine())
        {
            return;
        }
        double price = EngineRepairPrice(car, ClassOf(car));
        if (!_wallet.TrySpend(price))
        {
            return;
        }
        car.Engine.Repair(car.Odometer, _game.GameNow);
        Reload();
        _onRepaired(new MechanicRepair("engine-repair", price, 0, EngineReplaced: false));
    }

    // Unity OnNewEngineClicked: a new engine resets the odometer to 0 and every
    // part baseline with it, so any queued part is now freshly serviced and is
    // dropped rather than charged twice.
    private void NewEngine()
    {
        ClearBackFlash();
        if (_garage.Current is not { } car || !CanFixEngine())
        {
            return;
        }
        double price = NewEnginePrice(car, ClassOf(car));
        if (!_wallet.TrySpend(price))
        {
            return;
        }
        car.Engine.ReplaceEngine(_game.GameNow);
        car.ResetOdometer();
        car.Condition.ResetAll();
        _queued.Clear();
        Reload();
        _onRepaired(new MechanicRepair("new-engine", price, 0, EngineReplaced: true));
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out (bank-page precedent).
    private void Back()
    {
        ClearBackFlash();
        _backCommand.Execute(null);
    }

    private bool CanRepairParts() =>
        _garage.Current is { } car && _queued.Count > 0 && _wallet.Cash >= QueuedTotal(ClassOf(car));

    private bool CanFixEngine() => _garage.Current is { } car && car.Engine.Broken;

    private void Reload()
    {
        if (_garage.Current is not { } car)
        {
            HasCar = false;
            CarLine = Strings.Get("MechanicNoCar");
            CashLine = string.Empty;
            BreakdownLine = string.Empty;
            EngineConditionLine = string.Empty;
            RepairEngineCostLine = string.Empty;
            NewEngineCostLine = string.Empty;
            TotalLine = string.Empty;
            TotalVisible = false;
            RepairPartsVisible = false;
            EngineFailureVisible = false;
            Rows = [];
            RaiseCommands();
            RebuildVoice();
            return;
        }

        HasCar = true;
        CarSpec? spec = _garage.SpecOf(car);
        int carClass = spec?.Class ?? 0;
        int engineCc = spec?.EngineCc ?? 0;
        CarLine = Strings.Format("MechanicCarLine", CarTitles.Of(car.Key, spec), carClass);
        CashLine = Strings.Format("MechanicCash", _wallet.Cash);
        BreakdownLine = Strings.Format("MechanicBreakdown", car.Condition.BreakdownChance(car.Odometer) * 100.0);
        double brokenPercent = car.Engine.Broken ? 1.0 : car.Engine.BrokenPercent(engineCc, car.Odometer);
        // Floor at 5 decimals instead of rounding: a nearly-pristine engine must
        // never round up to 100.00000 (Unity CarMechanicScreen).
        double condition = Math.Floor((100.0 - brokenPercent * 100.0) * 100000.0) / 100000.0;
        EngineConditionLine = Strings.Format("MechanicEngineCondition", condition);
        EngineFailureVisible = car.Engine.Broken;
        RepairEngineCostLine = Strings.Format("MechanicRepairEngineCost", EngineRepairPrice(car, carClass));
        NewEngineCostLine = Strings.Format("MechanicNewEngineCost", NewEnginePrice(car, carClass));
        Rows = Paint(car, carClass);
        double total = QueuedTotal(carClass);
        TotalVisible = total > 0.0;
        TotalLine = Strings.Format("MechanicTotal", total, _wallet.Cash - total);
        RepairPartsVisible = _queued.Count > 0 && total > 0.0;
        RaiseCommands();
        OnPropertyChanged(nameof(EngineButtonsVisible));
        RebuildVoice();
    }

    private void RaiseCommands()
    {
        _repairParts.RaiseCanExecuteChanged();
        _repairEngine.RaiseCanExecuteChanged();
        _newEngine.RaiseCanExecuteChanged();
    }

    // Unity RefreshPartRowStates: efficiency counts down from 100, the price is
    // the class-scaled full price, and the ADD button reads ADD / NO $ / ✓ with
    // its interactable state matching. A queued row hides its button.
    private List<MechanicPartRow> Paint(OwnedCar car, int carClass)
    {
        var rows = new List<MechanicPartRow>(_rules.Parts.Count);
        foreach (MaintenancePart part in _rules.Parts)
        {
            double fraction = car.Condition.PartFraction(part.Key, car.Odometer);
            double price = part.FullPrice * _rules.MultiplierFor(carClass);
            bool serviced = fraction <= 0.0001;
            bool queued = _queued.Contains(part.Key);
            bool affordable = _wallet.Cash >= price - 0.0001;
            string action = serviced
                ? Strings.Get("MechanicServiced")
                : !affordable ? Strings.Get("MechanicNoCash") : Strings.Get("MechanicAdd");
            rows.Add(new MechanicPartRow(
                part.Key,
                MechanicPartNames.Of(part.Key),
                Strings.Format("MechanicEfficiency", (1.0 - fraction) * 100.0),
                Strings.Format("MechanicPrice", price),
                action,
                AddEnabled: !serviced && !queued && affordable,
                AddVisible: !queued,
                Queued: queued));
        }
        return rows;
    }

    private double QueuedTotal(int carClass)
    {
        double total = 0.0;
        foreach (MaintenancePart part in _rules.Parts)
        {
            if (_queued.Contains(part.Key))
            {
                total += part.FullPrice * _rules.MultiplierFor(carClass);
            }
        }
        return total;
    }

    private double EngineRepairPrice(OwnedCar car, int carClass) =>
        (_garage.SpecOf(car)?.Price ?? 0.0) * _rules.EngineRepairFraction * _rules.MultiplierFor(carClass);

    private double NewEnginePrice(OwnedCar car, int carClass) =>
        (_garage.SpecOf(car)?.Price ?? 0.0) * _rules.NewEngineFraction * _rules.MultiplierFor(carClass);

    private int ClassOf(OwnedCar car) => _garage.SpecOf(car)?.Class ?? 0;

    // Part names are the voice keys for their rows; the repair labels act at
    // once, BACK glows first and acts at the beat's end. The menu is stable per
    // car, so it is rebuilt with the page (no per-row delegate churn).
    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>(Rows.Count + 4);
        foreach (MechanicPartRow row in Rows)
        {
            MechanicPartRow hit = row;
            actions[hit.Name] = () => Add(hit);
        }
        actions[Strings.Get("MechanicRepairParts")] = RepairParts;
        actions[Strings.Get("MechanicRepairEngine")] = RepairEngine;
        actions[Strings.Get("MechanicNewEngine")] = NewEngine;
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
