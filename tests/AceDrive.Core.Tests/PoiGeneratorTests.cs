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

        PointOfInterest poi = Assert.IsType<PointOfInterest>(generator.Spawn());

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
}
