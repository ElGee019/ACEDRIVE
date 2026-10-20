using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Brand sponsorship curve, ported 1:1 from Unity
/// TaxiReceiptScreen.CalculateBrandLoyaltyBonus (input km halved, 1% base,
/// 1000% cap).</summary>
public sealed class BrandLoyaltyTests
{
    [Theory]
    [InlineData(0.0, 0.01)]
    [InlineData(0.5, 0.01)]
    [InlineData(1.9, 0.01)]
    [InlineData(2.0, 0.02)]
    [InlineData(8.2, 0.05)]
    [InlineData(10.0, 0.06)]
    public void KnownPoints(double brandKm, double expected) =>
        Assert.Equal(expected, BrandLoyalty.Rate(brandKm), precision: 9);

    [Fact]
    public void JustPastFiveKmScaled_StaysAtFivePercent()
    {
        // 11 km scales to 5.5: the 100% boundary cuts through the next
        // step, so the curve hands back the 5% it banked at 5 km flat.
        Assert.Equal(0.05, BrandLoyalty.Rate(11.0), precision: 9);
    }

    [Fact]
    public void HugeMileage_CapsAtTenfold()
    {
        Assert.Equal(10.0, BrandLoyalty.Rate(1_000_000.0), precision: 9);
        Assert.Equal(BrandLoyalty.MaxRate, BrandLoyalty.Rate(1_000_000.0), precision: 9);
    }

    [Fact]
    public void Rate_NeverNegative()
    {
        foreach (double km in new double[] { -5.0, 0.0, 3.3, 50.0, 500.0 })
        {
            Assert.InRange(BrandLoyalty.Rate(km), 0.0, BrandLoyalty.MaxRate);
        }
    }
}
