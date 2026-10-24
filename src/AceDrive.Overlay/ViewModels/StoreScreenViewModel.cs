using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The shop interior (PORTPLAN items 24/26, port of Unity MaxsScreen +
/// GrocerySelection). It renders inside the shared overlay backdrop as a
/// CurrentScreen, never as a separate window. The page owns only
/// presentation state; shelves, trunk and wallet are shared Core state, and
/// the host persists after a purchase. Store names, items and prices come
/// from data; all copy lives in the resx files.
/// </summary>
public sealed class StoreScreenViewModel :
    ViewModelBase, IPoiScreen, IJobsScreen, IVoiceScreen
{
    // Same bands as Unity's MaxsScreen friendship notification. The index
    // is FriendshipLevels.GetTier, so the copy and the receipt tiers cannot
    // drift apart.
    private static readonly string[] FriendshipTierKeys =
    [
        "StoreFriendshipStranger",
        "StoreFriendshipAcquaintance",
        "StoreFriendshipFriend",
        "StoreFriendshipGoodFriend",
        "StoreFriendshipCloseFriend",
        "StoreFriendshipBff",
    ];

    private readonly PointOfInterest _poi;
    private readonly StoreInventory _inventory;
    private readonly Trunk _trunk;
    private readonly DriverWallet _wallet;
    private readonly Garage _garage;
    private readonly IFriendshipStore _friendships;
    private readonly VoiceBeat _beat;
    private readonly ICommand _backCommand;
    private readonly Action _onPurchased;
    private readonly RelayCommand _buy;

    private StoreItemRowViewModel? _selected;
    private int _quantity;
    private bool _friendshipAdded;
    private bool _buyAllowed;

    public StoreScreenViewModel(
        PointOfInterest poi, StoreInventory inventory, Trunk trunk,
        DriverWallet wallet, Garage garage, IFriendshipStore friendships,
        VoiceBeat beat, ICommand backCommand, Action onPurchased)
    {
        _poi = poi;
        _inventory = inventory;
        _trunk = trunk;
        _wallet = wallet;
        _garage = garage;
        _friendships = friendships;
        _beat = beat;
        _backCommand = backCommand;
        _onPurchased = onPurchased;

        Items = [];
        IncreaseCommand = new RelayCommand(() => ChangeQuantity(1));
        DecreaseCommand = new RelayCommand(() => ChangeQuantity(-1));
        _buy = new RelayCommand(Buy, () => _buyAllowed);
        BuyCommand = _buy;
        BackCommand = new RelayCommand(Back);

        Greeting = Strings.Get("StoreGreeting");
        ReloadCash();
        RebuildItems();
        RepaintSelection();
        RebuildVoice();
    }

    /// <summary>The POI name in Unity's display case ("MAX'S GROCERY STORE").</summary>
    public string StoreTitle => _poi.Name.ToUpperInvariant();

    public ObservableCollection<StoreItemRowViewModel> Items { get; }

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }
    public ICommand BuyCommand { get; }
    public ICommand BackCommand { get; }

    /// <summary>"Hello there…" / "Anything else?" after a purchase.</summary>
    public string Greeting
    {
        get => _greeting;
        private set => SetField(ref _greeting, value);
    }
    private string _greeting = "";

    /// <summary>"Your Cash: {0:0.00}CR", refreshed after every purchase.</summary>
    public string CashLine
    {
        get => _cashLine;
        private set => SetField(ref _cashLine, value);
    }
    private string _cashLine = "";

    /// <summary>"  x  ITEM" for the current selection; empty with no selection.</summary>
    public string SelectionText
    {
        get => _selectionText;
        private set => SetField(ref _selectionText, value);
    }
    private string _selectionText = "";

    public string QuantityText
    {
        get => _quantityText;
        private set => SetField(ref _quantityText, value);
    }
    private string _quantityText = "0";

    /// <summary>"VOLUME: {total}L"; teal when the projection overflows.</summary>
    public string VolumeText
    {
        get => _volumeText;
        private set => SetField(ref _volumeText, value);
    }
    private string _volumeText = "";

    /// <summary>"PRICE: CR{total}"; teal when the cash is short.</summary>
    public string PriceText
    {
        get => _priceText;
        private set => SetField(ref _priceText, value);
    }
    private string _priceText = "";

    /// <summary>"TRUNK: {projected}L free" or "… (OVERFLOW)".</summary>
    public string TrunkText
    {
        get => _trunkText;
        private set => SetField(ref _trunkText, value);
    }
    private string _trunkText = "";

    /// <summary>
    /// True while a row is selected and BUY is offered. Hidden after a
    /// purchase (which clears the selection) until the player picks an item
    /// again, so the button can never quote nothing.
    /// </summary>
    public bool BuyVisible
    {
        get => _buyVisible;
        private set => SetField(ref _buyVisible, value);
    }
    private bool _buyVisible;

    public bool VolumeWarn
    {
        get => _volumeWarn;
        private set => SetField(ref _volumeWarn, value);
    }
    private bool _volumeWarn;

    public bool PriceWarn
    {
        get => _priceWarn;
        private set => SetField(ref _priceWarn, value);
    }
    private bool _priceWarn;

    public bool TrunkOverflow
    {
        get => _trunkOverflow;
        private set => SetField(ref _trunkOverflow, value);
    }
    private bool _trunkOverflow;

    /// <summary>"You have bought N x item", shown after a purchase.</summary>
    public string BoughtText
    {
        get => _boughtText;
        private set => SetField(ref _boughtText, value);
    }
    private string _boughtText = "";

    public bool BoughtVisible
    {
        get => _boughtVisible;
        private set => SetField(ref _boughtVisible, value);
    }
    private bool _boughtVisible;

    /// <summary>The once-per-visit familiarity notification.</summary>
    public string FriendshipText
    {
        get => _friendshipText;
        private set => SetField(ref _friendshipText, value);
    }
    private string _friendshipText = "";

    public bool FriendshipVisible
    {
        get => _friendshipVisible;
        private set => SetField(ref _friendshipVisible, value);
    }
    private bool _friendshipVisible;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash
    {
        get => _backFlash;
        private set => SetField(ref _backFlash, value);
    }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void Select(StoreItemRowViewModel row)
    {
        if (row.IsSoldOut)
        {
            return;
        }

        // Unity starts every fresh selection at one and hides the previous
        // bought confirmation; the friendship notice stays for the visit.
        foreach (StoreItemRowViewModel item in Items)
        {
            item.IsSelected = ReferenceEquals(item, row);
        }
        _selected = row;
        _quantity = 1;
        BoughtVisible = false;
        RepaintSelection();
    }

    private void ChangeQuantity(int delta)
    {
        if (_selected is not { } row)
        {
            return;
        }

        int stock = _inventory.StockOf(row.Item.Name);
        _quantity = Math.Clamp(_quantity + delta, 0, Math.Max(0, stock));
        RepaintSelection();
    }

    // Unity MaxsScreen.OnBuyItem: the quote's status is the gate, the same
    // quote the panel painted; a stale command therefore never spends.
    private void Buy()
    {
        if (_selected is not { } row)
        {
            return;
        }

        StorePurchaseQuote quote = StoreCheckout.Buy(
            _inventory, _trunk, _wallet, row.Item, _quantity, TrunkCapacity);
        if (quote.Status != StorePurchaseStatus.Success)
        {
            return;
        }

        ReloadCash();
        Greeting = Strings.Get("StoreGreetingAfter");
        BoughtText = Strings.Format("StoreBought", quote.Quantity, row.Item.Name);
        BoughtVisible = true;
        RegisterFriendship();

        // Unity resets the selection after a purchase so the player picks a
        // fresh item; sold-out shelves repaint from the updated stock.
        RebuildItems();
        RepaintSelection();
        _onPurchased();
    }

    private void RegisterFriendship()
    {
        if (_friendshipAdded || string.IsNullOrWhiteSpace(_poi.OwnerName))
        {
            return;
        }

        _friendships.AddFamiliarity(_poi.OwnerName, 1);
        int level = _friendships.GetLevel(_poi.OwnerName);
        string firstName = FriendshipLevels.DisplayFirstName(_poi.OwnerName);
        int tier = Math.Clamp(
            FriendshipLevels.GetTier(level), 0, FriendshipTierKeys.Length - 1);
        string tail = Strings.Format(FriendshipTierKeys[tier], firstName);
        FriendshipText = Strings.Format("StoreFriendship", firstName, level, tail);
        FriendshipVisible = true;
        _friendshipAdded = true;
    }

    private void RebuildItems()
    {
        Items.Clear();
        foreach (StoreShelf shelf in _inventory.Shelves)
        {
            Items.Add(new StoreItemRowViewModel(
                shelf.Item, shelf.Quantity, Select));
        }
        _selected = null;
        _quantity = 0;
    }

    // One repaint path for "no selection", a quantity change and a purchase,
    // so the panel numbers, BUY's visibility and the BUY gate always describe
    // the same quote.
    private void RepaintSelection()
    {
        double capacity = TrunkCapacity;
        if (_selected is not { } row)
        {
            BuyVisible = false;
            SelectionText = string.Empty;
            QuantityText = 0.ToString(CultureInfo.CurrentCulture);
            VolumeText = Strings.Format("StoreVolumeLine", 0.0);
            PriceText = Strings.Format("StorePriceLine", 0.0);
            TrunkText = Strings.Format("StoreTrunkFree", _trunk.Remaining(capacity));
            VolumeWarn = false;
            PriceWarn = false;
            TrunkOverflow = false;
            SetBuyAllowed(false);
            return;
        }

        BuyVisible = true;
        StorePurchaseQuote quote = StoreCheckout.Quote(
            _inventory, _trunk, _wallet, row.Item, _quantity, capacity);
        SelectionText = Strings.Format(
            "StoreSelection", row.Item.Name.ToUpperInvariant());
        QuantityText = _quantity.ToString(CultureInfo.CurrentCulture);
        VolumeText = Strings.Format("StoreVolumeLine", quote.TotalVolume);
        PriceText = Strings.Format("StorePriceLine", quote.TotalPrice);

        bool overflow = quote.ProjectedTrunkLitres < 0.0;
        TrunkText = overflow
            ? Strings.Format("StoreTrunkOverflow", quote.ProjectedTrunkLitres)
            : Strings.Format("StoreTrunkFree", quote.ProjectedTrunkLitres);
        VolumeWarn = overflow;
        PriceWarn = quote.TotalPrice > _wallet.Cash;
        TrunkOverflow = overflow;
        SetBuyAllowed(quote.Status == StorePurchaseStatus.Success);
    }

    private void SetBuyAllowed(bool allowed)
    {
        if (_buyAllowed == allowed)
        {
            return;
        }
        _buyAllowed = allowed;
        _buy.RaiseCanExecuteChanged();
    }

    private void ReloadCash() =>
        CashLine = Strings.Format("StoreCash", _wallet.Cash);

    private double TrunkCapacity =>
        _garage.Current is { } car ? _garage.SpecOf(car)?.TrunkLitres ?? 0 : 0;

    // One path back: a click and the voice tap's deferred end both land here,
    // so the glow clears on the way out (bank-page precedent).
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            // BUY stays on this page: it acts at once like a click.
            [Strings.Get("StoreBuy")] = Buy,
            // BACK navigates: glow first, act at the end of the shared beat.
            [Strings.Get("StoreBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
