using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Shop checkout (Unity MaxsScreen.OnBuyItem): stock, cash and trunk
/// gates, and the one transaction that moves all three together.</summary>
public sealed class StoreCheckoutTests
{
    private static readonly StoreItem Maple = new(
        "MAPLE SYRUP", 45.99, 0.4, 3, "groceries");

    private static StoreInventory Shelf(int stock = 20) =>
        new("max-grocery", [Maple], stock);

    private static DriverWallet Wallet(double cash) =>
        new(new DriverState(cash, 0.0));

    [Fact]
    public void Buy_MovesCashStockAndTrunkTogether()
    {
        StoreInventory inventory = Shelf();
        var trunk = new Trunk();
        DriverWallet wallet = Wallet(100.0);

        StorePurchaseQuote quote = StoreCheckout.Buy(
            inventory, trunk, wallet, Maple, 2, 60.0);

        Assert.Equal(StorePurchaseStatus.Success, quote.Status);
        Assert.Equal(91.98, quote.TotalPrice, 2);
        Assert.Equal(0.8, quote.TotalVolume, 6);
        Assert.Equal(18, inventory.StockOf(Maple.Name));
        Assert.Equal(8.02, wallet.Cash, 2);
        Assert.Equal(0.8, trunk.UsedLitres, 6);
        Assert.Equal(59.2, trunk.Remaining(60.0), 6);
    }

    [Fact]
    public void Quote_ShortCash_LeavesEverythingUntouched()
    {
        StoreInventory inventory = Shelf();
        var trunk = new Trunk();
        DriverWallet wallet = Wallet(40.0);

        StorePurchaseQuote quote = StoreCheckout.Buy(
            inventory, trunk, wallet, Maple, 1, 60.0);

        Assert.Equal(StorePurchaseStatus.NotEnoughCash, quote.Status);
        Assert.Equal(20, inventory.StockOf(Maple.Name));
        Assert.Equal(40.0, wallet.Cash);
        Assert.Equal(0.0, trunk.UsedLitres);
    }

    [Fact]
    public void Quote_TrunkFull_LeavesEverythingUntouched()
    {
        StoreInventory inventory = Shelf();
        var trunk = new Trunk();
        DriverWallet wallet = Wallet(100.0);

        StorePurchaseQuote quote = StoreCheckout.Buy(
            inventory, trunk, wallet, Maple, 1, 0.2);

        Assert.Equal(StorePurchaseStatus.TrunkFull, quote.Status);
        Assert.Equal(20, inventory.StockOf(Maple.Name));
        Assert.Equal(100.0, wallet.Cash);
        Assert.Equal(0.0, trunk.UsedLitres);
    }

    [Fact]
    public void Quote_SoldOut_LeavesEverythingUntouched()
    {
        StoreInventory inventory = Shelf(stock: 0);
        var trunk = new Trunk();
        DriverWallet wallet = Wallet(100.0);

        StorePurchaseQuote quote = StoreCheckout.Buy(
            inventory, trunk, wallet, Maple, 1, 60.0);

        Assert.Equal(StorePurchaseStatus.NotEnoughStock, quote.Status);
        Assert.Equal(0, inventory.StockOf(Maple.Name));
        Assert.Equal(100.0, wallet.Cash);
    }

    [Fact]
    public void Quote_ZeroQuantity_IsInvalid()
    {
        StorePurchaseQuote quote = StoreCheckout.Quote(
            Shelf(), new Trunk(), Wallet(100.0), Maple, 0, 60.0);

        Assert.Equal(StorePurchaseStatus.InvalidQuantity, quote.Status);
    }

    [Fact]
    public void Quote_ProjectsTheTrunkAfterThePurchase()
    {
        var trunk = new Trunk();
        trunk.Add(Maple, 1);

        StorePurchaseQuote quote = StoreCheckout.Quote(
            Shelf(), trunk, Wallet(100.0), Maple, 2, 60.0);

        Assert.Equal(StorePurchaseStatus.Success, quote.Status);
        Assert.Equal(60.0 - 1.2, quote.ProjectedTrunkLitres, 6);
    }

    [Fact]
    public void Trunk_MergesTheSameItem()
    {
        var trunk = new Trunk();
        trunk.Add(Maple, 1);
        trunk.Add(Maple, 2);

        TrunkItem item = Assert.Single(trunk.Items);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(1.2, trunk.UsedLitres, 6);
    }

    [Fact]
    public void Trunk_ResumesAValidSavedStackAndKeepsAZeroVolumeKeepsake()
    {
        var trunk = new Trunk(
        [
            new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 2 },
            // A zero unit volume is a keepsake (the certificate path banks
            // one), not a corrupt row: it stays and weighs nothing.
            new TrunkItem("BROKEN", 0.0) { Quantity = 1 },
            new TrunkItem("EMPTY", 0.5) { Quantity = 0 },
        ]);

        Assert.Equal(2, trunk.Items.Count);
        TrunkItem item = trunk.Items[0];
        Assert.Equal("MAPLE SYRUP", item.Name);
        Assert.Equal(2, item.Quantity);
        Assert.Equal("BROKEN", trunk.Items[1].Name);
        Assert.Equal(0.0, trunk.Items[1].UnitVolume, 6);
        Assert.Equal(0.8, trunk.UsedLitres, 6);
    }

    [Fact]
    public void Trunk_SnapshotIsDetached()
    {
        var trunk = new Trunk();
        trunk.Add(Maple, 1);

        IReadOnlyList<TrunkItem> snapshot = trunk.Snapshot();
        trunk.Add(Maple, 1);

        Assert.Equal(1, Assert.Single(snapshot).Quantity);
        Assert.Equal(2, Assert.Single(trunk.Items).Quantity);
    }
}
