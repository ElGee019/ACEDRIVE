using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Home lookup plus the closed-lap distance the taxi radius uses.</summary>
public sealed class AddressBookTests
{
    private static readonly AddressMap Map = AddressMap.Default;

    // 20 km loop, two 10 km streets, one house every km: far enough that the
    // 5 km floor can actually reject doors in tests.
    private static readonly AddressMap LongMap = new(
        "Germany", "Test", 20_000.0, 1_000.0,
        [
            new AddressStreet("a", "A", 0.0, 0.5),
            new AddressStreet("b", "B", 0.5, 1.0),
        ]);

    [Fact]
    public void Home_ResolvesTheSavedStreetAndNumber()
    {
        var profile = new PlayerProfile(
            "Driver", "Germany", "Brands", "street-2", 17);

        Assert.Equal(
            new AddressLocation("Street 2", 17, HouseSide.Right),
            AddressBook.Home(Map, profile));
    }

    [Fact]
    public void Home_UnknownOrOutOfRange_AnswersNull()
    {
        Assert.Null(AddressBook.Home(Map, null));
        Assert.Null(AddressBook.Home(
            Map, new PlayerProfile("D", "Germany", "Brands", "nowhere", 1)));
        Assert.Null(AddressBook.Home(
            Map, new PlayerProfile("D", "Germany", "Brands", "street-1", 0)));
        Assert.Null(AddressBook.Home(
            Map, new PlayerProfile("D", "Germany", "Brands", "street-1", 34)));
    }

    [Fact]
    public void Distance_NeighboursAreOneHouseApart()
    {
        Assert.Equal(30.0, AddressBook.DistanceMeters(
            Map,
            new AddressLocation("Street 1", 1, HouseSide.Right),
            new AddressLocation("Street 1", 2, HouseSide.Left)));
    }

    [Fact]
    public void Distance_WrapsAroundTheStartLine()
    {
        // Street 2 No. 33 sits at 1,924.5 m on a 1,929 m lap: 4.5 m from
        // Street 1 No. 1, not 1,924.5 m the other way around.
        Assert.Equal(4.5, AddressBook.DistanceMeters(
            Map,
            new AddressLocation("Street 1", 1, HouseSide.Right),
            new AddressLocation("Street 2", 33, HouseSide.Right)));
    }

    [Fact]
    public void RuntimeTrackLength_MovesTheStreetFractionsNotTheHouseSpacing()
    {
        // Street 2 starts at half of EVO's runtime length; house 33 is 32 x 30 m
        // further, so the closed 2 km lap puts it 40 m from Street 1 No. 1.
        Assert.Equal(1000.0, AddressBook.LapOffsetMeters(
            Map, new AddressLocation("Street 2", 1, HouseSide.Right), 2000.0), 3);
        Assert.Equal(40.0, AddressBook.DistanceMeters(
            Map,
            new AddressLocation("Street 1", 1, HouseSide.Right),
            new AddressLocation("Street 2", 33, HouseSide.Right),
            2000.0), 3);
    }

    [Fact]
    public void Distance_UnknownAddressIsNeverInsideARadius()
    {
        double distance = AddressBook.DistanceMeters(
            Map,
            new AddressLocation("Street 1", 1, HouseSide.Right),
            new AddressLocation("Elsewhere", 1, HouseSide.Right));

        Assert.True(double.IsPositiveInfinity(distance));
    }

    [Fact]
    public void JobFitsRadius_FollowsUnitysOffAndUnsetRules()
    {
        var home = new AddressLocation("A", 1, HouseSide.Right);
        var far = new AddressLocation("B", 1, HouseSide.Left);
        var near = new AddressLocation("A", 3, HouseSide.Right);

        Assert.True(AddressBook.JobFitsRadius(LongMap, home, far, near, 0));
        Assert.True(AddressBook.JobFitsRadius(LongMap, null, far, near, 5));
        Assert.True(AddressBook.JobFitsRadius(LongMap, home, null, near, 5));
        Assert.False(AddressBook.JobFitsRadius(LongMap, home, far, near, 5));
        Assert.True(AddressBook.JobFitsRadius(LongMap, home, near, near, 5));
    }
}
