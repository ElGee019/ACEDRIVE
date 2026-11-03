using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The car dealer's weekly floor (PORTPLAN item 29, port of Unity CarDealerScreen)
/// plus ACEDRIVE's trade-in half: this week's stock, the selected car's specs and
/// the buy button, and a sell list of the player's own cars at the price
/// <see cref="DealerSaleRules"/> quotes. Stock is drawn once per visit from the
/// current week (DealerStock, item 29) and never persists; owning a car removes it
/// from every later floor. Unity parity for buying: money is the only gate (no
/// slot cap, no class lock, no trade-in), and the car is owned the moment the cash
/// moves — the follow-up choice to drive it lives on BoughtFirstCarScreen.
/// Selling is an ACEDRIVE addition (the mother game's dealer only buys) and always
/// keeps one car in the garage.
/// Both verbs stay on the page: SELL opens the garage or confirms the picked sale,
/// BUY buys the picked floor car or returns to the floor while the sell list is up.
/// A failed click names its reason on the button itself (CAN'T SELL YOUR ONLY CAR,
/// NO CARS TO SELL, CAN'T AFFORD THIS CAR) and the page stays open, so the buttons
/// are never pre-disabled into a dead end. The screen writes nothing itself: the
/// host banks the purchase and the sale. Voice parity with the other pages: a car
/// name picks at once; BUY glows first since the follow-up page replaces this one;
/// SELL and the mode switch stay on the page so they act at once, and BACK glows
/// and navigates.
/// </summary>
public sealed class DealerScreenViewModel : ViewModelBase, IVoiceScreen, IPoiScreen, IRefreshableScreen
{
    private readonly IReadOnlyList<CarSpec> _catalog;
    private readonly Garage _garage;
    private readonly DriverWallet _wallet;
    private readonly int _week;
    private readonly ICommand _backCommand;
    private readonly Action<CarSpec> _onBought;
    private readonly Action<CarSpec, double> _onSold;
    private readonly VoiceBeat _beat;
    private readonly RelayCommand _buy;
    private readonly RelayCommand _sell;
    // This week's floor. The rows below are the same data preformatted for the
    // view, so the raw list stays private to the page. A sale never redraws the
    // floor: the sold car was owned when the floor was drawn, so rebuilding would
    // put the just-sold car back on it at full price.
    private IReadOnlyList<CarSpec> _stock = [];
    // The picked row, by catalogue key: the same key resolves in either mode, so
    // swapping lists cannot leave a stale pick behind.
    private string? _selectedKey;

    public DealerScreenViewModel(
        IReadOnlyList<CarSpec> catalog, Garage garage, DriverWallet wallet, int week,
        ICommand backCommand, Action<CarSpec> onBought,
        Action<CarSpec, double> onSold, VoiceBeat beat)
    {
        _catalog = catalog;
        _garage = garage;
        _wallet = wallet;
        _week = week;
        _backCommand = backCommand;
        _onBought = onBought;
        _onSold = onSold;
        _beat = beat;
        BackCommand = new RelayCommand(Back);
        SelectCommand = new ParameterizedCommand<DealerRow>(Select);
        // Both action commands stay executable: a click that fails paints its
        // reason on the button instead of a disabled state the player cannot press.
        _buy = new RelayCommand(Buy);
        BuyCommand = _buy;
        _sell = new RelayCommand(Sell);
        SellCommand = _sell;
        _stock = DealerStock.Build(catalog, garage.Keys, week);
        PaintRows();
        Reload();
    }

    public ICommand BackCommand { get; }

    /// <summary>Picks a car: the row highlights and the preview follows.</summary>
    public ICommand SelectCommand { get; }

    /// <summary>Buys the picked floor car, or returns to the floor while selling.</summary>
    public ICommand BuyCommand { get; }

    /// <summary>Opens the garage, or sells the picked owned car.</summary>
    public ICommand SellCommand { get; }

    /// <summary>The numbered rows answer to their spoken word ("ONE", "TWO", …):
    /// selecting a car stays on this page, so a tap acts at once like a click.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>True while the page lists the garage instead of the floor.</summary>
    public bool IsSelling { get => _isSelling; private set => SetField(ref _isSelling, value); }
    private bool _isSelling;

    /// <summary>Teal-glow acknowledgment for the navigating BUY tap.</summary>
    public bool BuyFlash { get => _buyFlash; private set => SetField(ref _buyFlash, value); }
    private bool _buyFlash;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyList<DealerRow> Rows { get => _rows; private set => SetField(ref _rows, value); }
    private IReadOnlyList<DealerRow> _rows = [];

    /// <summary>Unity RefreshHeader's "Current Saldo: CR{0:F2}".</summary>
    public string CashLine { get => _cashLine; private set => SetField(ref _cashLine, value); }
    private string _cashLine = "";

    /// <summary>The preview block: the picked car's specs, or the pick hint.</summary>
    public string SpecsLine { get => _specsLine; private set => SetField(ref _specsLine, value); }
    private string _specsLine = "";

    public string SelectedLine { get => _selectedLine; private set => SetField(ref _selectedLine, value); }
    private string _selectedLine = "";

    /// <summary>The picked car's price (buy mode) or the dealer's offer (sell
    /// mode), shown below the list; empty with no pick.</summary>
    public string SelectedPriceLine { get => _selectedPriceLine; private set => SetField(ref _selectedPriceLine, value); }
    private string _selectedPriceLine = "";

    /// <summary>The sell button's label: SELL until a click explains the gate.</summary>
    public string SellLabel { get => _sellLabel; private set => SetField(ref _sellLabel, value); }
    private string _sellLabel = "";

    /// <summary>The buy button's label: BUY until a short wallet explains itself.</summary>
    public string BuyLabel { get => _buyLabel; private set => SetField(ref _buyLabel, value); }
    private string _buyLabel = "";

    /// <summary>Everything this player could still buy has been bought: the floor is empty.</summary>
    public bool IsEmpty => !IsSelling && Rows.Count == 0;

    public string EmptyLine { get; } = Strings.Get("DealerEmpty");

    public string PickLine { get; } = Strings.Get("DealerPick");

    /// <summary>
    /// A crash repair can spend the cash this floor quotes against while the
    /// page is interrupted, so the hand-back repaints the saldo.
    /// </summary>
    public void Refresh() => Reload();

    // Unity OnCarSelected: rows repaint (the selected one highlights), the preview
    // follows the pick, and both action labels go back to their verbs.
    private void Select(DealerRow? row)
    {
        _selectedKey = row?.Key;
        if (ResolveSelectedSpec() is null)
        {
            _selectedKey = null;
        }
        PaintRows();
        Reload();
    }

    // Unity OnBuyClicked: the gate is checked on the click itself, so a stale
    // selection can never spend and a short wallet paints CAN'T AFFORD THIS CAR on
    // the button instead of silently doing nothing. While the sell list is up BUY
    // is the way back to the floor. The purchase is the cash move plus the garage
    // entry; the host banks it and shows the follow-up choice.
    private void Buy()
    {
        // A click or the voice tap's deferred end both land here, so the glow
        // clears on the way out — a click during a pending tap leaves no teal.
        ClearVoiceFlash();
        if (IsSelling)
        {
            IsSelling = false;
            _selectedKey = null;
            PaintRows();
            Reload();
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }
        if (_selectedKey is not { } key || FindInStock(key) is not { } car)
        {
            return;
        }
        if (_wallet.Cash < car.Price || !_wallet.TrySpend(car.Price))
        {
            BuyLabel = Strings.Get("DealerCantAfford");
            return;
        }
        _garage.Add(car, _week);
        // Unity keeps the bought row on screen with the button hidden; the overlay
        // drops it instead and repaints from the same rule that would exclude it on
        // the next visit. The host navigates to the follow-up screen either way.
        _stock = DealerStock.Build(_catalog, _garage.Keys, _week);
        _selectedKey = null;
        PaintRows();
        Reload();
        OnPropertyChanged(nameof(IsEmpty));
        _onBought(car);
    }

    // The trade-in, in two taps: on the floor SELL opens the garage (or names the
    // only-car gate), in sell mode it takes the picked car. A sale re-derives the
    // offer from the car's own readings, so a stale selection can never pay twice
    // or pay for a car the garage no longer holds. The host banks the cash and
    // the garage.
    private void Sell()
    {
        ClearVoiceFlash();
        if (IsSelling)
        {
            SellPicked();
            return;
        }
        if (_garage.Cars.Count < 2)
        {
            SellLabel = Strings.Get(
                _garage.Cars.Count == 1 ? "DealerSellOnlyCar" : "DealerSellNoCars");
            return;
        }
        IsSelling = true;
        _selectedKey = null;
        PaintRows();
        Reload();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void SellPicked()
    {
        if (_garage.Cars.Count <= 1)
        {
            SellLabel = Strings.Get("DealerSellOnlyCar");
            return;
        }
        if (_selectedKey is not { } key)
        {
            return;
        }
        OwnedCar? owned = _garage.Find(key);
        CarSpec? car = owned is null ? null : _garage.SpecOf(owned);
        if (owned is null || car is null)
        {
            return;
        }
        double paid = SaleValue(owned, car);
        if (paid <= 0.0 || !_garage.Remove(key))
        {
            return;
        }
        _wallet.AddSale(paid);
        _selectedKey = null;
        PaintRows();
        Reload();
        OnPropertyChanged(nameof(IsEmpty));
        _onSold(car, paid);
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out (bank-page precedent).
    private void Back()
    {
        ClearVoiceFlash();
        _backCommand.Execute(null);
    }

    private void Reload()
    {
        CashLine = Strings.Format("DealerCash", _wallet.Cash);
        if (ResolveSelectedSpec() is { } car)
        {
            SelectedLine = CarTitles.Of(car);
            SelectedPriceLine = IsSelling
                ? Strings.Format("DealerSellPrice", SaleValue(_garage.Find(_selectedKey!)!, car))
                : Strings.Format("DealerPrice", car.Price);
            SpecsLine = Strings.Format(
                "DealerCarSpecs", car.Class, car.Passengers, car.TrunkLitres,
                car.Horsepower, car.TopSpeedKmh);
        }
        else
        {
            SelectedLine = Strings.Get(IsSelling ? "DealerSellPick" : "DealerPick");
            SelectedPriceLine = string.Empty;
            SpecsLine = string.Empty;
        }
        // A pick or a mode swap clears the previous click's reason: the buttons
        // open on their verbs and re-explain the gate only when pressed again.
        SellLabel = Strings.Get("DealerSell");
        BuyLabel = Strings.Get("DealerBuy");
        RebuildVoice();
    }

    // The catalogue row the picked key resolves in the current mode: a floor row
    // must still be in this week's stock, an owned row must still be in the
    // garage. Null (and so "nothing picked") once a sale or a mode swap drops it.
    private CarSpec? ResolveSelectedSpec()
    {
        if (_selectedKey is not { } key)
        {
            return null;
        }
        if (!IsSelling)
        {
            return FindInStock(key);
        }
        OwnedCar? owned = _garage.Find(key);
        return owned is null ? null : _garage.SpecOf(owned);
    }

    private CarSpec? FindInStock(string key)
    {
        foreach (CarSpec car in _stock)
        {
            if (string.Equals(car.Key, key, StringComparison.Ordinal))
            {
                return car;
            }
        }
        return null;
    }

    private static double SaleValue(OwnedCar car, CarSpec spec) =>
        DealerSaleRules.Value(
            spec, car.Engine.BrokenPercent(spec.EngineCc, car.Odometer), car.LeaderboardRanking);

    private void PaintRows() =>
        Rows = IsSelling ? PaintOwned(_garage, _selectedKey) : Paint(_stock, _selectedKey);

    private static DealerRow[] Paint(IReadOnlyList<CarSpec> stock, string? selectedKey)
    {
        var rows = new DealerRow[stock.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            CarSpec car = stock[i];
            rows[i] = new DealerRow(
                car.Key,
                CarTitles.Of(car),
                string.Equals(selectedKey, car.Key, StringComparison.Ordinal));
        }
        return rows;
    }

    private static DealerRow[] PaintOwned(Garage garage, string? selectedKey)
    {
        var rows = new DealerRow[garage.Cars.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            OwnedCar car = garage.Cars[i];
            rows[i] = new DealerRow(
                car.Key,
                CarTitles.Of(car.Key, garage.SpecOf(car)),
                string.Equals(selectedKey, car.Key, StringComparison.Ordinal));
        }
        return rows;
    }

    // Each row's button text is the car name, so the name is the voice key:
    // picking a car stays on this page, so it acts at once like a click. BUY and
    // BACK navigate, so they glow first and act at the beat's end. SELL and the
    // mode swap stay on the page, so they act at once too; the gate labels stay
    // voiced and the action re-checks the click.
    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>(Rows.Count + 6);
        foreach (DealerRow row in Rows)
        {
            DealerRow hit = row;
            actions[hit.Title] = () => Select(hit);
        }
        actions[Strings.Get("DealerSell")] = Sell;
        if (IsSelling)
        {
            // The sell list is up: BUY returns to the floor and buys nothing.
            actions[Strings.Get("DealerBuy")] = Buy;
        }
        else
        {
            // One delegate per navigating button answers in either buy state (the
            // delivery page's phase-name precedent): CAN'T AFFORD still fires BUY,
            // which re-checks the click, so the label and the action never fork.
            Action buy = () => FlashThen(buy: true, Buy);
            actions[Strings.Get("DealerBuy")] = buy;
            actions[Strings.Get("DealerCantAfford")] = buy;
            actions[Strings.Get("DealerSellOnlyCar")] = Sell;
            actions[Strings.Get("DealerSellNoCars")] = Sell;
        }
        actions[Strings.Get("DeliveryBack")] = () => FlashThen(buy: false, Back);
        VoiceActions = actions;
    }

    // Navigating voice taps glow first, then act at the end of the shared beat
    // so the teal acknowledgment shows before the page change.
    private void FlashThen(bool buy, Action act)
    {
        BuyFlash = buy;
        BackFlash = !buy;
        _beat.Defer(act);
    }

    private void ClearVoiceFlash()
    {
        BuyFlash = false;
        BackFlash = false;
    }
}
