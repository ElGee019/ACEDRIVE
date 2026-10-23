using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The progressive business POIs (PORTPLAN 24, Unity generators).</summary>
public sealed class PoiSpawnerTests
{
    private static PoiSpawner Spawner(int seed = 0) => new(
        AddressMap.Default, PoiCatalog.Default,
        new ProceduralNameGenerator(NamePoolTable.Default, seed), seed);

    [Fact]
    public void NewCareer_HasNoBusinessDoor()
    {
        Assert.Empty(Spawner().Spawn(0, 0, new PoiDirectory()));
        // The home street alone opens nothing; a new game starts on story
        // doors only.
        Assert.Empty(Spawner().Spawn(1, 0, new PoiDirectory()));
    }

    [Fact]
    public void SixStreets_OpenTheCarWash()
    {
        PointOfInterest poi = Assert.Single(
            Spawner().Spawn(6, 0, new PoiDirectory()));

        Assert.Equal(PoiTypes.CarWash, poi.Type);
        Assert.StartsWith("carwash_", poi.Id, StringComparison.Ordinal);
        Assert.EndsWith("'s Carwash", poi.Name, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(poi.OwnerName));
        Assert.True(poi.Address.StreetName is "Street 1" or "Street 2");
        Assert.InRange(poi.Address.HouseNumber, 1, 33);
    }

    [Fact]
    public void StreetThresholds_OpenInsuranceAtThirteenAndBankAtFifteen()
    {
        Assert.Equal(
            [PoiTypes.CarWash, PoiTypes.Insurance],
            Spawner().Spawn(13, 0, new PoiDirectory()).Select(poi => poi.Type));

        Assert.Equal(
            [PoiTypes.CarWash, PoiTypes.Insurance, PoiTypes.Bank],
            Spawner().Spawn(15, 0, new PoiDirectory()).Select(poi => poi.Type));
    }

    [Fact]
    public void TenJobs_OpenTheDealer()
    {
        Assert.DoesNotContain(
            Spawner().Spawn(1, 9, new PoiDirectory()),
            poi => poi.Type == PoiTypes.Dealer);

        IReadOnlyList<PointOfInterest> grown =
            Spawner().Spawn(1, 10, new PoiDirectory());

        PointOfInterest dealer = Assert.Single(grown, poi => poi.Type == PoiTypes.Dealer);
        Assert.EndsWith("'s Cars", dealer.Name, StringComparison.Ordinal);
        Assert.Equal(PoiTypes.Dealer, dealer.Type);
    }

    [Fact]
    public void ThirtySixJobs_OpenTheMechanic_ThenTheStreetIntervalAddsOne()
    {
        Assert.DoesNotContain(
            Spawner().Spawn(10, 35, new PoiDirectory()),
            poi => poi.Type == PoiTypes.Mechanic);

        Assert.Single(
            Spawner().Spawn(3, 36, new PoiDirectory()),
            poi => poi.Type == PoiTypes.Mechanic);

        Assert.Equal(
            2,
            Spawner().Spawn(39, 36, new PoiDirectory())
                .Count(poi => poi.Type == PoiTypes.Mechanic));
    }

    [Fact]
    public void MultipleIntervals_CatchUpInOneCall()
    {
        // 100 streets: carwash 3, insurance 2, bank 2, dealer 2, mechanic 3.
        IReadOnlyList<PointOfInterest> grown =
            Spawner().Spawn(100, 36, new PoiDirectory());

        Assert.Equal(3, grown.Count(poi => poi.Type == PoiTypes.CarWash));
        Assert.Equal(2, grown.Count(poi => poi.Type == PoiTypes.Insurance));
        Assert.Equal(2, grown.Count(poi => poi.Type == PoiTypes.Bank));
        Assert.Equal(2, grown.Count(poi => poi.Type == PoiTypes.Dealer));
        Assert.Equal(3, grown.Count(poi => poi.Type == PoiTypes.Mechanic));
    }

    [Fact]
    public void Spawn_IsIdempotent_WhenTheDoorsAreBanked()
    {
        var directory = new PoiDirectory();
        foreach (PointOfInterest poi in Spawner().Spawn(100, 36, directory))
        {
            Assert.True(directory.Add(poi));
        }

        Assert.Empty(Spawner().Spawn(100, 36, directory));
    }

    [Fact]
    public void Spawn_NeverReusesAKnownAddress()
    {
        var directory = new PoiDirectory();
        directory.Add(new PointOfInterest(
            "story", "A Story Door",
            new AddressLocation("Street 1", 1, HouseSide.Right)));

        IReadOnlyList<PointOfInterest> grown =
            Spawner(seed: 7).Spawn(100, 36, directory);

        Assert.DoesNotContain(
            grown,
            poi => poi.Address.StreetName == "Street 1"
                && poi.Address.HouseNumber == 1);
        Assert.Equal(
            grown.Count,
            grown.Select(poi => (poi.Address.StreetName, poi.Address.HouseNumber))
                .Distinct()
                .Count());
    }

    [Fact]
    public void Spawn_StopsAtTheMapCapacity_WithoutThrowing()
    {
        var map = new AddressMap(
            "Germany", "Brands", 60.0, 30.0,
            [new AddressStreet("street-1", "Street 1", 0.0, 1.0)]);
        var spawner = new PoiSpawner(
            map, PoiCatalog.Default,
            new ProceduralNameGenerator(NamePoolTable.Default, seed: 0), seed: 0);

        // Three houses, far more businesses due: the spawner fills what it can
        // and leaves the rest for a later map.
        IReadOnlyList<PointOfInterest> grown =
            spawner.Spawn(1000, 1000, new PoiDirectory());

        Assert.Equal(3, grown.Count);
        Assert.Equal(
            3,
            grown.Select(poi => poi.Address.HouseNumber).Distinct().Count());
    }

    [Fact]
    public void OwnedGroceryHub_PlacesTheFirstStoreImmediately()
    {
        IReadOnlyList<PointOfInterest> grown = Spawner().Spawn(
            1, 0, new PoiDirectory(),
            category => string.Equals(category, "groceries", StringComparison.Ordinal));

        PointOfInterest store = Assert.Single(grown);
        Assert.Equal(PoiTypes.GroceryStore, store.Type);
        Assert.Equal("groceries", store.Category);
        Assert.StartsWith("grocery_store_", store.Id, StringComparison.Ordinal);
        Assert.EndsWith("'s Grocery", store.Name, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(store.OwnerName));
        Assert.InRange(store.Address.HouseNumber, 1, 33);
    }

    [Fact]
    public void OwnedGroceryHub_ScalesEveryTwentyStreets()
    {
        Predicate<string> groceries =
            category => string.Equals(category, "groceries", StringComparison.Ordinal);

        Assert.Equal(
            1, Spawner().Spawn(39, 0, new PoiDirectory(), groceries)
                .Count(poi => poi.Type == PoiTypes.GroceryStore));
        Assert.Equal(
            2, Spawner().Spawn(40, 0, new PoiDirectory(), groceries)
                .Count(poi => poi.Type == PoiTypes.GroceryStore));
    }

    [Fact]
    public void OwnedCoffeeHub_PlacesCoffeeShopsEveryFourteenStreets()
    {
        Predicate<string> coffee =
            category => string.Equals(category, "coffee_beans", StringComparison.Ordinal);

        IReadOnlyList<PointOfInterest> first =
            Spawner().Spawn(1, 0, new PoiDirectory(), coffee);
        PointOfInterest shop = Assert.Single(first);
        Assert.Equal(PoiTypes.CoffeeShop, shop.Type);
        Assert.Equal("coffee_beans", shop.Category);
        Assert.StartsWith("coffee_shop_", shop.Id, StringComparison.Ordinal);

        Assert.Equal(
            2, Spawner().Spawn(28, 0, new PoiDirectory(), coffee)
                .Count(poi => poi.Type == PoiTypes.CoffeeShop));
    }

    [Fact]
    public void NoOwnedHub_PlacesNoStore()
    {
        Assert.DoesNotContain(
            Spawner().Spawn(100, 36, new PoiDirectory()),
            poi => PlacedStore.IsStoreType(poi.Type));
        Assert.DoesNotContain(
            Spawner().Spawn(
                100, 36, new PoiDirectory(),
                category => string.Equals(category, "medicine", StringComparison.Ordinal)),
            poi => PlacedStore.IsStoreType(poi.Type));
    }

    [Fact]
    public void StoreSpawn_IsIdempotent_WhenTheDoorIsBanked()
    {
        var directory = new PoiDirectory();
        foreach (PointOfInterest poi in Spawner().Spawn(
            100, 0, directory,
            category => string.Equals(category, "groceries", StringComparison.Ordinal)))
        {
            Assert.True(directory.Add(poi));
        }

        Assert.DoesNotContain(
            Spawner().Spawn(
                100, 0, directory,
                category => string.Equals(category, "groceries", StringComparison.Ordinal)),
            poi => poi.Type == PoiTypes.GroceryStore);
    }

    [Fact]
    public void Spawn_EmptyMap_AddsNothing()
    {
        var empty = new AddressMap(
            "Germany", "Brands", 0.0, 30.0,
            [new AddressStreet("street-1", "Street 1", 0.0, 0.5)]);
        var spawner = new PoiSpawner(
            empty, PoiCatalog.Default,
            new ProceduralNameGenerator(NamePoolTable.Default, seed: 0), seed: 0);

        Assert.Empty(spawner.Spawn(1000, 1000, new PoiDirectory()));
    }
}
