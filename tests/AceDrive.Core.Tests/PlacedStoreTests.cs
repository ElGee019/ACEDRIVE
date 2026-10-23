using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>A placed shop becomes a delivery-board row (Unity's store POIs).</summary>
public sealed class PlacedStoreTests
{
    private static PointOfInterest Store(
        string id, string type, string category, string name = "Marco's Grocery")
    {
        return new PointOfInterest(
            id, name, new AddressLocation("Street 1", 4, HouseSide.Left),
            type, "Marco Bianchi", category);
    }

    [Fact]
    public void GroceryStore_DerivesNameOwnerCategoryAndCapacity()
    {
        DeliveryStore? row = PlacedStore.Row(
            Store("grocery_store_0", PoiTypes.GroceryStore, "groceries"),
            StoreCatalog.Default);

        Assert.NotNull(row);
        Assert.Equal("grocery_store_0", row.Key);
        Assert.Equal("Marco's Grocery", row.Name);
        Assert.Equal("Marco Bianchi", row.Owner);
        Assert.Equal("groceries", row.Category);
        // The POI's door is the delivery destination: no second address table.
        Assert.Equal(new AddressLocation("Street 1", 4, HouseSide.Left), row.Address);
        // Default layout: 8 groceries + 2 coffee beans, 20 units per slot.
        Assert.Equal(200, row.UnitCapacity);
        Assert.Equal(200, row.StockedUnits);
        Assert.True(row.PricePerUnit > 0.0);
        Assert.Equal(0, row.UnitsNeeded); // placed stores open full
    }

    [Fact]
    public void CoffeeShop_DerivesItsOwnCategoryLayout()
    {
        var catalog = new StoreCatalog(
            [
                new StoreItem("BEANS", 10.0, 0.1, 0, "coffee_beans"),
                new StoreItem("LATTE", 4.0, 0.1, 0, "coffee_beans"),
            ],
            DefaultStock: 20);

        DeliveryStore? row = PlacedStore.Row(
            Store(
                "coffee_shop_0", PoiTypes.CoffeeShop, "coffee_beans",
                "Greta's Coffee Shop"),
            catalog);

        Assert.NotNull(row);
        Assert.Equal("coffee_beans", row.Category);
        Assert.Equal(40, row.UnitCapacity);
        Assert.Equal(7.0, row.PricePerUnit);
    }

    [Fact]
    public void MaxsStoryShop_IsTheRealGroceryRow_BusinessDoorsAreNot()
    {
        DeliveryStore? max = PlacedStore.Row(
            Store("max-grocery", PoiTypes.Grocery, "groceries", "Max's Grocery Store"),
            StoreCatalog.Default);
        Assert.NotNull(max);
        Assert.Equal("max-grocery", max.Key);
        Assert.Equal("groceries", max.Category);
        Assert.Null(PlacedStore.Row(
            Store("bank_0", PoiTypes.Bank, ""), StoreCatalog.Default));
    }

    [Fact]
    public void UnknownCategory_CannotBecomeABoardRow()
    {
        Assert.Null(PlacedStore.Row(
            Store("grocery_store_0", PoiTypes.GroceryStore, "medicine"),
            StoreCatalog.Default));
        Assert.Null(PlacedStore.Row(
            Store("grocery_store_0", PoiTypes.GroceryStore, ""),
            StoreCatalog.Default));
        Assert.Null(PlacedStore.Row(null, StoreCatalog.Default));
        Assert.Null(PlacedStore.Row(
            Store("grocery_store_0", PoiTypes.GroceryStore, "groceries"), null));
    }

    [Fact]
    public void Rows_KeepsEveryRealShopDoor_InDirectoryOrder()
    {
        var catalog = new StoreCatalog(
            [
                new StoreItem("BANANA", 2.0, 0.3, 4, "groceries"),
                new StoreItem("BEANS", 10.0, 0.1, 0, "coffee_beans"),
            ],
            DefaultStock: 20);
        var pois = new List<PointOfInterest>
        {
            Store("max-grocery", PoiTypes.Grocery, "groceries", "Max's Grocery Store"),
            Store("bank_0", PoiTypes.Bank, ""),
            Store("grocery_store_0", PoiTypes.GroceryStore, "groceries"),
            Store("coffee_shop_0", PoiTypes.CoffeeShop, "coffee_beans"),
        };

        IReadOnlyList<DeliveryStore> rows = PlacedStore.Rows(pois, catalog);

        Assert.Equal(["max-grocery", "grocery_store_0", "coffee_shop_0"],
            rows.Select(row => row.Key));
    }

    [Fact]
    public void WithStore_AppendsOnceAndKeepsTheExistingBoard()
    {
        var first = new DeliveryStore("a", "A", "Hahn", 1, 10, 1.0);
        var second = new DeliveryStore("b", "B", "Fuchs", 1, 10, 2.0);
        var board = new DeliveryCatalog([first], 0.7, 100);

        DeliveryCatalog grown = board.WithStore(second);

        Assert.Equal(["a", "b"], grown.Stores.Select(store => store.Key));
        Assert.Same(board, board.WithStore(second with { Key = "a" }));
        Assert.Equal(["a", "b"], board.WithStores([second, second]).Stores.Select(s => s.Key));
    }
}
