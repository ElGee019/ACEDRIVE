using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia reputation growth (PORTPLAN 49, Unity
/// MilleMigliaManager.ClubReputationTickerLoop + ReputationCalculator): the
/// per-tick factor, its growth over the kilometres driven and the guards around
/// bad input.</summary>
public sealed class CentoReputationTests
{
    [Fact]
    public void GrowthFactor_AddsTheCarClassAndTheGarageSize()
    {
        Assert.Equal(1.0 + (12.0 / 1_000_000.0) + (1.0 / 1_000_000.0),
            CentoReputation.GrowthFactor(12, 1), precision: 12);
        Assert.Equal(1.0, CentoReputation.GrowthFactor(0, 0));
        // A hand-edited negative reading never shrinks the reputation.
        Assert.Equal(1.0, CentoReputation.GrowthFactor(-5, -2));
    }

    [Fact]
    public void AfterDistance_GrowsWithTheDistanceDriven()
    {
        double start = 1.0;
        double afterOne = CentoReputation.AfterDistance(start, 12, 1, 1.0);
        double afterTen = CentoReputation.AfterDistance(start, 12, 1, 10.0);

        Assert.True(afterOne > start);
        Assert.True(afterTen > afterOne);
        // Unity's gain is a few thousandths per kilometre, never a tier.
        Assert.InRange(afterOne - start, 0.0, 0.01);
    }

    [Fact]
    public void AfterDistance_KeepsTheStartOnUnusableInput()
    {
        Assert.Equal(0.0, CentoReputation.AfterDistance(0.0, 12, 1, 5.0));
        Assert.Equal(1.0, CentoReputation.AfterDistance(1.0, 12, 1, 0.0));
        Assert.Equal(1.0, CentoReputation.AfterDistance(1.0, 12, 1, -5.0));
        // A torn distance read is no distance at all.
        Assert.Equal(1.0, CentoReputation.AfterDistance(1.0, 12, 1, double.NaN));
        Assert.Equal(1.0, CentoReputation.AfterDistance(1.0, 12, 1, double.PositiveInfinity));
    }

    [Fact]
    public void AfterDistance_ChargesOneTickPerHundredthKilometre()
    {
        // One tick is 0.01 km: a step of that length is exactly one Unity
        // multiplication, and a kilometre is a hundred of them.
        double factor = CentoReputation.GrowthFactor(20, 3);

        Assert.Equal(
            factor,
            CentoReputation.AfterDistance(1.0, 20, 3, CentoReputation.TickKilometres),
            precision: 12);
        Assert.Equal(
            Math.Pow(factor, 100.0),
            CentoReputation.AfterDistance(1.0, 20, 3, 1.0),
            precision: 12);
    }
}
