using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Unity's loyalty-to-class windows (CarHaulOfferService.
/// ClassRangeForPct): the band edges are exclusive on the way up, so 200% is
/// the first percent that opens the 1-8 window.</summary>
public sealed class CarHaulClassBandTests
{
    [Theory]
    [InlineData(0.0, 1, 6)]
    [InlineData(100.0, 1, 6)]
    [InlineData(199.9, 1, 6)]
    [InlineData(200.0, 1, 8)]
    [InlineData(299.9, 1, 8)]
    [InlineData(300.0, 1, 10)]
    [InlineData(400.0, 1, 12)]
    [InlineData(500.0, 1, 14)]
    [InlineData(600.0, 1, 16)]
    [InlineData(700.0, 5, 17)]
    [InlineData(799.9, 5, 17)]
    [InlineData(800.0, 6, 21)]
    [InlineData(900.0, 7, 21)]
    [InlineData(1000.0, 7, 21)]
    public void BandEdges_MatchUnity(double percent, int minClass, int maxClass)
    {
        CarHaulClassBand band = CarHaulClassBand.For(percent);
        Assert.Equal(minClass, band.MinClass);
        Assert.Equal(maxClass, band.MaxClass);
    }

    [Fact]
    public void Contains_IsInclusive()
    {
        CarHaulClassBand band = CarHaulClassBand.For(700.0);
        Assert.False(band.Contains(4));
        Assert.True(band.Contains(5));
        Assert.True(band.Contains(17));
        Assert.False(band.Contains(18));
    }
}
