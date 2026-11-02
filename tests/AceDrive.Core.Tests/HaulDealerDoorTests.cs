using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The haul's pickup dealer over the POI directory: a discovered dealer is
/// reused as it is, a career without one opens its first and banks it, and a
/// row without a street never becomes the pickup.
/// </summary>
public sealed class HaulDealerDoorTests
{
    private static HaulDealerDoor Door(
        PoiDirectory directory, FakePoiDirectoryStore store) =>
        new(directory, store,
            new PoiSpawner(
                PoiCatalog.Default,
                new ProceduralNameGenerator(NamePoolTable.Default, seed: 0), seed: 0),
            new OperatingZone(
                new DriverState(0.0, 0.0, StreetIds: ["street-1", "street-2"], ZoneKm: 1.0),
                seed: 0, AddressMap.Default, homeStreetId: "street-1"),
            AddressMap.Default, new TestLog());

    [Fact]
    public void Dealer_ReusesADiscoveredDealerWithoutSaving()
    {
        var directory = new PoiDirectory();
        var dealer = new PointOfInterest(
            "dealer-1", "Bianchi Cars",
            new AddressLocation("Street 1", 4, HouseSide.Left),
            PoiTypes.Dealer, "Giulia Bianchi");
        directory.Add(dealer);
        var store = new FakePoiDirectoryStore();

        Assert.Same(dealer, Door(directory, store).Dealer());
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void Dealer_OpensAndBanksTheFirstDealerOnce()
    {
        var directory = new PoiDirectory();
        var store = new FakePoiDirectoryStore();

        PointOfInterest opened = Assert.IsType<PointOfInterest>(
            Door(directory, store).Dealer());

        Assert.Equal(PoiTypes.Dealer, opened.Type);
        Assert.False(string.IsNullOrEmpty(opened.Address.StreetName));
        Assert.Contains(directory.All, poi => poi.Id == opened.Id);
        Assert.Equal(1, store.Saves);
        Assert.Contains(store.Saved, poi => poi.Id == opened.Id);

        // The next tap reuses the door: the first haul opens it, not every tap.
        Assert.Same(opened, Door(directory, store).Dealer());
        Assert.Equal(1, store.Saves);
    }
}
