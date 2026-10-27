using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Diminishing-returns tiers (Unity XpEarningHelper): full rate
/// below 100 XP, −1% per 100-XP tier, floored at 1%.</summary>
public sealed class XpEarningTests
{
    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(99.99, 1.0)]
    [InlineData(100.0, 0.99)]
    [InlineData(250.0, 0.98)]
    [InlineData(1000.0, 0.90)]
    public void Multiplier_Tiers(double currentXp, double expected) =>
        Assert.Equal(expected, XpEarning.GetMultiplier(currentXp), precision: 9);

    [Fact]
    public void Multiplier_FloorsAtOnePercent()
    {
        Assert.Equal(0.01, XpEarning.GetMultiplier(10000.0), precision: 9);
        Assert.Equal(0.01, XpEarning.GetMultiplier(100000.0), precision: 9);
    }

    [Fact]
    public void Diminish_AppliesMultiplier() =>
        Assert.Equal(0.99, XpEarning.Diminish(1.0, 100.0), precision: 9);

    [Fact]
    public void Compute_FullFormula()
    {
        // (8.2 x class 3 x rarity 1) / 100 x SK 100% x 1.0 = 0.246.
        Assert.Equal(0.246, XpEarning.Compute(8.2, 3, 1, 100, 0.0), precision: 9);
    }

    [Fact]
    public void Compute_StreetKnowledgeScales()
    {
        Assert.Equal(0.041, XpEarning.Compute(8.2, 1, 1, 50, 0.0), precision: 9);
    }

    // Delivery twin: the taxi formula at a fifth, so the goods run stays the
    // lesser job without paying an order of magnitude less.
    [Fact]
    public void ComputeDelivery_PaysAFifthOfTheTaxiRate()
    {
        // (8.2 km x class 10 x rarity 1) / 100 x SK 100% x 0.2 = 0.164.
        Assert.Equal(0.164, XpEarning.ComputeDelivery(8.2, 10, 1, 100, 0.0), precision: 9);
        Assert.Equal(0.082, XpEarning.ComputeDelivery(8.2, 10, 1, 50, 0.0), precision: 9);
        // The diminishing tier still applies on top of the fifth (2 full
        // tiers in at 250 XP).
        Assert.Equal(0.164 * 0.98, XpEarning.ComputeDelivery(8.2, 10, 1, 100, 250.0), precision: 9);
    }

    [Fact]
    public void ComputeDelivery_IsAlwaysAFifthOfTheMatchingTaxiAward()
    {
        double taxi = XpEarning.Compute(12.5, 14, 6, 80, 40.0);
        double delivery = XpEarning.ComputeDelivery(12.5, 14, 6, 80, 40.0);
        Assert.Equal(taxi * XpEarning.DeliveryShare, delivery, precision: 12);
    }
}
