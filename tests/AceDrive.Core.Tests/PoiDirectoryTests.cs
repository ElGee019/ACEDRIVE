using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The discovered-POI directory behind the addresses page.</summary>
public sealed class PoiDirectoryTests
{
    private static PointOfInterest Poi(string id, string name, int house = 1) =>
        new(id, name, new AddressLocation("Street 1", house, HouseSide.Right));

    [Fact]
    public void Alphabetical_SortsByNameThenId()
    {
        var directory = new PoiDirectory();
        directory.Add(Poi("zeta", "Zeta"));
        directory.Add(Poi("alpha-b", "Alpha"));
        directory.Add(Poi("alpha-a", "Alpha"));

        Assert.Equal(
            ["alpha-a", "alpha-b", "zeta"],
            directory.Alphabetical().Select(poi => poi.Id));
    }

    [Fact]
    public void Add_KeepsTheFirstBankedAddress()
    {
        var directory = new PoiDirectory();
        Assert.True(directory.Add(Poi("bank", "Bank", house: 4)));
        Assert.False(directory.Add(Poi("bank", "Bank", house: 9)));

        PointOfInterest bank = Assert.Single(directory.All);
        Assert.Equal(4, bank.Address.HouseNumber);
    }

    [Fact]
    public void Add_RejectsUnusableRows()
    {
        var directory = new PoiDirectory();
        Assert.False(directory.Add(Poi("", "No id")));
        Assert.False(directory.Add(new PointOfInterest(
            "bank", "Bank", new AddressLocation("", 1, HouseSide.Right))));
        Assert.False(directory.Add(new PointOfInterest(
            "bank", "Bank", new AddressLocation("Street 1", 0, HouseSide.Right))));
        Assert.Empty(directory.All);
    }
}
