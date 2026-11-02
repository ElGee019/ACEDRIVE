using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The care-package store draw (PORTPLAN 21): only a shop door on a discovered
/// street may hold a kit, and the pick is the caller's seeded stream.
/// </summary>
public sealed class CarePackageStoresTests
{
    private static PointOfInterest Door(
        string id, string type, string street, int number) =>
        new(id, id, new AddressLocation(street, number, HouseNumberPlan.SideOf(number)), type);

    [Fact]
    public void Eligible_KeepsStoreDoorsOnDiscoveredStreets()
    {
        IReadOnlyList<PointOfInterest> stores = CarePackageStores.Eligible(
            [
                Door("groceries_store", PoiTypes.GroceryStore, "Street 1", 2),
                Door("coffee_shop", PoiTypes.CoffeeShop, "Street 1", 3),
                Door("bank", PoiTypes.Bank, "Street 1", 4),
                Door("far_shop", PoiTypes.GroceryStore, "Nowhere", 5),
            ],
            AddressMap.Default);

        Assert.Equal(2, stores.Count);
        Assert.Equal("groceries_store", stores[0].Id);
        Assert.Equal("coffee_shop", stores[1].Id);
    }

    [Fact]
    public void Pick_UsesTheSeededStream()
    {
        IReadOnlyList<PointOfInterest> stores =
        [
            Door("groceries_store", PoiTypes.GroceryStore, "Street 1", 2),
            Door("coffee_shop", PoiTypes.CoffeeShop, "Street 1", 3),
        ];

        PointOfInterest? first = CarePackageStores.Pick(stores, new Random(0).Next);
        PointOfInterest? second = CarePackageStores.Pick(stores, new Random(0).Next);

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Null(CarePackageStores.Pick([], new Random(0).Next));
    }
}
