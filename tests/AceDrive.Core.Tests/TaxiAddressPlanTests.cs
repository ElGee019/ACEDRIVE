using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Unity TaxiJobGenerator steps 1-2: two houses, on different streets
/// while the map offers a second one, filtered by the radius around the
/// player's home; a one-street zone falls back to another house on that street
/// (TOZONES §4.7).</summary>
public sealed class TaxiAddressPlanTests
{
    private static readonly AddressMap Map = AddressMap.Default;

    // 20 km loop, two 10 km streets, one house every km: the 5 km floor can
    // reject doors here, unlike the 1,929 m Brands Hatch stand-in.
    private static readonly AddressMap LongMap = new(
        "Germany", "Test", 20_000.0, 1_000.0,
        [
            new AddressStreet("a", "A", 0.0, 0.5),
            new AddressStreet("b", "B", 0.5, 1.0),
        ]);

    [Fact]
    public void Draw_UsesTwoDifferentStreets_WithValidNumbers()
    {
        foreach (int seed in Enumerable.Range(0, 25))
        {
            Assert.True(TaxiAddressPlan.TryDraw(
                Map, new Random(seed), home: null, radiusKm: 0,
                out AddressLocation home, out AddressLocation second));

            Assert.NotEqual(home.StreetName, second.StreetName);
            Assert.InRange(home.HouseNumber, 1, 33);
            Assert.InRange(second.HouseNumber, 1, 33);
        }
    }

    [Fact]
    public void SameSeed_ReplaysTheSamePair()
    {
        Assert.True(TaxiAddressPlan.TryDraw(
            Map, new Random(7), home: null, radiusKm: 0,
            out AddressLocation firstHome, out AddressLocation firstSecond));
        Assert.True(TaxiAddressPlan.TryDraw(
            Map, new Random(7), home: null, radiusKm: 0,
            out AddressLocation secondHome, out AddressLocation secondSecond));

        Assert.Equal(firstHome, secondHome);
        Assert.Equal(firstSecond, secondSecond);
    }

    [Fact]
    public void OneStreet_DrawsTheSecondResidenceOnTheSameStreet()
    {
        // A fresh career's zone covers a single street. The proposal must still
        // resolve both legs — the second one as another door on that street,
        // never the same address twice.
        var map = new AddressMap(
            "Germany", "Test", 1_929.0, 30.0,
            [new AddressStreet("only", "Only Street", 0.0, 1.0)]);

        foreach (int seed in Enumerable.Range(0, 25))
        {
            Assert.True(TaxiAddressPlan.TryDraw(
                map, new Random(seed), home: null, radiusKm: 0,
                out AddressLocation home, out AddressLocation second));

            Assert.Equal("Only Street", home.StreetName);
            Assert.Equal(home.StreetName, second.StreetName);
            Assert.NotEqual(home.HouseNumber, second.HouseNumber);
            Assert.InRange(home.HouseNumber, 1, 65);
            Assert.InRange(second.HouseNumber, 1, 65);
        }
    }

    [Fact]
    public void OneStreetWithASingleHouse_CannotDraw()
    {
        // One address cannot host both legs: the page keeps its fallback copy
        // instead of sending the passenger next door to themselves.
        var map = new AddressMap(
            "Germany", "Test", 1_000.0, 2_000.0,
            [new AddressStreet("only", "Only Street", 0.0, 0.5)]);

        Assert.False(TaxiAddressPlan.TryDraw(
            map, new Random(0), home: null, radiusKm: 0,
            out _, out _));
    }

    [Fact]
    public void NoHouseNumbers_CannotDraw()
    {
        var map = new AddressMap(
            "Germany", "Test", 1_929.0, 0.0,
            [
                new AddressStreet("a", "A", 0.0, 0.5),
                new AddressStreet("b", "B", 0.5, 1.0),
            ]);

        Assert.False(TaxiAddressPlan.TryDraw(
            map, new Random(0), home: null, radiusKm: 0,
            out _, out _));
    }

    [Fact]
    public void Radius_KeepsBothLegsWithinTheLimit()
    {
        var home = new AddressLocation("A", 1, HouseSide.Right);
        var streets = new HashSet<string>(StringComparer.Ordinal);

        foreach (int seed in Enumerable.Range(0, 50))
        {
            Assert.True(TaxiAddressPlan.TryDraw(
                LongMap, new Random(seed), home, radiusKm: 5,
                out AddressLocation first, out AddressLocation second));

            Assert.True(AddressBook.DistanceMeters(LongMap, home, first) <= 5_000.0);
            Assert.True(AddressBook.DistanceMeters(LongMap, home, second) <= 5_000.0);
            streets.Add(first.StreetName);
            streets.Add(second.StreetName);
        }

        // Both streets have doors inside the radius (one near the start line,
        // one near the lap end), so the draw is not silently stuck on one.
        Assert.Equal(2, streets.Count);
    }

    [Fact]
    public void Radius_LeavingOneStreet_DrawsBothLegsOnIt()
    {
        // A covers 0-10 km, B 30-90 km: from home at 0 only A has a door
        // inside 5 km (B's nearest end is 10 km away around the loop). The
        // radius leaves a one-street pool, so the relaxed draw keeps both legs
        // on A instead of answering false.
        var map = new AddressMap(
            "Germany", "Test", 100_000.0, 1_000.0,
            [
                new AddressStreet("a", "A", 0.0, 0.1),
                new AddressStreet("b", "B", 0.3, 0.9),
            ]);
        var home = new AddressLocation("A", 1, HouseSide.Right);

        Assert.True(TaxiAddressPlan.TryDraw(
            map, new Random(0), home, radiusKm: 5,
            out AddressLocation first, out AddressLocation second));
        Assert.NotEqual(first.HouseNumber, second.HouseNumber);
        Assert.True(AddressBook.DistanceMeters(map, home, first) <= 5_000.0);
        Assert.True(AddressBook.DistanceMeters(map, home, second) <= 5_000.0);

        Assert.True(TaxiAddressPlan.TryDraw(
            map, new Random(0), home, radiusKm: 0,
            out AddressLocation unlimitedFirst, out AddressLocation unlimitedSecond));
        Assert.NotEqual(unlimitedFirst.StreetName, unlimitedSecond.StreetName);
    }
}
