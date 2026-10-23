using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The Unity pacing curves behind the generated business POIs.</summary>
public sealed class PoiSpawnRuleTests
{
    [Fact]
    public void Streets_FirstThenEveryInterval()
    {
        var rule = new PoiSpawnRule(
            PoiPacing.Streets, FirstStreets: 6, IntervalStreets: 47);

        Assert.Equal(0, rule.ExpectedCount(0, 0));
        Assert.Equal(0, rule.ExpectedCount(5, 0));
        Assert.Equal(1, rule.ExpectedCount(6, 0));
        Assert.Equal(1, rule.ExpectedCount(52, 0));
        Assert.Equal(2, rule.ExpectedCount(53, 0));
        Assert.Equal(3, rule.ExpectedCount(100, 0));
    }

    [Fact]
    public void JobsFirstThenStreets_NeedsTheJobOpeningBeforeAnyStreetInterval()
    {
        var rule = new PoiSpawnRule(
            PoiPacing.JobsFirstThenStreets,
            FirstStreets: 3, IntervalStreets: 36, FirstJobs: 36);

        // Unity's mechanic: no street count opens the first one.
        Assert.Equal(0, rule.ExpectedCount(50, 35));
        Assert.Equal(1, rule.ExpectedCount(3, 36));
        // The first street interval starts one full interval after the offset.
        Assert.Equal(1, rule.ExpectedCount(38, 36));
        Assert.Equal(2, rule.ExpectedCount(39, 36));
        Assert.Equal(3, rule.ExpectedCount(75, 36));
    }

    [Fact]
    public void Either_TakesTheJobOpeningOrTheStreetCurve()
    {
        var rule = new PoiSpawnRule(
            PoiPacing.Either, FirstStreets: 37, IntervalStreets: 37, FirstJobs: 10);

        Assert.Equal(0, rule.ExpectedCount(0, 9));
        Assert.Equal(1, rule.ExpectedCount(0, 10));
        Assert.Equal(1, rule.ExpectedCount(37, 0));
        Assert.Equal(1, rule.ExpectedCount(37, 10));
        Assert.Equal(2, rule.ExpectedCount(74, 0));
        Assert.Equal(2, rule.ExpectedCount(74, 10));
    }

    [Fact]
    public void None_NeverSpawns()
    {
        Assert.Equal(0, new PoiSpawnRule().ExpectedCount(1000, 1000));
        Assert.Equal(
            0, new PoiSpawnRule(PoiPacing.None, 1, 1, 1).ExpectedCount(1000, 1000));
    }

    [Fact]
    public void OwnedCategory_NeedsTheHubAndStartsAtOne()
    {
        var rule = new PoiSpawnRule(
            PoiPacing.OwnedCategory, IntervalStreets: 20);

        // Unity's backfill: buying the hub grants one store even at zero
        // streets; the interval adds one after every full block.
        Assert.Equal(0, rule.ExpectedCount(0, 0, categoryOwned: false));
        Assert.Equal(1, rule.ExpectedCount(0, 0, categoryOwned: true));
        Assert.Equal(1, rule.ExpectedCount(19, 0, categoryOwned: true));
        Assert.Equal(1, rule.ExpectedCount(20, 0, categoryOwned: true));
        Assert.Equal(2, rule.ExpectedCount(40, 0, categoryOwned: true));
    }

    [Fact]
    public void NegativeCounters_AnswerZero()
    {
        var rule = new PoiSpawnRule(
            PoiPacing.Streets, FirstStreets: 6, IntervalStreets: 47);

        Assert.Equal(0, rule.ExpectedCount(-10, -10));
    }
}
