using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The taxi job distance rule (PORTPLAN item 11): approach plus ride.</summary>
public sealed class TaxiJobDistanceTests
{
    private static readonly AddressMap Map = AddressMap.Default;
    private static readonly AddressLocation Pickup =
        new("Street 1", 5, HouseSide.Right);
    private static readonly AddressLocation Dropoff =
        new("Street 2", 1, HouseSide.Right);

    [Fact]
    public void Kilometers_AddsTheApproachToTheRide()
    {
        // Start line -> Street 1 No. 5 = 120 m; No. 5 -> Street 2 No. 1 is
        // 844.5 m the short way, so the job is 964.5 m.
        Assert.Equal(
            0.9645,
            TaxiJobDistance.Kilometers(Map, 0.0, Pickup, Dropoff, 1929.0),
            precision: 3);
    }

    [Fact]
    public void Kilometers_WithoutAStartPosition_IsTheRideOnly()
    {
        Assert.Equal(
            0.8445,
            TaxiJobDistance.Kilometers(Map, null, Pickup, Dropoff, 1929.0),
            precision: 3);
    }

    [Fact]
    public void Kilometers_MeasuresTheApproachTheShortWayAroundTheLap()
    {
        // Start at the line with the pickup 120 m in: the approach is 120 m,
        // never the 1,809 m the other way around the closed lap.
        Assert.Equal(
            0.120,
            TaxiJobDistance.Kilometers(Map, 0.0, Pickup, Pickup, 1929.0),
            precision: 3);
    }

    [Fact]
    public void Kilometers_InvalidDoorsOrGeometry_IsZero()
    {
        Assert.Equal(0.0, TaxiJobDistance.Kilometers(Map, 0.5, null, Dropoff, 1929.0));
        Assert.Equal(0.0, TaxiJobDistance.Kilometers(Map, 0.5, Pickup, null, 1929.0));
        Assert.Equal(0.0, TaxiJobDistance.Kilometers(Map, 0.5, Pickup, Dropoff, 0.0));
        Assert.Equal(0.0, TaxiJobDistance.Kilometers(
            Map, 0.5, new AddressLocation("Elsewhere", 1, HouseSide.Right), Dropoff, 1929.0));
    }
}
