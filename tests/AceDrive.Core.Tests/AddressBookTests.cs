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
    public void MapLengthKm_SumsTheStreetSpans()
    {
        // 1,929 m over two half-lap streets: the interim operating-zone seed.
        Assert.Equal(1.929, AddressBook.MapLengthKm(Map), precision: 9);
        Assert.Equal(20.0, AddressBook.MapLengthKm(LongMap), precision: 9);
    }

    [Fact]
    public void AverageStreetLengthKm_DividesByTheStreetCount()
    {
        Assert.Equal(0.9645, AddressBook.AverageStreetLengthKm(Map), precision: 9);
        Assert.Equal(10.0, AddressBook.AverageStreetLengthKm(LongMap), precision: 9);
        Assert.Equal(0.0, AddressBook.AverageStreetLengthKm(
            new AddressMap("Germany", "Test", 20_000.0, 1_000.0, [])));
    }

    [Fact]
    public void MapLengthKm_CountsBaseStreetsOnly_AndOneStreetCarriesItsOwnSpan()
    {
        // The fictional rows label the same lap, so they must not inflate the
        // base area the zone and the starter bonus seed from (TOZONES §4.1).
        var map = new AddressMap(
            "Germany", "Test", 1929.0, 30.0,
            [
                new AddressStreet("street-1", "Street 1", 0.0, 0.5),
                new AddressStreet("street-2", "Street 2", 0.5, 1.0),
                new AddressStreet("street-3", "Street 3", 0.0, 0.5, Fictional: true),
                new AddressStreet("street-4", "Street 4", 0.5, 1.0, Fictional: true),
            ]);

        Assert.Equal(1.929, AddressBook.MapLengthKm(map), precision: 9);
        Assert.Equal(0.9645, AddressBook.AverageStreetLengthKm(map), precision: 9);
        Assert.Equal(
            0.9645, AddressBook.StreetLengthKm(map, map.Streets[2]), precision: 9);
        // A map with no base street has no base area, never a fictional one.
        Assert.Equal(0.0, AddressBook.MapLengthKm(new AddressMap(
            "Germany", "Test", 1929.0, 30.0,
            [new AddressStreet("street-3", "Street 3", 0.0, 0.5, Fictional: true)])));
    }

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

    [Fact]
    public void NeighbourStreets_AreTheRowsOnEitherSideOfTheClosedLap()
    {
        var ring = new AddressMap(
            "Germany", "Test", 400.0, 30.0,
            [
                new AddressStreet("a", "A", 0.0, 0.25),
                new AddressStreet("b", "B", 0.25, 0.5),
                new AddressStreet("c", "C", 0.5, 0.75),
                new AddressStreet("d", "D", 0.75, 1.0),
            ]);

        Assert.Equal(
            ["D", "B"],
            AddressBook.NeighbourStreets(ring, ring.Streets[0]).Select(street => street.Name));
        Assert.Equal(
            ["A", "C"],
            AddressBook.NeighbourStreets(ring, ring.Streets[1]).Select(street => street.Name));
        Assert.Equal(
            ["B", "D"],
            AddressBook.NeighbourStreets(ring, ring.Streets[2]).Select(street => street.Name));
        Assert.Equal(
            ["C", "A"],
            AddressBook.NeighbourStreets(ring, ring.Streets[3]).Select(street => street.Name));
    }

    [Fact]
    public void NeighbourStreets_KeepTinyAndUnknownMapsHonest()
    {
        // Two rows share the one other street; one row has no neighbour and a
        // row from another map answers none instead of throwing.
        Assert.Equal(
            ["Street 2"],
            AddressBook.NeighbourStreets(Map, Map.Streets[0]).Select(street => street.Name));
        Assert.Equal(
            ["Street 1"],
            AddressBook.NeighbourStreets(Map, Map.Streets[1]).Select(street => street.Name));

        var single = new AddressMap(
            "Germany", "Test", 100.0, 30.0,
            [new AddressStreet("only", "Only", 0.0, 1.0)]);
        Assert.Empty(AddressBook.NeighbourStreets(single, single.Streets[0]));
        Assert.Empty(AddressBook.NeighbourStreets(
            Map, new AddressStreet("elsewhere", "Elsewhere", 0.0, 0.5)));
    }

    [Fact]
    public void ZoneStreets_LeadsWithTheHomeStreetAndCapsAtTheDiscoveredCount()
    {
        // The interim mapping the Cento Miglia route plans over: the profile's
        // street joins first, the map order fills the rest, and the zone's
        // street count is the cap.
        Assert.Equal(
            ["Street 2", "Street 1"],
            AddressBook.ZoneStreets(Map, 2, "street-2").Select(street => street.Name));
        Assert.Equal(
            ["Street 2"],
            AddressBook.ZoneStreets(Map, 1, "street-2").Select(street => street.Name));
        // No profile yet: the map order answers.
        Assert.Equal(
            ["Street 1", "Street 2"],
            AddressBook.ZoneStreets(Map, 2, null).Select(street => street.Name));
        // A grown zone never asks for streets the map does not have.
        Assert.Equal(2, AddressBook.ZoneStreets(Map, 9, "street-1").Count);
        Assert.Empty(AddressBook.ZoneStreets(Map, 0, "street-1"));
    }
}
