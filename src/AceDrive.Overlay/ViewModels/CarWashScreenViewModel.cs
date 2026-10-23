using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The car wash (PORTPLAN item 33, port of Unity CarWashScreen): the car
/// driven now, its filth reading and the one flat wash price. Unity parity —
/// the wash is a single CR50 move whatever the reading, refused when the car
/// is already clean or the cash is short, and the button goes inert after the
/// wash while the result line reports what came off. The page writes nothing
/// itself: the host banks the cash and the reading (SRP).
/// The Unity popularity bump on the wash POI's owner is parked until a wash
/// POI exists (items 24/59), like the dealer's familiarity bump.
/// Voice parity with the other pages: WASH IT stays on the page and acts at
/// once like a click; BACK navigates, so its voice tap glows teal first and
/// waits out the shared beat.
/// </summary>
public sealed class CarWashScreenViewModel : ViewModelBase, IVoiceScreen, IPoiScreen
{
    private readonly Garage _garage;
    private readonly DriverWallet _wallet;
    private readonly FilthRules _rules;
    private readonly VoiceBeat _beat;
    private readonly ICommand _backCommand;
    private readonly Action<int, double> _onWashed;
    private readonly RelayCommand _wash;
    private bool _washed;
    private int _removed;
    private double _paid;

    public CarWashScreenViewModel(
        Garage garage, DriverWallet wallet, VoiceBeat beat,
        ICommand backCommand, Action<int, double> onWashed)
    {
        _garage = garage;
        _wallet = wallet;
        // The price the fleet's cars were built with (PORTPLAN item 33), so
        // the page never carries a second copy of the balance numbers.
        _rules = garage.FilthRules;
        _beat = beat;
        _backCommand = backCommand;
        _onWashed = onWashed;
        _wash = new RelayCommand(Wash, CanWash);
        WashCommand = _wash;
        BackCommand = new RelayCommand(Back);
        Reload();
        RebuildVoice();
    }

    public ICommand WashCommand { get; }

    public ICommand BackCommand { get; }

    /// <summary>The car driven now, by its catalogue title; the no-car line on
    /// an empty garage.</summary>
    public string CarLine { get => _carLine; private set => SetField(ref _carLine, value); }
    private string _carLine = "";

    /// <summary>Unity's "_textFilthLevel": "Filth: {0} / {1}".</summary>
    public string FilthLine { get => _filthLine; private set => SetField(ref _filthLine, value); }
    private string _filthLine = "";

    /// <summary>Unity's flat "Price to wash your car: CR{0:F2}".</summary>
    public string PriceLine { get => _priceLine; private set => SetField(ref _priceLine, value); }
    private string _priceLine = "";

    /// <summary>Unity's result block: "Removed {0} filth for CR {1:F2}", shown
    /// only after a wash.</summary>
    public string ResultLine { get => _resultLine; private set => SetField(ref _resultLine, value); }
    private string _resultLine = "";

    public bool ResultVisible { get => _resultVisible; private set => SetField(ref _resultVisible, value); }
    private bool _resultVisible;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // Unity OnWashClicked's gate: a clean car, a short wallet or a car already
    // washed this visit (the button hides then) leaves the page untouched. The
    // spend itself is re-checked in Wash, so a stale command state never spends.
    private bool CanWash() =>
        !_washed && _garage.Current is { } car && car.Filth.Value > 0
        && _wallet.Cash >= _rules.WashPrice;

    // Unity CarWashService.WashCar: the cash moves, the reading clears, the
    // result shows; the host banks both (Unity SaveManager.Save).
    private void Wash()
    {
        if (!CanWash() || _garage.Current is not { } car)
        {
            return;
        }
        _removed = car.Filth.Value;
        _paid = _rules.WashPrice;
        if (!_wallet.TrySpend(_paid))
        {
            return;
        }
        car.Filth.Wash();
        _washed = true;
        Reload();
        _onWashed(_removed, _paid);
    }

    // One path back: a click and the voice tap's deferred end both land here,
    // so the glow clears on the way out (bank-page precedent).
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void Reload()
    {
        if (_garage.Current is not { } car)
        {
            CarLine = Strings.Get("CarWashNoCar");
            FilthLine = string.Empty;
            PriceLine = string.Empty;
            ResultVisible = false;
            _wash.RaiseCanExecuteChanged();
            return;
        }
        CarLine = CarTitles.Of(car.Key, _garage.SpecOf(car));
        FilthLine = Strings.Format("CarWashFilth", car.Filth.Value, car.Filth.Max);
        PriceLine = Strings.Format("CarWashPrice", _rules.WashPrice);
        ResultVisible = _washed;
        if (_washed)
        {
            ResultLine = Strings.Format("CarWashRemoved", _removed, _paid);
        }
        _wash.RaiseCanExecuteChanged();
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            // Wash stays on this page: it acts at once like a click.
            [Strings.Get("CarWashWash")] = Wash,
            // Back navigates: glow first, act at the end of the shared beat.
            [Strings.Get("CarWashBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
