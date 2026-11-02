using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The progressive business POIs (PORTPLAN 24, Unity generators): the
/// street/job thresholds, the discovered-street draw and the expansion's
/// first-door rule.
/// </summary>
public sealed class PoiSpawnerTests
{
    private static PoiSpawner Spawner(int seed = 0) => new(
        PoiCatalog.Default,
        new ProceduralNameGenerator(NamePoolTable.Default, seed), seed);

    private static PoiDirectory StreetTwoFullExceptFirst()
    {
        // Leaves Street 2 house 1 free: the forced expansion door has exactly
        // one slot, so what happens next is deterministic.
        var directory = new PoiDirectory();
        AddressStreet street = AddressMap.Default.Streets[1];
        for (int number = 2; number <= HouseNumberPlan.Count(street, AddressMap.Default); number++)
        {
            directory.Add(new PointOfInterest(
                $"story_{number}", $"Story Door {number}",
                new AddressLocation(street.Name, number, HouseNumberPlan.SideOf(number))));
        }
        return directory;
    }

    [Fact]
    public void SpawnGrantedFirst_FallsBackToTheWholeMap_WhenTheZoneHasNoFreeHouse()
    {
        // The moment is the player's, not the map's: a zone whose streets are
        // all taken must still open the broker on the map it was cut from, so a
        // crash repair never promises a place that is not there.
        AddressMap map = AddressMap.Default;
        AddressStreet zoneStreet = map.Streets[0];
        PoiDirectory directory = FullStreet(zoneStreet, map);
        AddressMap zoneMap = map with { Streets = [zoneStreet] };

        Assert.Null(Spawner().SpawnGrantedFirst(PoiTypes.Insurance, directory, zoneMap));

        PointOfInterest broker = Assert.IsType<PointOfInterest>(
            Spawner().SpawnGrantedFirst(PoiTypes.Insurance, directory, zoneMap, map));

        Assert.Equal(PoiTypes.Insurance, broker.Type);
        Assert.Equal(map.Streets[1].Name, broker.Address.StreetName);
    }

    [Fact]
    public void SpawnGrantedFirst_StaysInTheZone_WhenItHasRoom()
    {
        // The fallback is a last resort: a free house in the zone always wins.
        AddressMap map = AddressMap.Default;
        AddressMap zoneMap = map with { Streets = [map.Streets[1]] };

        PointOfInterest broker = Assert.IsType<PointOfInterest>(
            Spawner().SpawnGrantedFirst(
                PoiTypes.Insurance, new PoiDirectory(), zoneMap, map));

        Assert.Equal(map.Streets[1].Name, broker.Address.StreetName);
    }

    private static PoiDirectory FullStreet(AddressStreet street, AddressMap map)
    {
        var directory = new PoiDirectory();
        for (int number = 1; number <= HouseNumberPlan.Count(street, map); number++)
        {
            directory.Add(new PointOfInterest(
                $"story_{number}", $"Story Door {number}",
                new AddressLocation(street.Name, number, HouseNumberPlan.SideOf(number))));
        }
        return directory;
    }

    [Fact]
    public void Place_DrawsAUniqueFreeDoorForACareerHub()
    {
        var directory = new PoiDirectory();
        PointOfInterest hub = Assert.IsType<PointOfInterest>(Spawner().Place(
            "groceries_grocers_and_sons", "Grocers and Sons",
            PoiTypes.Hub, "Elena Marchetti", "groceries", directory, AddressMap.Default));

        Assert.Equal(PoiTypes.Hub, hub.Type);
        Assert.Equal("groceries", hub.Category);
        Assert.Equal("Elena Marchetti", hub.OwnerName);
        Assert.True(hub.Address.StreetName is "Street 1" or "Street 2");
        Assert.InRange(hub.Address.HouseNumber, 1, 33);

        Assert.True(directory.Add(hub));
        Assert.Null(Spawner().Place(
            "groceries_grocers_and_sons", "Grocers and Sons",
            PoiTypes.Hub, "Elena Marchetti", "groceries", directory, AddressMap.Default));
        Assert.Null(Spawner().Place(
            "", "No id", PoiTypes.Hub, "Elena Marchetti", "groceries", directory,
            AddressMap.Default));
    }

    [Fact]
    public void NewCareer_HasNoBusinessDoor()
    {
        Assert.Empty(Spawner().Spawn(0, 0, new PoiDirectory(), AddressMap.Default));
        // The home street alone opens nothing; a new game starts on story
        // doors only.
        Assert.Empty(Spawner().Spawn(1, 0, new PoiDirectory(), AddressMap.Default));
    }

    [Fact]
    public void SpawnOnDemand_OpensTheDealerBelowItsOwnCurve()
    {
        // The car haul's pickup dealer: a career nowhere near the dealer's ten
        // jobs still gets the door the first haul rides from, named and placed
        // like a paced one.
        var directory = new PoiDirectory();

        PointOfInterest dealer = Assert.IsType<PointOfInterest>(
            Spawner().SpawnOnDemand(PoiTypes.Dealer, directory, AddressMap.Default));

        Assert.Equal(PoiTypes.Dealer, dealer.Type);
        Assert.Equal("dealer_0", dealer.Id);
        Assert.EndsWith("'s Cars", dealer.Name, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(dealer.OwnerName));
        Assert.InRange(dealer.Address.HouseNumber, 1, 33);
    }

    [Fact]
    public void SpawnOnDemand_RefusesAFamilyThatAlreadyHasADoor()
    {
        // The caller uses the door that exists; a second one would break the
        // one-dealer-per-family rule the pacing keeps.
        var directory = new PoiDirectory();
        directory.Add(new PointOfInterest(
            "dealer_0", "Bianchi Cars",
            new AddressLocation("Street 1", 4, HouseSide.Left),
            PoiTypes.Dealer, "Giulia Bianchi"));

        Assert.Null(Spawner().SpawnOnDemand(
            PoiTypes.Dealer, directory, AddressMap.Default));
    }

    [Fact]
    public void SixStreets_OpenTheCarWash()
    {
        PointOfInterest poi = Assert.Single(
            Spawner().Spawn(6, 0, new PoiDirectory(), AddressMap.Default));

        Assert.Equal(PoiTypes.CarWash, poi.Type);
        Assert.StartsWith("carwash_", poi.Id, StringComparison.Ordinal);
        Assert.EndsWith("'s Carwash", poi.Name, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(poi.OwnerName));
        Assert.True(poi.Address.StreetName is "Street 1" or "Street 2");
        Assert.InRange(poi.Address.HouseNumber, 1, 33);
    }

    [Fact]
    public void StreetThresholds_OpenTheCarWashAndTheBank_ButNeverTheFirstBroker()
    {
        // The insurance broker's first door is earned by the player's first
        // crash repair, never by a street count: its family stays empty here
        // even past Unity's 13-street threshold.
        Assert.Equal(
            [PoiTypes.CarWash, PoiTypes.Bank],
            Spawner().Spawn(15, 0, new PoiDirectory(), AddressMap.Default)
                .Select(poi => poi.Type));
    }

    [Fact]
    public void SpawnGrantedFirst_OpensTheInsuranceBrokerOnce_ThenTheStreetCurveAddsTheNext()
    {
        var directory = new PoiDirectory();

        PointOfInterest broker = Assert.IsType<PointOfInterest>(
            Spawner().SpawnGrantedFirst(PoiTypes.Insurance, directory, AddressMap.Default));

        Assert.Equal(PoiTypes.Insurance, broker.Type);
        Assert.Equal("insurance_0", broker.Id);
        Assert.EndsWith("'s Insurance", broker.Name, StringComparison.Ordinal);
        Assert.True(directory.Add(broker));

        // The moment is once, and only the families whose first is earned can
        // be opened this way.
        Assert.Null(Spawner().SpawnGrantedFirst(PoiTypes.Insurance, directory, AddressMap.Default));
        Assert.Null(Spawner().SpawnGrantedFirst(PoiTypes.CarWash, new PoiDirectory(), AddressMap.Default));

        // The granted broker holds the 13-street slot: the next one is due one
        // full interval later, exactly where the paced second door used to open.
        Assert.DoesNotContain(
            Spawner().Spawn(13, 0, directory, AddressMap.Default),
            poi => poi.Type == PoiTypes.Insurance);
        Assert.Single(
            Spawner().Spawn(70, 0, directory, AddressMap.Default),
            poi => poi.Type == PoiTypes.Insurance);
    }

    [Fact]
    public void TenJobs_OpenTheDealer()
    {
        Assert.DoesNotContain(
            Spawner().Spawn(1, 9, new PoiDirectory(), AddressMap.Default),
            poi => poi.Type == PoiTypes.Dealer);

        IReadOnlyList<PointOfInterest> grown =
            Spawner().Spawn(1, 10, new PoiDirectory(), AddressMap.Default);

        PointOfInterest dealer = Assert.Single(grown, poi => poi.Type == PoiTypes.Dealer);
        Assert.EndsWith("'s Cars", dealer.Name, StringComparison.Ordinal);
        Assert.Equal(PoiTypes.Dealer, dealer.Type);
    }

    [Fact]
    public void ThirtySixJobs_OpenTheMechanic_ThenTheStreetIntervalAddsOne()
    {
        Assert.DoesNotContain(
            Spawner().Spawn(10, 35, new PoiDirectory(), AddressMap.Default),
            poi => poi.Type == PoiTypes.Mechanic);

        Assert.Single(
            Spawner().Spawn(3, 36, new PoiDirectory(), AddressMap.Default),
            poi => poi.Type == PoiTypes.Mechanic);

        Assert.Equal(
            2,
            Spawner().Spawn(39, 36, new PoiDirectory(), AddressMap.Default)
                .Count(poi => poi.Type == PoiTypes.Mechanic));
    }

    [Fact]
    public void MultipleIntervals_CatchUpInOneCall()
    {
        // 100 streets: carwash 3, insurance 1 more (the second door — the first
        // came from the crash repair), bank 2, dealer 2, mechanic 3.
        var directory = new PoiDirectory();
        directory.Add(Assert.IsType<PointOfInterest>(Spawner().SpawnGrantedFirst(
            PoiTypes.Insurance, directory, AddressMap.Default)));

        IReadOnlyList<PointOfInterest> grown =
            Spawner().Spawn(100, 36, directory, AddressMap.Default);

        Assert.Equal(3, grown.Count(poi => poi.Type == PoiTypes.CarWash));
        Assert.Equal(1, grown.Count(poi => poi.Type == PoiTypes.Insurance));
        Assert.Equal(2, grown.Count(poi => poi.Type == PoiTypes.Bank));
        Assert.Equal(2, grown.Count(poi => poi.Type == PoiTypes.Dealer));
        Assert.Equal(3, grown.Count(poi => poi.Type == PoiTypes.Mechanic));
    }

    [Fact]
    public void Spawn_IsIdempotent_WhenTheDoorsAreBanked()
    {
        var directory = new PoiDirectory();
        foreach (PointOfInterest poi in
            Spawner().Spawn(100, 36, directory, AddressMap.Default))
        {
            Assert.True(directory.Add(poi));
        }

        Assert.Empty(Spawner().Spawn(100, 36, directory, AddressMap.Default));
    }

    [Fact]
    public void Spawn_NeverReusesAKnownAddress()
    {
        var directory = new PoiDirectory();
        directory.Add(new PointOfInterest(
            "story", "A Story Door",
            new AddressLocation("Street 1", 1, HouseSide.Right)));

        IReadOnlyList<PointOfInterest> grown =
            Spawner(seed: 7).Spawn(100, 36, directory, AddressMap.Default);

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
    public void Spawn_DrawsOnlyOnTheDiscoveredStreets()
    {
        // The zone's map is the only pool: a zone that covers Street 2 alone
        // can never send a generated business to Street 1.
        var zoneMap = AddressMap.Default with
        {
            Streets = [AddressMap.Default.Streets[1]],
        };

        IReadOnlyList<PointOfInterest> grown =
            Spawner().Spawn(100, 36, new PoiDirectory(), zoneMap);

        Assert.NotEmpty(grown);
        Assert.All(grown, poi => Assert.Equal("Street 2", poi.Address.StreetName));
    }

    [Fact]
    public void Spawn_FirstDoorOfAnExpansion_OpensOnTheNewStreet()
    {
        IReadOnlyList<PointOfInterest> grown = Spawner().Spawn(
            6, 0, new PoiDirectory(), AddressMap.Default, openedStreet: "Street 2");

        PointOfInterest carwash = Assert.Single(grown);
        Assert.Equal(PoiTypes.CarWash, carwash.Type);
        Assert.Equal("Street 2", carwash.Address.StreetName);
    }

    [Fact]
    public void Spawn_OnlyTheFirstDoor_UsesTheOpenedStreet()
    {
        // One free slot on Street 2: the expansion's first door takes it, the
        // second family must draw over the whole discovered map.
        PoiDirectory directory = StreetTwoFullExceptFirst();

        IReadOnlyList<PointOfInterest> grown = Spawner().Spawn(
            13, 10, directory, AddressMap.Default, openedStreet: "Street 2");

        Assert.Equal(2, grown.Count);
        Assert.Equal(PoiTypes.CarWash, grown[0].Type);
        Assert.Equal(new AddressLocation("Street 2", 1, HouseSide.Right), grown[0].Address);
        Assert.Equal(PoiTypes.Dealer, grown[1].Type);
        Assert.Equal("Street 1", grown[1].Address.StreetName);
    }

    [Fact]
    public void Spawn_FullOpenedStreet_FallsBackInsteadOfSkipping()
    {
        // Every Street 2 house is taken: the carwash must still open, on the
        // other discovered street, rather than being dropped for the expansion.
        var directory = new PoiDirectory();
        AddressStreet street = AddressMap.Default.Streets[1];
        for (int number = 1; number <= HouseNumberPlan.Count(street, AddressMap.Default); number++)
        {
            directory.Add(new PointOfInterest(
                $"story_{number}", $"Story Door {number}",
                new AddressLocation(street.Name, number, HouseNumberPlan.SideOf(number))));
        }

        IReadOnlyList<PointOfInterest> grown = Spawner().Spawn(
            6, 0, directory, AddressMap.Default, openedStreet: "Street 2");

        PointOfInterest carwash = Assert.Single(grown);
        Assert.Equal(PoiTypes.CarWash, carwash.Type);
        Assert.Equal("Street 1", carwash.Address.StreetName);
    }

    [Fact]
    public void NoStreetJoined_OpensNoStreetPacedBusiness()
    {
        // Boot, board open and hub close ask with streetJoined: false so no
        // business is ever added off the expansion page. Both the car wash
        // (6 streets) and the dealer (10 jobs) are due here and both wait.
        Assert.Empty(Spawner().Spawn(
            6, 10, new PoiDirectory(), AddressMap.Default, streetJoined: false));
    }

    [Fact]
    public void NoStreetJoined_StillOpensAnOwnedHubStore()
    {
        // Stores follow the hub purchase, not the streets: a career's first
        // store must exist when the board is shown again after the purchase.
        Predicate<string> groceries =
            category => string.Equals(category, "groceries", StringComparison.Ordinal);

        PointOfInterest store = Assert.Single(Spawner().Spawn(
            1, 0, new PoiDirectory(), AddressMap.Default,
            ownsCategory: groceries, streetJoined: false));

        Assert.Equal(PoiTypes.GroceryStore, store.Type);
    }

    [Fact]
    public void MaxDoors_OpensOneFamilyPerExpansion()
    {
        // One street carries one announcement: the car wash takes this
        // expansion's door, the dealer stays due for the next one.
        PointOfInterest door = Assert.Single(Spawner().Spawn(
            6, 10, new PoiDirectory(), AddressMap.Default,
            openedStreet: "Street 2", maxDoors: 1));

        Assert.Equal(PoiTypes.CarWash, door.Type);
        Assert.Equal("Street 2", door.Address.StreetName);
    }

    [Fact]
    public void Spawn_StopsAtTheMapCapacity_WithoutThrowing()
    {
        var map = new AddressMap(
            "Germany", "Brands", 60.0, 30.0,
            [new AddressStreet("street-1", "Street 1", 0.0, 1.0)]);

        // Three houses, far more businesses due: the spawner fills what it can
        // and leaves the rest for a later map.
        IReadOnlyList<PointOfInterest> grown =
            Spawner().Spawn(1000, 1000, new PoiDirectory(), map);

        Assert.Equal(3, grown.Count);
        Assert.Equal(
            3,
            grown.Select(poi => poi.Address.HouseNumber).Distinct().Count());
    }

    [Fact]
    public void OwnedGroceryHub_PlacesTheFirstStoreImmediately()
    {
        Predicate<string> groceries =
            category => string.Equals(category, "groceries", StringComparison.Ordinal);

        IReadOnlyList<PointOfInterest> grown = Spawner().Spawn(
            1, 0, new PoiDirectory(), AddressMap.Default, ownsCategory: groceries);

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
            1, Spawner().Spawn(39, 0, new PoiDirectory(), AddressMap.Default, ownsCategory: groceries)
                .Count(poi => poi.Type == PoiTypes.GroceryStore));
        Assert.Equal(
            2, Spawner().Spawn(40, 0, new PoiDirectory(), AddressMap.Default, ownsCategory: groceries)
                .Count(poi => poi.Type == PoiTypes.GroceryStore));
    }

    [Fact]
    public void OwnedCoffeeHub_PlacesCoffeeShopsEveryFourteenStreets()
    {
        Predicate<string> coffee =
            category => string.Equals(category, "coffee_beans", StringComparison.Ordinal);

        IReadOnlyList<PointOfInterest> first =
            Spawner().Spawn(1, 0, new PoiDirectory(), AddressMap.Default, ownsCategory: coffee);
        PointOfInterest shop = Assert.Single(first);
        Assert.Equal(PoiTypes.CoffeeShop, shop.Type);
        Assert.Equal("coffee_beans", shop.Category);
        Assert.StartsWith("coffee_shop_", shop.Id, StringComparison.Ordinal);

        Assert.Equal(
            2, Spawner().Spawn(28, 0, new PoiDirectory(), AddressMap.Default, ownsCategory: coffee)
                .Count(poi => poi.Type == PoiTypes.CoffeeShop));
    }

    [Fact]
    public void NoOwnedHub_PlacesNoStore()
    {
        Assert.DoesNotContain(
            Spawner().Spawn(100, 36, new PoiDirectory(), AddressMap.Default),
            poi => PlacedStore.IsStoreType(poi.Type));
        Assert.DoesNotContain(
            Spawner().Spawn(
                100, 36, new PoiDirectory(), AddressMap.Default,
                ownsCategory: category => string.Equals(
                    category, "medicine", StringComparison.Ordinal)),
            poi => PlacedStore.IsStoreType(poi.Type));
    }

    [Fact]
    public void StoreSpawn_IsIdempotent_WhenTheDoorIsBanked()
    {
        Predicate<string> groceries =
            category => string.Equals(category, "groceries", StringComparison.Ordinal);
        var directory = new PoiDirectory();
        foreach (PointOfInterest poi in
            Spawner().Spawn(100, 0, directory, AddressMap.Default, ownsCategory: groceries))
        {
            Assert.True(directory.Add(poi));
        }

        Assert.DoesNotContain(
            Spawner().Spawn(100, 0, directory, AddressMap.Default, ownsCategory: groceries),
            poi => poi.Type == PoiTypes.GroceryStore);
    }

    [Fact]
    public void Spawn_EmptyMap_AddsNothing()
    {
        var empty = new AddressMap(
            "Germany", "Brands", 0.0, 30.0,
            [new AddressStreet("street-1", "Street 1", 0.0, 0.5)]);

        Assert.Empty(Spawner().Spawn(1000, 1000, new PoiDirectory(), empty));
    }
}
