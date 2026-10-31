using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The two offer gates: the switchable loyalty floor (off in the
/// development build, Unity's 100% when it lands) and the switchable one-haul-
/// per-game-day stamp.</summary>
public sealed class CarHaulPolicyTests
{
    private static readonly DateTime Noon = new(1924, 6, 3, 12, 0, 0);

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(0.5, true)]
    [InlineData(250.0, true)]
    public void Development_UnlocksAnyDrivenBrand(double percent, bool unlocks)
    {
        Assert.Equal(unlocks, CarHaulPolicy.Development.UnlocksOffer(percent));
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(99.9, false)]
    [InlineData(100.0, true)]
    [InlineData(250.0, true)]
    public void Unity_UnlocksAtTheHundredPercentFloor(double percent, bool unlocks)
    {
        Assert.Equal(unlocks, CarHaulPolicy.Unity.UnlocksOffer(percent));
    }

    [Fact]
    public void Unlocks_RejectsNotANumber()
    {
        Assert.False(CarHaulPolicy.Development.UnlocksOffer(double.NaN));
        Assert.False(CarHaulPolicy.Unity.UnlocksOffer(double.NaN));
    }

    [Fact]
    public void Development_HasNoDailyCap()
    {
        Assert.True(CarHaulPolicy.Development.CanOffer(
            Noon, lastDeliveredOn: Noon.AddHours(-2), haulInProgress: false));
    }

    [Fact]
    public void Development_StillBlocksASecondHaulInFlight()
    {
        Assert.False(CarHaulPolicy.Development.CanOffer(
            Noon, lastDeliveredOn: null, haulInProgress: true));
    }

    [Fact]
    public void Unity_BlocksASecondHaulOnTheSameGameDate()
    {
        // Unity stamps the game day, not the minute: a haul delivered at 08:00
        // closes the offer until the calendar moves on.
        Assert.False(CarHaulPolicy.Unity.CanOffer(
            Noon, Noon.AddHours(-4), haulInProgress: false));
    }

    [Fact]
    public void Unity_OpensAgainTheNextGameDate()
    {
        Assert.True(CarHaulPolicy.Unity.CanOffer(
            Noon.AddDays(1), Noon, haulInProgress: false));
    }
}
