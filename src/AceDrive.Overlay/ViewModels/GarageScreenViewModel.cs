using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The garage page (PORTPLAN item 30, port of Unity GarageScreen): the cars
/// owned now in a picker, and the picked car's stats below it. Unity's garage
/// lists the cars as rows and shows the six dealer-style preview values for the
/// selection (price, engine size, power, consumption, passengers, boot); this
/// page keeps that preview and turns the list into the dropdown the home page
/// asked for, with the odometer added because the port tracks mileage per car.
/// The current car opens selected, the way Unity selects it on entry, and the
/// drive button switches to any other picked car. Like Unity, the screen itself
/// writes nothing: the switch is banked by the host.
/// Voice parity: a car's title or the drive label picks/switches at once (both
/// stay on the page, like the dealer's row taps); BACK navigates, so its tap
/// glows teal first and the page waits out the shared beat.
/// </summary>
public sealed class GarageScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly Garage _garage;
    private readonly ICommand _backCommand;
    private readonly Action<string> _onSwitch;
    private readonly VoiceBeat _beat;
    private readonly RelayCommand _switch;

    public GarageScreenViewModel(
        Garage garage, ICommand backCommand, Action<string> onSwitch, VoiceBeat beat)
    {
        _garage = garage;
        _backCommand = backCommand;
        _onSwitch = onSwitch;
        _beat = beat;
        BackCommand = new RelayCommand(Back);
        _switch = new RelayCommand(Switch, () => SwitchEnabled);
        SwitchCommand = _switch;
        Cars = BuildRows();
        _selected = InitialRow();
        Reload();
    }

    public ICommand BackCommand { get; }

    /// <summary>Drives the picked car: disabled while it is already the current one.</summary>
    public ICommand SwitchCommand { get; }

    /// <summary>The picker's items, one per owned car, in garage order.</summary>
    public IReadOnlyList<GarageRow> Cars { get; private set; }

    /// <summary>
    /// The car the stats panel reads. The view binds this two-way to the
    /// dropdown, so a pick repaints the labels without the page writing anything.
    /// </summary>
    public GarageRow? SelectedCar
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                Reload();
            }
        }
    }
    private GarageRow? _selected;

    /// <summary>The picked car's stats: label/value pairs the view lays out.</summary>
    public IReadOnlyList<GarageStatRow> Stats { get => _stats; private set => SetField(ref _stats, value); }
    private IReadOnlyList<GarageStatRow> _stats = [];

    /// <summary>Unity's drive label: DRIVE while another car is picked, CURRENT
    /// CAR while the driven one is, so the button explains itself in both states.</summary>
    public string SwitchLabel { get => _switchLabel; private set => SetField(ref _switchLabel, value); }
    private string _switchLabel = "";

    /// <summary>False for the current car and for an empty garage (nothing to switch to).</summary>
    public bool SwitchEnabled { get => _switchEnabled; private set => SetField(ref _switchEnabled, value); }
    private bool _switchEnabled;

    /// <summary>A garage with no cars at all: a save that predates this slice and
    /// never completed onboarding. The hint keeps the page from reading as broken.</summary>
    public bool IsEmpty => Cars.Count == 0;

    /// <summary>The picker's inverse flag, so the view can hide the form on an empty garage.</summary>
    public bool HasCars => !IsEmpty;

    public string EmptyLine { get; } = Strings.Get("GarageEmpty");

    /// <summary>Caption above the dropdown.</summary>
    public string SelectLabel { get; } = Strings.Get("GarageSelect");

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // The picker's list: one row per owned car, the catalogue row behind the
    // key naming it and the one driven now marked.
    private List<GarageRow> BuildRows()
    {
        var rows = new List<GarageRow>(_garage.Cars.Count);
        foreach (OwnedCar car in _garage.Cars)
        {
            rows.Add(new GarageRow(
                car.Key,
                CarTitles.Of(car.Key, _garage.SpecOf(car)),
                string.Equals(car.Key, _garage.CurrentKey, StringComparison.Ordinal)));
        }
        return rows;
    }

    // Unity selects the current car on entry; an empty current key falls back
    // to the first row so the stats block still opens on a car.
    private GarageRow? InitialRow()
    {
        foreach (GarageRow row in Cars)
        {
            if (row.IsCurrent)
            {
                return row;
            }
        }
        return Cars.Count > 0 ? Cars[0] : null;
    }

    // The stats come from the catalogue row the key resolves, so a retired
    // variant (no catalogue row) shows its title with an empty stats block
    // instead of invented numbers.
    private void Reload()
    {
        GarageRow? row = _selected;
        OwnedCar? car = row is null ? null : _garage.Find(row.Key);
        CarSpec? spec = car is null ? null : _garage.SpecOf(car);
        Stats = BuildStats(car, spec);
        SwitchEnabled = car is not null
            && !string.Equals(row!.Key, _garage.CurrentKey, StringComparison.Ordinal);
        SwitchLabel = Strings.Get(SwitchEnabled ? "GarageDrive" : "GarageCurrentCar");
        _switch.RaiseCanExecuteChanged();
        RebuildVoice();
    }

    private static IReadOnlyList<GarageStatRow> BuildStats(OwnedCar? car, CarSpec? spec)
    {
        if (car is null || spec is null)
        {
            return [];
        }
        return
        [
            // Unity GarageScreen/CarDealerScreen preview order. The price is the
            // catalogue figure; Unity scales it by engine condition, which the
            // port's value pass has not adopted (the dealer shows the same row).
            new(Strings.Get("GaragePriceLabel"), Strings.Format("GaragePrice", spec.Price)),
            new(Strings.Get("GarageEngineLabel"), Strings.Format("GarageEngine", spec.EngineCc)),
            new(Strings.Get("GaragePowerLabel"), Strings.Format("GaragePower", spec.Horsepower)),
            new(Strings.Get("GarageConsumptionLabel"), Strings.Format("GarageConsumption", spec.FuelPer100Km)),
            new(Strings.Get("GaragePassengersLabel"), Strings.Format("GaragePassengers", spec.Passengers)),
            new(Strings.Get("GarageBootLabel"), Strings.Format("GarageBoot", spec.TrunkLitres)),
            // The port's own per-car reading (Unity's garage slot shows it too).
            new(Strings.Get("GarageOdometerLabel"), Strings.Format("GarageOdometer", car.Odometer)),
        ];
    }

    // Unity GarageScreen.OnChangeClicked: the picked car becomes the one driven;
    // the host banks it (Unity SaveManager.Save) and logs the switch. A rejected
    // pick repaints from the garage's own state and stops, so the page never
    // claims a switch that did not happen.
    private void Switch()
    {
        if (_selected is not { } row || row.IsCurrent || !_garage.SetCurrent(row.Key))
        {
            return;
        }
        Rebuild(row.Key);
        _onSwitch(row.Key);
    }

    // Rebuild after a switch keeps the picked car selected, now as the current
    // one, so the stats the player is reading do not jump to another row.
    private void Rebuild(string key)
    {
        Cars = BuildRows();
        _selected = FindRow(key);
        OnPropertyChanged(nameof(SelectedCar));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasCars));
        Reload();
    }

    private GarageRow? FindRow(string? key)
    {
        if (key is null)
        {
            return InitialRow();
        }
        foreach (GarageRow row in Cars)
        {
            if (string.Equals(row.Key, key, StringComparison.Ordinal))
            {
                return row;
            }
        }
        return InitialRow();
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out — a click during a pending voice
    // tap never leaves a stale teal behind (bank-page precedent).
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>(Cars.Count + 3);
        // A car title picks that row: selecting stays on the page, so it acts
        // at once like the dealer's row taps (which carry the same titles).
        foreach (GarageRow row in Cars)
        {
            GarageRow hit = row;
            actions[hit.Title] = () => SelectedCar = hit;
        }
        // The drive button stays on the page too; the current pick makes it a
        // no-op, exactly like its disabled state in the view.
        actions[Strings.Get("GarageDrive")] = Switch;
        actions[Strings.Get("GarageCurrentCar")] = Switch;
        // Back navigates: glow first, act at the end of the shared beat.
        actions[Strings.Get("GarageBack")] = () =>
        {
            BackFlash = true;
            _beat.Defer(Back);
        };
        VoiceActions = actions;
    }
}
