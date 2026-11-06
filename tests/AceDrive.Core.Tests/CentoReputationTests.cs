using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia reputation growth (PORTPLAN 49, Unity
/// MilleMigliaManager.ClubReputationTickerLoop + ReputationCalculator): the
/// per-tick factor with its assertivity term, its growth over the kilometres
/// driven and the guards around bad input.</summary>
public sealed class CentoReputationTests
{
    [Fact]
    public void GrowthFactor_AddsAssertivityTheCarClassAndTheGarageSize()
    {
        Assert.Equal(1.0 + (12.0 / 1_000_000.0) + (1.0 / 1_000_000.0),
            CentoReputation.GrowthFactor(12, 1, 0.0), precision: 12);
        Assert.Equal(
            1.0 + (2.0 / 1_000_000.0) + (12.0 / 1_000_000.0) + (1.0 / 1_000_000.0),
            CentoReputation.GrowthFactor(12, 1, 2.0),
            precision: 12);
        Assert.Equal(1.0, CentoReputation.GrowthFactor(0, 0, 0.0));
        // A hand-edited negative reading never shrinks the reputation.
        Assert.Equal(1.0, CentoReputation.GrowthFactor(-5, -2, -4.0));
    }

    [Fact]
    public void GrowthFactor_TruncatesAssertivityLikeUnitysIntCast()
    {
        // Unity's (int) cast keeps whole points: 1.9 still counts as one, and a
        // pair of declined requests (0.2) buys nothing until the tenth.
        Assert.Equal(
            CentoReputation.GrowthFactor(0, 0, 1.0),
            CentoReputation.GrowthFactor(0, 0, 1.9),
            precision: 12);
        Assert.Equal(
            CentoReputation.GrowthFactor(0, 0, 0.0),
            CentoReputation.GrowthFactor(0, 0, 0.9),
            precision: 12);
        Assert.Equal(
            CentoReputation.GrowthFactor(0, 0, 2.0),
            CentoReputation.GrowthFactor(0, 0, 2.7),
            precision: 12);
        // A torn value reads as no assertivity, never as a NaN factor.
        Assert.Equal(1.0, CentoReputation.GrowthFactor(0, 0, double.NaN));
    }

    [Fact]
    public void AfterDistance_GrowsWithTheDistanceDriven()
    {
        double start = 1.0;
        double afterOne = CentoReputation.AfterDistance(start, 12, 1, 1.0, 1.0);
        double afterTen = CentoReputation.AfterDistance(start, 12, 1, 1.0, 10.0);

        Assert.True(afterOne > start);
        Assert.True(afterTen > afterOne);
        // Unity's gain is a few thousandths per kilometre, never a tier.
        Assert.InRange(afterOne - start, 0.0, 0.01);
    }

    [Fact]
    public void AfterDistance_KeepsTheStartOnUnusableInput()
    {
        Assert.Equal(0.0, CentoReputation.AfterDistance(0.0, 12, 1, 0.0, 5.0));
        Assert.Equal(1.0, CentoReputation.AfterDistance(1.0, 12, 1, 0.0, 0.0));
        Assert.Equal(1.0, CentoReputation.AfterDistance(1.0, 12, 1, 0.0, -5.0));
        // A torn distance read is no distance at all.
        Assert.Equal(1.0, CentoReputation.AfterDistance(1.0, 12, 1, 0.0, double.NaN));
        Assert.Equal(1.0, CentoReputation.AfterDistance(1.0, 12, 1, 0.0, double.PositiveInfinity));
    }

    [Fact]
    public void AfterDistance_ChargesOneTickPerHundredthKilometre()
    {
        // One tick is 0.01 km: a step of that length is exactly one Unity
        // multiplication, and a kilometre is a hundred of them.
        double factor = CentoReputation.GrowthFactor(20, 3, 0.0);

        Assert.Equal(
            factor,
            CentoReputation.AfterDistance(
                1.0, 20, 3, 0.0, CentoReputation.TickKilometres),
            precision: 12);
        Assert.Equal(
            Math.Pow(factor, 100.0),
            CentoReputation.AfterDistance(1.0, 20, 3, 0.0, 1.0),
            precision: 12);
    }
}
