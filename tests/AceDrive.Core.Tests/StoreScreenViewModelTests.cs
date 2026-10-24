using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The shop page (Unity MaxsScreen + GrocerySelection): rows,
/// selection quote, checkout, sold-out shelves, friendship and BACK.</summary>
public sealed class StoreScreenViewModelTests
{
    private static readonly StoreItem Maple =
        new("MAPLE SYRUP", 45.99, 0.4, 3, "groceries");

    private static readonly StoreItem Banana =
        new("BANANA", 2.0, 0.3, 4, "groceries");

    private static readonly PointOfInterest Maxs = new(
        "max-grocery", "Max's Grocery Store",
        new AddressLocation("Street 1", 7, HouseSide.Right),
        PoiTypes.Grocery, "Max Silvestri", "groceries");

    private static (
        StoreScreenViewModel Vm, StoreInventory Inventory, DriverWallet Wallet,
        Trunk Trunk, FakeFriendshipStore Friends, List<string> Events)
        Create(
            double cash = 1000.0, int stock = 20, int trunkLitres = 60,
            IReadOnlyList<StoreItem>? items = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture =
            new CultureInfo("en-US");
        var inventory = new StoreInventory(
            "max-grocery", items ?? [Maple, Banana], stock);
        var wallet = new DriverWallet(new DriverState(cash, 0.0));
        var trunk = new Trunk();
        var friends = new FakeFriendshipStore();
        var car = new CarSpec(
            "ks_test_grocery", 2020, "Test", "Grocery", TrunkLitres: trunkLitres);
        var garage = new Garage([car]);
        garage.Add(car, week: 1);
        var events = new List<string>();
        var back = new RelayCommand(() => events.Add("back"));
        var vm = new StoreScreenViewModel(
            Maxs, inventory, trunk, wallet, garage, friends,
            new VoiceBeat(new FakeClock()), back, () => events.Add("purchase"));
        return (vm, inventory, wallet, trunk, friends, events);
    }

    private static StoreItemRowViewModel Row(StoreScreenViewModel vm, string name) =>
        vm.Items.First(item => item.Item.Name == name);

    [Fact]
    public void Constructor_ShowsTheStoreIdentityAndEmptySelection()
    {
        var (vm, _, _, _, _, _) = Create();

        Assert.Equal("MAX'S GROCERY STORE", vm.StoreTitle);
        Assert.Equal("\"Hello there, what can I get you today?\"", vm.Greeting);
        Assert.Equal("Your Cash: 1000.00CR", vm.CashLine);
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("hunger -3", Row(vm, "MAPLE SYRUP").HungerLine);
        Assert.Equal("CR45.99", Row(vm, "MAPLE SYRUP").PriceLine);
        Assert.Equal("0", vm.QuantityText);
        Assert.Equal("", vm.SelectionText);
        Assert.False(vm.BuyVisible);
        Assert.False(vm.BuyCommand.CanExecute(null));
    }

    [Fact]
    public void Select_StartsAtOneAndPaintsTheQuote()
    {
        var (vm, _, _, _, _, _) = Create();

        Row(vm, "MAPLE SYRUP").SelectCommand.Execute(null);

        Assert.True(Row(vm, "MAPLE SYRUP").IsSelected);
        Assert.Equal("1", vm.QuantityText);
        Assert.Equal("  x  MAPLE SYRUP", vm.SelectionText);
        Assert.Equal("VOLUME: 0.40L", vm.VolumeText);
        Assert.Equal("PRICE: CR45.99", vm.PriceText);
        Assert.Equal("TRUNK: 59.60L free", vm.TrunkText);
        Assert.True(vm.BuyVisible);
        Assert.True(vm.BuyCommand.CanExecute(null));
    }

    [Fact]
    public void ChangeQuantity_ClampsToZeroAndStock()
    {
        var (vm, _, _, _, _, _) = Create(stock: 3);
        Row(vm, "MAPLE SYRUP").SelectCommand.Execute(null);

        vm.IncreaseCommand.Execute(null);
        vm.IncreaseCommand.Execute(null);
        vm.IncreaseCommand.Execute(null);

        Assert.Equal("3", vm.QuantityText);
        vm.IncreaseCommand.Execute(null);
        Assert.Equal("3", vm.QuantityText);

        vm.DecreaseCommand.Execute(null);
        vm.DecreaseCommand.Execute(null);
        vm.DecreaseCommand.Execute(null);
        Assert.Equal("0", vm.QuantityText);
        Assert.False(vm.BuyCommand.CanExecute(null));
    }

    [Fact]
    public void Buy_DeductsCashStockAndAddsTrunk()
    {
        var (vm, inventory, wallet, trunk, friends, events) = Create();
        Row(vm, "MAPLE SYRUP").SelectCommand.Execute(null);

        vm.BuyCommand.Execute(null);

        Assert.Equal(954.01, wallet.Cash, 2);
        Assert.Equal(19, inventory.StockOf("MAPLE SYRUP"));
        Assert.Equal(0.4, trunk.UsedLitres, 6);
        Assert.Equal("Your Cash: 954.01CR", vm.CashLine);
        Assert.Equal("\"Anything else?\"", vm.Greeting);
        Assert.Equal("You have bought 1 x MAPLE SYRUP", vm.BoughtText);
        Assert.True(vm.BoughtVisible);
        Assert.Equal(1, friends.GetLevel("Max Silvestri"));
        Assert.Equal(
            "New familiarity level with Max: 1. Still a stranger though.",
            vm.FriendshipText);
        Assert.True(vm.FriendshipVisible);
        Assert.Contains("purchase", events);
        Assert.Equal("0", vm.QuantityText);
        Assert.Equal("", vm.SelectionText);
        Assert.False(vm.BuyVisible);
        Assert.False(vm.BuyCommand.CanExecute(null));
    }

    [Fact]
    public void Buy_HidesTheButtonUntilTheNextSelection()
    {
        var (vm, _, _, _, _, _) = Create();
        Row(vm, "MAPLE SYRUP").SelectCommand.Execute(null);
        Assert.True(vm.BuyVisible);

        vm.BuyCommand.Execute(null);

        // The purchase clears the selection, so the button that would quote
        // nothing leaves the page until another row is picked.
        Assert.False(vm.BuyVisible);
        Assert.False(vm.BuyCommand.CanExecute(null));

        Row(vm, "BANANA").SelectCommand.Execute(null);

        Assert.True(vm.BuyVisible);
        Assert.True(vm.BuyCommand.CanExecute(null));
    }

    [Fact]
    public void Buy_BumpsFriendshipOnlyOncePerVisit()
    {
        var (vm, _, _, _, friends, _) = Create();
        Row(vm, "MAPLE SYRUP").SelectCommand.Execute(null);
        vm.BuyCommand.Execute(null);

        Row(vm, "BANANA").SelectCommand.Execute(null);
        vm.BuyCommand.Execute(null);

        Assert.Equal(1, friends.GetLevel("Max Silvestri"));
        Assert.Single(friends.Deltas);
    }

    [Fact]
    public void Buy_ShortCash_LeavesTheStateUntouched()
    {
        var (vm, inventory, wallet, trunk, _, events) = Create(cash: 10.0);
        Row(vm, "MAPLE SYRUP").SelectCommand.Execute(null);

        Assert.True(vm.PriceWarn);
        Assert.False(vm.BuyCommand.CanExecute(null));
        vm.BuyCommand.Execute(null);

        Assert.Equal(10.0, wallet.Cash);
        Assert.Equal(20, inventory.StockOf("MAPLE SYRUP"));
        Assert.Equal(0.0, trunk.UsedLitres);
        Assert.DoesNotContain("purchase", events);
    }

    [Fact]
    public void Buy_TrunkFull_DisablesBuyAndShowsOverflow()
    {
        var (vm, _, _, _, _, _) = Create(trunkLitres: 0);
        Row(vm, "MAPLE SYRUP").SelectCommand.Execute(null);

        Assert.True(vm.VolumeWarn);
        Assert.True(vm.TrunkOverflow);
        Assert.Equal("TRUNK: -0.40L (OVERFLOW)", vm.TrunkText);
        Assert.False(vm.BuyCommand.CanExecute(null));
    }

    [Fact]
    public void SoldOutRow_ShowsTheLabelAndCannotBeSelected()
    {
        var (vm, _, _, _, _, _) = Create(stock: 0);
        StoreItemRowViewModel row = Row(vm, "MAPLE SYRUP");

        Assert.True(row.IsSoldOut);
        Assert.Equal("Sold Out", row.Name);
        row.SelectCommand.Execute(null);

        Assert.False(row.IsSelected);
        Assert.Equal("", vm.SelectionText);
        Assert.False(vm.BuyVisible);
        Assert.False(vm.BuyCommand.CanExecute(null));
    }

    [Fact]
    public void BuyingTheLastUnit_RebuildsTheRowAsSoldOut()
    {
        var (vm, _, _, _, _, _) = Create(stock: 1);
        Row(vm, "MAPLE SYRUP").SelectCommand.Execute(null);

        vm.BuyCommand.Execute(null);

        StoreItemRowViewModel row = Row(vm, "MAPLE SYRUP");
        Assert.True(row.IsSoldOut);
        Assert.Equal("Sold Out", row.Name);
    }

    [Fact]
    public void Back_ExecutesTheHostCommand()
    {
        var (vm, _, _, _, _, events) = Create();

        vm.BackCommand.Execute(null);

        Assert.Contains("back", events);
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void VoiceActions_ExposeBuyAndBack()
    {
        var (vm, _, wallet, _, _, _) = Create();

        Assert.True(vm.VoiceActions.ContainsKey("BUY"));
        Assert.True(vm.VoiceActions.ContainsKey("BACK"));

        vm.VoiceActions["BUY"]();
        Assert.Equal(1000.0, wallet.Cash);

        Row(vm, "MAPLE SYRUP").SelectCommand.Execute(null);
        vm.VoiceActions["BUY"]();
        Assert.Equal(954.01, wallet.Cash, 2);

        vm.VoiceActions["BACK"]();
        Assert.True(vm.BackFlash);
    }
}
