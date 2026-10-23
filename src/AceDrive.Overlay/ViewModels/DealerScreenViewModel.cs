using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The car dealer's weekly floor (PORTPLAN item 29, port of Unity CarDealerScreen):
/// this week's stock, the selected car's specs and the buy button. Stock is drawn
/// once per visit from the current week (DealerStock, item 29) and never persists;
/// owning a car removes it from every later floor. Unity parity: money is the only
/// gate (no slot cap, no class lock, no trade-in), the buy button names the reason
/// it cannot be used, and the car is owned the moment the cash moves — the
/// follow-up choice to drive it lives on BoughtFirstCarScreen. The dealer's
/// familiarity bump waits for the POI owner (items 24/59), like the bank's.
/// Screen writes nothing itself: the host banks the purchase.
/// Voice parity with the other pages: a car name (the row's button text) picks
/// the car at once; BUY glows first since the follow-up page replaces this one,
/// and BACK glows and navigates.
/// </summary>
public sealed class DealerScreenViewModel : ViewModelBase, IVoiceScreen, IPoiScreen
{
    private readonly IReadOnlyList<CarSpec> _catalog;
    private readonly Garage _garage;
    private readonly DriverWallet _wallet;
    private readonly int _week;
    private readonly ICommand _backCommand;
    private readonly Action<CarSpec> _onBought;
    private readonly VoiceBeat _beat;
    private readonly RelayCommand _buy;
    // This week's floor. The rows below are the same data preformatted for the
    // view, so the raw list stays private to the page.
    private IReadOnlyList<CarSpec> _stock = [];
    private CarSpec? _selected;

    public DealerScreenViewModel(
        IReadOnlyList<CarSpec> catalog, Garage garage, DriverWallet wallet, int week,
        ICommand backCommand, Action<CarSpec> onBought, VoiceBeat beat)
    {
        _catalog = catalog;
        _garage = garage;
        _wallet = wallet;
        _week = week;
        _backCommand = backCommand;
        _onBought = onBought;
        _beat = beat;
        BackCommand = new RelayCommand(Back);
        SelectCommand = new ParameterizedCommand<DealerRow>(Select);
        _buy = new RelayCommand(Buy, CanBuy);
        BuyCommand = _buy;
        _stock = DealerStock.Build(catalog, garage.Keys, week);
        Rows = Paint(_stock, null);
        Reload();
    }

    public ICommand BackCommand { get; }

    /// <summary>Picks a car: the row highlights and the preview follows.</summary>
    public ICommand SelectCommand { get; }

    public ICommand BuyCommand { get; }

    /// <summary>The numbered rows answer to their spoken word ("ONE", "TWO", …):
    /// selecting a car stays on this page, so a tap acts at once like a click.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

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

    /// <summary>The picked car's price, shown below the list; empty with no pick.</summary>
    public string SelectedPriceLine { get => _selectedPriceLine; private set => SetField(ref _selectedPriceLine, value); }
    private string _selectedPriceLine = "";

    /// <summary>Unity's buy label: BUY IT while affordable, CAN'T AFFORD while the
    /// cash is short, BUY IT again with nothing picked (the button is disabled then).</summary>
    public string BuyLabel { get => _buyLabel; private set => SetField(ref _buyLabel, value); }
    private string _buyLabel = "";

    /// <summary>True when a car is picked but the wallet is short: the bar keeps
    /// the CAN'T AFFORD note as plain text, with no button outline (nothing to
    /// tap), Unity's disabled-buy look.</summary>
    public bool CantAfford { get => _cantAfford; private set => SetField(ref _cantAfford, value); }
    private bool _cantAfford;

    /// <summary>Everything this player could still buy has been bought: the floor is empty.</summary>
    public bool IsEmpty => Rows.Count == 0;

    public string EmptyLine { get; } = Strings.Get("DealerEmpty");

    public string PickLine { get; } = Strings.Get("DealerPick");

    // Unity OnCarSelected: rows repaint (the selected one highlights), the preview
    // and the buy state follow the pick.
    private void Select(DealerRow? row)
    {
        _selected = row is null ? null : FindInStock(row.Key);
        Rows = Paint(_stock, _selected);
        Reload();
    }

    // Unity OnBuyClicked: affordability is re-checked here, not just painted, so a
    // stale command state can never spend. A short wallet leaves the floor alone
    // (Unity returns without buying). The purchase is the cash move plus the garage
    // entry; the host banks it and shows the follow-up choice.
    private void Buy()
    {
        // A click or the voice tap's deferred end both land here, so the glow
        // clears on the way out — a click during a pending tap leaves no teal.
        ClearVoiceFlash();
        if (_selected is not { } car || !CanBuy())
        {
            return;
        }
        if (!_wallet.TrySpend(car.Price))
        {
            return;
        }
        _garage.Add(car, _week);
        // Unity keeps the bought row on screen with the button hidden; the overlay
        // drops it instead and repaints from the same rule that would exclude it on
        // the next visit. The host navigates to the follow-up screen either way.
        _stock = DealerStock.Build(_catalog, _garage.Keys, _week);
        _selected = null;
        Rows = Paint(_stock, null);
        Reload();
        OnPropertyChanged(nameof(IsEmpty));
        _onBought(car);
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out (bank-page precedent).
    private void Back()
    {
        ClearVoiceFlash();
        _backCommand.Execute(null);
    }

    private bool CanBuy() => _selected is { } car && _wallet.Cash >= car.Price;

    private void Reload()
    {
        CashLine = Strings.Format("DealerCash", _wallet.Cash);
        if (_selected is { } car)
        {
            SelectedLine = CarTitles.Of(car);
            SelectedPriceLine = Strings.Format("DealerPrice", car.Price);
            SpecsLine = Strings.Format(
                "DealerCarSpecs", car.Class, car.Passengers, car.TrunkLitres,
                car.Horsepower, car.TopSpeedKmh);
            bool affordable = _wallet.Cash >= car.Price;
            BuyLabel = Strings.Get(affordable ? "DealerBuy" : "DealerCantAfford");
            CantAfford = !affordable;
        }
        else
        {
            SelectedLine = PickLine;
            SelectedPriceLine = string.Empty;
            SpecsLine = string.Empty;
            BuyLabel = Strings.Get("DealerBuy");
            CantAfford = false;
        }
        _buy.RaiseCanExecuteChanged();
        RebuildVoice();
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

    private static DealerRow[] Paint(IReadOnlyList<CarSpec> stock, CarSpec? selected)
    {
        var rows = new DealerRow[stock.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            CarSpec car = stock[i];
            rows[i] = new DealerRow(
                car.Key,
                CarTitles.Of(car),
                selected is not null && string.Equals(selected.Key, car.Key, StringComparison.Ordinal));
        }
        return rows;
    }

    // Each row's button text is the car name, so the name is the voice key:
    // picking a car stays on this page, so it acts at once like a click. BUY and
    // BACK navigate, so they glow first and act at the beat's end. Rows are
    // rebuilt on every paint, so the actions follow the current floor.
    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>(Rows.Count + 3);
        foreach (DealerRow row in Rows)
        {
            DealerRow hit = row;
            actions[hit.Title] = () => Select(hit);
        }
        // One delegate per navigating button answers in either buy state (the
        // delivery page's phase-name precedent): CAN'T AFFORD still fires BUY,
        // which re-checks affordability, so the label and the action never fork.
        Action buy = () => FlashThen(buy: true, Buy);
        Action back = () => FlashThen(buy: false, Back);
        actions[Strings.Get("DealerBuy")] = buy;
        actions[Strings.Get("DealerCantAfford")] = buy;
        actions[Strings.Get("DeliveryBack")] = back;
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
