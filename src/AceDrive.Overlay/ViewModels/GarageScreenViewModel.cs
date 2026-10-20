using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The garage page (PORTPLAN item 30, port of Unity GarageScreen): every owned car
/// with its specs, mileage and value, the current car marked, and a switch to
/// another car. Unity's per-car job actions (car sharing, employee, zone expansion)
/// belong to items 22/23/58 and are not on the page yet; selling and depreciation do
/// not exist in the mother game and are not invented here. The value line is the
/// catalogue price — Unity scales it by engine condition, which arrives with item 32.
/// Like Unity, the screen itself writes nothing: the switch is banked by the host.
/// Voice parity with the other pages: BACK navigates, so its voice tap glows
/// teal first and the page waits out the shared beat.
/// </summary>
public sealed class GarageScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly Garage _garage;
    private readonly ICommand _backCommand;
    private readonly Action<string> _onSwitch;
    private readonly VoiceBeat _beat;

    public GarageScreenViewModel(
        Garage garage, ICommand backCommand, Action<string> onSwitch, VoiceBeat beat)
    {
        _garage = garage;
        _backCommand = backCommand;
        _onSwitch = onSwitch;
        _beat = beat;
        BackCommand = new RelayCommand(Back);
        SwitchCommand = new ParameterizedCommand<GarageRow>(Switch);
        Rebuild();
        RebuildVoice();
    }

    public ICommand BackCommand { get; }

    /// <summary>The page's one action: drive the tapped car.</summary>
    public ICommand SwitchCommand { get; }

    public IReadOnlyList<GarageRow> Rows { get => _rows; private set => SetField(ref _rows, value); }
    private IReadOnlyList<GarageRow> _rows = [];

    /// <summary>A garage with no cars at all: a save that predates this slice and
    /// never completed onboarding. The hint keeps the page from reading as broken.</summary>
    public bool IsEmpty => Rows.Count == 0;

    public string EmptyLine { get; } = Strings.Get("GarageEmpty");

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // Unity RefreshScreen/ApplyAvailabilityState: the current car reads as such and
    // cannot be re-picked; every other row switches on tap.
    private void Rebuild()
    {
        var rows = new List<GarageRow>(_garage.Cars.Count);
        foreach (OwnedCar car in _garage.Cars)
        {
            CarSpec? spec = _garage.SpecOf(car);
            rows.Add(new GarageRow(
                car.Key,
                CarTitles.Of(car.Key, spec),
                spec is null
                    ? string.Empty
                    : Strings.Format("GarageCarSpecs", spec.Class, spec.Passengers, spec.TrunkLitres, car.Odometer),
                spec is null ? string.Empty : Strings.Format("GarageCarValue", spec.Price),
                string.Equals(car.Key, _garage.CurrentKey, StringComparison.Ordinal)));
        }
        Rows = rows;
        OnPropertyChanged(nameof(IsEmpty));
    }

    // Unity GarageScreen.OnChangeClicked: tapping another car makes it the one
    // driven; the host banks it (Unity SaveManager.Save) and logs the switch. A
    // rejected pick (the car left the garage mid-screen) repaints and stops, so
    // the page never claims a switch that did not happen.
    private void Switch(GarageRow? row)
    {
        if (row is null || row.IsCurrent || !_garage.SetCurrent(row.Key))
        {
            return;
        }
        Rebuild();
        _onSwitch(row.Key);
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
        VoiceActions = new Dictionary<string, Action>
        {
            // Back navigates: glow first, act at the end of the shared beat.
            [Strings.Get("GarageBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
