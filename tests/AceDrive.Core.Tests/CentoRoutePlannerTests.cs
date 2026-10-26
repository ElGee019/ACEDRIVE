using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia route (PORTPLAN 49, Unity MilleMigliaService):
/// the furthest door pair on the operating zone's streets, measured on the
/// placeholder map's closed lap.</summary>
public sealed class CentoRoutePlannerTests
{
    private static AddressMap Map(params AddressStreet[] streets) =>
        AddressMap.Default with { Streets = streets };

    [Fact]
    public void LongestRoute_SpansHalfTheLapAcrossBothStreets()
    {
        // Street 1 No. 1 sits on the line, Street 2 No. 1 exactly half a lap
        // later: the furthest pair the closed lap can separate.
        CentoRoutePlan plan = Assert.IsType<CentoRoutePlan>(
            CentoRoutePlanner.LongestRoute(AddressMap.Default, AddressMap.Default.Streets));
        Assert.Equal("Street 1", plan.Start.StreetName);
        Assert.Equal(1, plan.Start.HouseNumber);
        Assert.Equal("Street 2", plan.Finish.StreetName);
        Assert.Equal(1, plan.Finish.HouseNumber);
        Assert.Equal(0.9645, plan.DistanceKm, precision: 6);
    }

    [Fact]
    public void LongestRoute_StaysInsideTheStreetsItIsGiven()
    {
        // A one-street zone cannot reach the other half of the map: the furthest
        // pair is that street's first and last door.
        AddressStreet street = AddressMap.Default.Streets[0];
        CentoRoutePlan plan = Assert.IsType<CentoRoutePlan>(
            CentoRoutePlanner.LongestRoute(AddressMap.Default, [street]));
        Assert.Equal("Street 1", plan.Start.StreetName);
        Assert.Equal("Street 1", plan.Finish.StreetName);
        Assert.Equal(1, plan.Start.HouseNumber);
        Assert.Equal(33, plan.Finish.HouseNumber);
        Assert.Equal(0.96, plan.DistanceKm, precision: 6);
    }

    [Fact]
    public void LongestRoute_KeepsTheFirstPairOnATie()
    {
        // Every same-number pair across the two streets is half a lap apart, so
        // the tie-break (street then house order) is what makes the route stable
        // between visits.
        CentoRoutePlan first = Assert.IsType<CentoRoutePlan>(
            CentoRoutePlanner.LongestRoute(AddressMap.Default, AddressMap.Default.Streets));
        CentoRoutePlan again = Assert.IsType<CentoRoutePlan>(
            CentoRoutePlanner.LongestRoute(AddressMap.Default, AddressMap.Default.Streets));
        Assert.Equal(first, again);
    }

    [Fact]
    public void LongestRoute_NeedsTwoDoors()
    {
        Assert.Null(CentoRoutePlanner.LongestRoute(AddressMap.Default, []));

        // A street shorter than the house spacing carries one door only.
        AddressMap tiny = Map(new AddressStreet("a", "A", 0.0, 0.5)) with
        {
            TrackLengthMeters = 10.0,
        };
        Assert.Null(CentoRoutePlanner.LongestRoute(tiny, tiny.Streets));
    }
}
