using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Insurance daily-cost math (PORTPLAN item 34, port of Unity
/// InsurancePricing): base × the +15% increase, the rating discount and the
/// CR1 floor.</summary>
public sealed class InsurancePricingTests
{
    private static readonly InsuranceRules Rules = InsuranceRules.Default;

    [Fact]
    public void Default_KeepsUnitysShippedNumbers()
    {
        Assert.Equal(0.0005, Rules.PremiumRate);
        Assert.Equal(1.15, Rules.BaseIncreaseFactor);
        Assert.Equal(1.0, Rules.MinimumDailyCost);
        Assert.Equal(1, Rules.ChargeHour);
    }

    [Fact]
    public void BaseDailyCost_KeepsUnitysExample()
    {
        // Unity's doc example: a CR12000 car at rate 0.0005 → CR6.00 → +15% → CR6.90.
        Assert.Equal(6.90, InsurancePricing.BaseDailyCost(12000.0, Rules), 6);
    }

    [Fact]
    public void DiscountedDailyCost_AppliesTheRatingPercent()
    {
        // Unity's example: CR6.90 base at rating 21 → CR6.90 − 21% = CR5.45.
        Assert.Equal(5.45, InsurancePricing.DiscountedDailyCost(12000.0, 21.0, Rules), 2);
    }

    [Fact]
    public void DiscountedDailyCost_FloorsAtTheMinimum()
    {
        // A free or worthless car still pays the CR1 floor.
        Assert.Equal(1.0, InsurancePricing.DiscountedDailyCost(0.0, 0.0, Rules), 10);
        // A 100% rating cannot discount below the floor either.
        Assert.Equal(1.0, InsurancePricing.DiscountedDailyCost(12000.0, 100.0, Rules), 10);
    }

    [Fact]
    public void DiscountedDailyCost_ClampsTheRating()
    {
        // Above 100 and below 0 clamp to the ends of the discount band.
        Assert.Equal(
            InsurancePricing.DiscountedDailyCost(12000.0, 100.0, Rules),
            InsurancePricing.DiscountedDailyCost(12000.0, 250.0, Rules), 10);
        Assert.Equal(
            InsurancePricing.DiscountedDailyCost(12000.0, 0.0, Rules),
            InsurancePricing.DiscountedDailyCost(12000.0, -30.0, Rules), 10);
    }

    [Fact]
    public void NegativeOrNonFinitePrice_ReadsTheFloor()
    {
        Assert.Equal(1.0, InsurancePricing.DiscountedDailyCost(-500.0, 0.0, Rules), 10);
        Assert.Equal(0.0, InsurancePricing.BaseDailyCost(double.NaN, Rules), 10);
    }
}
