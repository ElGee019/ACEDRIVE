using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The errand generator places a named POI on a real house.</summary>
public sealed class PoiGeneratorTests
{
    private static readonly string[] StreetNames = ["Street 1", "Street 2"];

    [Fact]
    public void Spawn_ReturnsANamedHouseOnTheMap()
    {
        var generator = new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 0);

        PointOfInterest poi = Assert.IsType<PointOfInterest>(
            generator.Spawn(PoiCatalog.MaxGroceryId));

        Assert.Equal("max-grocery", poi.Id);
        Assert.Equal("Max's Grocery Store", poi.Name);
        Assert.Equal(PoiTypes.Grocery, poi.Type);
        Assert.Equal("Max Silvestri", poi.OwnerName);
        Assert.Equal("groceries", poi.Category);
        Assert.Contains(poi.Address.StreetName, StreetNames);
        Assert.InRange(poi.Address.HouseNumber, 1, 33);
        Assert.Equal(HouseNumberPlan.SideOf(poi.Address.HouseNumber), poi.Address.Side);
    }

    [Fact]
    public void Spawn_IsDeterministicForASeed()
    {
        PointOfInterest? first =
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 7).Spawn();
        PointOfInterest? second =
            new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 7).Spawn();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Spawn_ReachesBothStreetsAndSpreadsOverTheHouseRange()
    {
        var generator = new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 0);
        var streets = new HashSet<string>();
        int min = int.MaxValue;
        int max = 0;
        for (int i = 0; i < 200; i++)
        {
            PointOfInterest poi = Assert.IsType<PointOfInterest>(generator.Spawn());
            streets.Add(poi.Address.StreetName);
            min = Math.Min(min, poi.Address.HouseNumber);
            max = Math.Max(max, poi.Address.HouseNumber);
        }

        Assert.Equal(["Street 1", "Street 2"], streets.OrderBy(name => name));
        Assert.InRange(min, 1, 33);
        Assert.InRange(max, 1, 33);
        Assert.True(max > min);
    }

    [Fact]
    public void Spawn_DrawsEveryDefinition()
    {
        var definitions = new[]
        {
            new PoiDefinition("a", "Alpha"),
            new PoiDefinition("b", "Beta"),
        };
        var generator = new PoiGenerator(AddressMap.Default, definitions, seed: 0);
        var ids = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            ids.Add(Assert.IsType<PointOfInterest>(generator.Spawn()).Id);
        }

        Assert.Equal(["a", "b"], ids.OrderBy(id => id));
    }

    [Fact]
    public void Place_RebuildsTheBankedBusinessAtTheStoredAddress()
    {
        var generator = new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 0);
        var address = new AddressLocation("Street 2", 7, HouseSide.Right);

        PointOfInterest? placed = generator.Place("max-grocery", address);

        PointOfInterest poi = Assert.IsType<PointOfInterest>(placed);
        Assert.Equal("max-grocery", poi.Id);
        Assert.Equal("Max's Grocery Store", poi.Name);
        Assert.Equal(PoiTypes.Grocery, poi.Type);
        Assert.Equal(address, poi.Address);
        Assert.Equal(["MAPLE SYRUP"], poi.GuaranteedItems);
    }

    [Fact]
    public void Place_NullForAnUnknownBusiness()
    {
        var generator = new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 0);
        var address = new AddressLocation("Street 2", 7, HouseSide.Right);

        Assert.Null(generator.Place("gone", address));
    }

    [Fact]
    public void Spawn_NullWithoutDefinitions()
    {
        var generator = new PoiGenerator(AddressMap.Default, [], seed: 0);
        Assert.Null(generator.Spawn());
    }

    [Fact]
    public void Spawn_NullWhenTheMapHasNoHouse()
    {
        var empty = new AddressMap(
            "Germany", "Brands", 0.0, 30.0,
            [new AddressStreet("street-1", "Street 1", 0.0, 0.5)]);
        var generator = new PoiGenerator(empty, PoiCatalog.Default, seed: 0);

        Assert.Null(generator.Spawn());
    }

    [Fact]
    public void DrawAddress_ReturnsAHouseAcrossTheWholeMap()
    {
        var generator = new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 11);
        var streets = new HashSet<string>();
        var sidesValid = true;

        for (int i = 0; i < 200; i++)
        {
            AddressLocation? drawn = generator.DrawAddress();
            Assert.NotNull(drawn);
            AddressLocation address = drawn.Value;
            streets.Add(address.StreetName);
            sidesValid &= address.Side == HouseNumberPlan.SideOf(address.HouseNumber);
            Assert.InRange(address.HouseNumber, 1, 33);
        }

        Assert.Equal(["Street 1", "Street 2"], streets.OrderBy(name => name));
        Assert.True(sidesValid);
    }

    [Fact]
    public void DrawAddress_NullWhenTheMapHasNoHouse()
    {
        var empty = new AddressMap(
            "Germany", "Brands", 0.0, 30.0,
            [new AddressStreet("street-1", "Street 1", 0.0, 0.5)]);
        var generator = new PoiGenerator(empty, PoiCatalog.Default, seed: 0);

        Assert.Null(generator.DrawAddress());
    }

    [Fact]
    public void DrawHouse_PinsTheStreetAndSkipsThePlayersOwnNumber()
    {
        AddressStreet street = AddressMap.Default.Streets[0];
        var generator = new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 3);
        var seen = new HashSet<int>();

        for (int i = 0; i < 200; i++)
        {
            AddressLocation? drawn = generator.DrawHouse(street, 5);
            Assert.NotNull(drawn);
            AddressLocation address = drawn.Value;
            Assert.Equal("Street 1", address.StreetName);
            Assert.Equal(HouseNumberPlan.SideOf(address.HouseNumber), address.Side);
            seen.Add(address.HouseNumber);
        }

        Assert.DoesNotContain(5, seen);
        Assert.True(seen.Count > 10);
    }

    [Fact]
    public void DrawHouse_SkipsEveryExcludedDoor()
    {
        AddressStreet street = AddressMap.Default.Streets[0];
        var generator = new PoiGenerator(AddressMap.Default, PoiCatalog.Default, seed: 4);
        var seen = new HashSet<int>();

        for (int i = 0; i < 200; i++)
        {
            AddressLocation? drawn = generator.DrawHouse(street, 3, 5, 9);
            Assert.NotNull(drawn);
            seen.Add(drawn.Value.HouseNumber);
        }

        Assert.DoesNotContain(3, seen);
        Assert.DoesNotContain(5, seen);
        Assert.DoesNotContain(9, seen);
        Assert.True(seen.Count > 10);
    }

    [Fact]
    public void DrawHouse_KeepsTheOnlyHouseWhenTheStreetHasOne()
    {
        var map = new AddressMap(
            "Germany", "Brands", 20.0, 30.0,
            [new AddressStreet("street-1", "Street 1", 0.0, 1.0)]);
        var generator = new PoiGenerator(map, PoiCatalog.Default, seed: 0);

        AddressLocation? drawn = generator.DrawHouse(map.Streets[0], 1);

        Assert.NotNull(drawn);
        Assert.Equal(
            new AddressLocation("Street 1", 1, HouseSide.Right), drawn.Value);
    }

    [Fact]
    public void DrawHouse_NullWhenEveryHouseIsExcluded()
    {
        var map = new AddressMap(
            "Germany", "Brands", 40.0, 30.0,
            [new AddressStreet("street-1", "Street 1", 0.0, 1.0)]);
        var generator = new PoiGenerator(map, PoiCatalog.Default, seed: 0);

        Assert.Null(generator.DrawHouse(map.Streets[0], 1, 2));
    }

    [Fact]
    public void DrawHouse_NullWhenTheStreetHasNoHouse()
    {
        var empty = new AddressMap(
            "Germany", "Brands", 0.0, 30.0,
            [new AddressStreet("street-1", "Street 1", 0.0, 0.5)]);
        var generator = new PoiGenerator(empty, PoiCatalog.Default, seed: 0);

        Assert.Null(generator.DrawHouse(empty.Streets[0]));
    }
}
