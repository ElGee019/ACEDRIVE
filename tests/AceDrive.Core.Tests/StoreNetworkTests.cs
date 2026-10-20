using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Shop shelf draw (Unity StorePlacementHelper.GenerateStoreItems):
/// layout counts, guaranteed story items, stable selection and the fallbacks.</summary>
public sealed class StoreNetworkTests
{
    private static readonly PointOfInterest Maxs = new(
        "max-grocery", "Max's Grocery Store",
        new AddressLocation("Street 1", 7, HouseSide.Right),
        PoiTypes.Grocery, "Max Silvestri", "groceries");

    private static StoreItem Grocery(string name) =>
        new(name, 5.0, 0.5, 5, "groceries");

    private static StoreItem Coffee(string name) =>
        new(name, 18.0, 0.5, 1, "coffee_beans");

    private static StoreCatalog CatalogWithLayout() => new(
        [
            .. Enumerable.Range(1, 10).Select(index => Grocery($"GROCERY {index}")),
            .. Enumerable.Range(1, 4).Select(index => Coffee($"COFFEE {index}")),
        ],
        DefaultStock: 20,
        Layouts: new Dictionary<string, IReadOnlyDictionary<string, int>>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["groceries"] = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["groceries"] = 8,
                ["coffee_beans"] = 2,
            },
        });

    [Fact]
    public void For_DrawsTheLayoutCounts()
    {
        var network = new StoreNetwork(CatalogWithLayout(), PoiCatalog.Default);

        StoreInventory inventory = network.For(Maxs);

        Assert.Equal(10, inventory.Shelves.Count);
        Assert.Equal(8, inventory.Shelves.Count(
            shelf => shelf.Item.Category == "groceries"));
        Assert.Equal(2, inventory.Shelves.Count(
            shelf => shelf.Item.Category == "coffee_beans"));
        Assert.All(inventory.Shelves, shelf => Assert.Equal(20, shelf.Quantity));
        Assert.Equal(
            inventory.Shelves.Count,
            inventory.Shelves.Select(shelf => shelf.Item.Name).Distinct().Count());
    }

    [Fact]
    public void For_GuaranteesTheStoryItemWhenTheLayoutMissesIt()
    {
        var catalog = new StoreCatalog(
            [
                Grocery("MAPLE SYRUP"),
                .. Enumerable.Range(1, 4).Select(index => Coffee($"COFFEE {index}")),
            ],
            DefaultStock: 20,
            Layouts: new Dictionary<string, IReadOnlyDictionary<string, int>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["groceries"] = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["coffee_beans"] = 2,
                },
            });
        var network = new StoreNetwork(catalog, PoiCatalog.Default);

        StoreInventory inventory = network.For(Maxs);

        Assert.Equal(3, inventory.Shelves.Count);
        Assert.Contains(inventory.Shelves, shelf => shelf.Item.Name == "MAPLE SYRUP");
    }

    [Fact]
    public void For_SamePoi_ReturnsTheSameInventoryAndShelfOrder()
    {
        var network = new StoreNetwork(CatalogWithLayout(), PoiCatalog.Default);

        StoreInventory first = network.For(Maxs);
        StoreInventory second = network.For(Maxs);

        Assert.Same(first, second);
        Assert.Equal(
            first.Shelves.Select(shelf => shelf.Item.Name),
            second.Shelves.Select(shelf => shelf.Item.Name));
    }

    [Fact]
    public void For_KeepsBoughtStockOnReentry()
    {
        var network = new StoreNetwork(CatalogWithLayout(), PoiCatalog.Default);
        StoreInventory inventory = network.For(Maxs);
        string item = inventory.Shelves[0].Item.Name;

        Assert.True(inventory.TryTake(item, 3));

        Assert.Equal(17, network.For(Maxs).StockOf(item));
    }

    [Fact]
    public void For_UnknownCategory_FallsBackToEveryItem()
    {
        var network = new StoreNetwork(CatalogWithLayout(), PoiCatalog.Default);
        var unknown = Maxs with { Category = "fuelstation" };

        StoreInventory inventory = network.For(unknown);

        Assert.Equal(14, inventory.Shelves.Count);
    }

    [Fact]
    public void For_NoLayout_UsesEveryItemInTheCategory()
    {
        var catalog = new StoreCatalog(
            [Grocery("BANANA"), Grocery("MAPLE SYRUP"), Coffee("COFFEE")],
            DefaultStock: 20);
        var network = new StoreNetwork(catalog, PoiCatalog.Default);

        StoreInventory inventory = network.For(Maxs);

        Assert.Equal(2, inventory.Shelves.Count);
        Assert.All(
            inventory.Shelves,
            shelf => Assert.Equal("groceries", shelf.Item.Category));
    }
}
