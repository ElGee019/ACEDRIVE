using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The Touristenfahrten doors: existing coffee shops and pizzerias are reused,
/// and a missing shop or landmark is placed once and banked, never moved.
/// </summary>
public sealed class TouristenDoorsTests
{
    [Fact]
    public void Pickup_WithNoKnownShop_PlacesAndBanksOne()
    {
        var directory = new PoiDirectory();
        var store = new FakePoiDirectoryStore();
        TouristenDoors doors = Create(directory, store);

        PointOfInterest poi = Assert.IsType<PointOfInterest>(doors.Pickup());

        Assert.NotNull(TouristenMenu.Default.PickupFor(poi.Type));
        Assert.True(directory.Contains(poi.Id));
        Assert.Equal(1, store.Saves);
        Assert.Single(store.Saved);
    }

    [Fact]
    public void Pickup_ReusesAKnownShopDoorWithoutWriting()
    {
        var cafe = new PointOfInterest(
            "coffee_shop_0", "Anna's Coffee Shop",
            new AddressLocation("Street 1", 1, HouseSide.Left),
            PoiTypes.CoffeeShop, "Anna", "coffee_beans");
        var directory = new PoiDirectory([cafe]);
        var store = new FakePoiDirectoryStore([cafe]);

        Assert.Same(cafe, Create(directory, store).Pickup());
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void Dropoff_PlacesTheLandmarkOnceAndReusesIt()
    {
        var directory = new PoiDirectory();
        var store = new FakePoiDirectoryStore();
        TouristenDoors doors = Create(directory, store);
        TouristenDropoffDefinition place = TouristenMenu.Default.Dropoffs[0];

        PointOfInterest first = Assert.IsType<PointOfInterest>(doors.Dropoff(place));
        PointOfInterest again = Assert.IsType<PointOfInterest>(doors.Dropoff(place));

        Assert.Same(first, again);
        Assert.Equal(PoiTypes.TouristenDropoff, first.Type);
        Assert.Equal(place.Name, first.Name);
        Assert.Equal(1, store.Saves);
        Assert.Single(directory.All);
    }

    private static TouristenDoors Create(
        PoiDirectory directory, FakePoiDirectoryStore store) =>
        new(
            directory, store,
            new PoiSpawner(
                PoiCatalog.Default,
                new ProceduralNameGenerator(NamePoolTable.Default, seed: 0), seed: 0),
            AddressMap.Default, TouristenMenu.Default, new Random(0), new TestLog());
}
