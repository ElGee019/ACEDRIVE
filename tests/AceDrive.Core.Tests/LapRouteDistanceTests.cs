using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The shared calculated-route rule (PORTPLAN items 11 and 19): the optional
/// approach plus every planned leg, measured on the closed lap.
/// </summary>
public sealed class LapRouteDistanceTests
{
    private static readonly AddressMap Map = AddressMap.Default;

    // 0 m, 120 m and 964.5 m along the 1,929 m lap; the last two houses sit
    // 30 m apart past the Street 2 boundary.
    private static readonly AddressLocation First =
        new("Street 1", 1, HouseSide.Right);
    private static readonly AddressLocation Near =
        new("Street 1", 5, HouseSide.Right);
    private static readonly AddressLocation Across =
        new("Street 2", 1, HouseSide.Right);
    private static readonly AddressLocation AcrossNext =
        new("Street 2", 2, HouseSide.Right);

    [Fact]
    public void Kilometers_SumsTheApproachAndEveryLegInOrder()
    {
        // 120 m approach + 844.5 m + 30 m: the route runs the way the stops are
        // listed, never the shorter order.
        Assert.Equal(
            0.9945,
            LapRouteDistance.Kilometers(
                Map, 0.0, [Near, Across, AcrossNext], 1929.0),
            precision: 3);
    }

    [Fact]
    public void Kilometers_WithoutAStartPosition_IsTheLegsOnly()
    {
        Assert.Equal(
            0.8745,
            LapRouteDistance.Kilometers(
                Map, null, [Near, Across, AcrossNext], 1929.0),
            precision: 3);
    }

    [Fact]
    public void Kilometers_SkipsAnUnresolvedDoorInsteadOfLosingTheWholeRoute()
    {
        // The middle stop has no door: the leg it would have split is measured
        // straight from Near to Across, so the route still prices 120 + 844.5.
        Assert.Equal(
            0.9645,
            LapRouteDistance.Kilometers(
                Map, 0.0, [Near, null, Across], 1929.0),
            precision: 3);
    }

    [Fact]
    public void Kilometers_MeasuresEachLegTheShortWayAroundTheLap()
    {
        // First (0 m) is behind Across (964.5 m) on the printed lap but 964.5 m
        // away the short way, never the 1,809 m the long way round.
        Assert.Equal(
            0.9645,
            LapRouteDistance.Kilometers(Map, null, [Across, First], 1929.0),
            precision: 3);
    }

    [Fact]
    public void Kilometers_WithNoResolvableDoorOrTrack_IsZero()
    {
        Assert.Equal(0.0, LapRouteDistance.Kilometers(Map, 0.0, [], 1929.0));
        Assert.Equal(
            0.0,
            LapRouteDistance.Kilometers(Map, 0.0, [null, null], 1929.0));
        Assert.Equal(
            0.0,
            LapRouteDistance.Kilometers(Map, 0.0, [Near, Across], 0.0));
        Assert.Equal(
            0.0,
            LapRouteDistance.Kilometers(Map, 0.0, [Near, Across], double.NaN));
    }
}
