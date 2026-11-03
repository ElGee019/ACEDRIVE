using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The dealer's trade-in rule (PORTPLAN 29's sell half): catalogue price scaled
/// by engine health and the car's leaderboard ranking.
/// </summary>
public sealed class DealerSaleRulesTests
{
    private static readonly CarSpec Car = new(
        "ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Price: 12000.0);

    [Fact]
    public void Value_HealthyTopRankedCar_KeepsItsWholePrice()
    {
        Assert.Equal(12000.0, DealerSaleRules.Value(Car, 0.0, 1.0));
    }

    [Fact]
    public void Value_EngineWear_TakesTheConditionShare()
    {
        // 25% broken = 75% of the price, the garage's own display rule.
        Assert.Equal(9000.0, DealerSaleRules.Value(Car, 0.25, 1.0));
    }

    [Fact]
    public void Value_LeaderboardRanking_ScalesThePrice()
    {
        // Half the ranking, half the money: the lower the rank the lower the
        // price, and a healthy car at 50% is still worth half its price.
        Assert.Equal(6000.0, DealerSaleRules.Value(Car, 0.0, 0.5));
    }

    [Fact]
    public void Value_BothTerms_Multiply()
    {
        Assert.Equal(4500.0, DealerSaleRules.Value(Car, 0.25, 0.5));
    }

    [Fact]
    public void Value_RoundsToTheCent()
    {
        Assert.Equal(1234.57, DealerSaleRules.Value(new CarSpec(
            "x", 2000, "X", "x", Price: 1234.5678), 0.0, 1.0));
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void Value_BrokenPercentOutsideTheBand_Clamps(double broken)
    {
        double expected = double.IsNaN(broken) ? 12000.0 : broken < 0.0 ? 12000.0 : 0.0;
        Assert.Equal(expected, DealerSaleRules.Value(Car, broken, 1.0));
    }

    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(2.0, 12000.0)]
    [InlineData(double.NaN, 0.0)]
    public void Value_RankingOutsideTheBand_Clamps(double ranking, double expected)
    {
        Assert.Equal(expected, DealerSaleRules.Value(Car, 0.0, ranking));
    }

    [Fact]
    public void Value_NoUsablePrice_IsWorthNothing()
    {
        Assert.Equal(0.0, DealerSaleRules.Value(null, 0.0, 1.0));
        Assert.Equal(0.0, DealerSaleRules.Value(Car with { Price = 0.0 }, 0.0, 1.0));
        Assert.Equal(0.0, DealerSaleRules.Value(Car with { Price = double.NaN }, 0.0, 1.0));
    }
}
