using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Golden values for the time-based haul pay: class band → tier,
/// rating × tier percent of the 0.83 base into a per-minute rate, drive
/// minutes price the total (Unity CarHaulingScreen.CalculateEarnings).</summary>
public sealed class CarHaulReceiptTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    [InlineData(10, 3)]
    [InlineData(11, 4)]
    [InlineData(12, 4)]
    [InlineData(13, 5)]
    [InlineData(14, 5)]
    [InlineData(15, 6)]
    [InlineData(16, 6)]
    [InlineData(17, 7)]
    [InlineData(21, 7)]
    public void ClassBands_MapToTiers(int carClass, int tier)
    {
        Assert.Equal(tier, CarHaulReceiptBuilder.TierForCarClass(carClass));
    }

    [Fact]
    public void BelowClass1_StillTakesTheLowestBand()
    {
        Assert.Equal(1, CarHaulReceiptBuilder.TierForCarClass(0));
    }

    [Fact]
    public void Earnings_RateTimesMinutes()
    {
        // Rating 50 on a class-15 car (tier 6): 0.83 * 50*6/100 = 2.49/min,
        // 30 minutes earn 74.70.
        CarHaulReceipt receipt = CarHaulReceiptBuilder.Build(
            "Test Hatch", carClass: 15, driverRating: 50, driveMinutes: 30);
        Assert.Equal(6, receipt.Tier);
        Assert.Equal(2.49, receipt.RatePerMin);
        Assert.Equal(74.70, receipt.TotalEarned);
        Assert.Equal(0, receipt.DriveHours);
        Assert.Equal(30, receipt.DriveMinutesRemainder);
    }

    [Fact]
    public void Earnings_TopBand_RoundsToCents()
    {
        // Rating 100, class 17 (tier 7): 0.83 * 7 = 5.81/min; 60 minutes
        // earn 348.60.
        CarHaulReceipt receipt = CarHaulReceiptBuilder.Build(
            "Test Halo", carClass: 17, driverRating: 100, driveMinutes: 60);
        Assert.Equal(7, receipt.Tier);
        Assert.Equal(5.81, receipt.RatePerMin);
        Assert.Equal(348.60, receipt.TotalEarned);
        Assert.Equal(1, receipt.DriveHours);
        Assert.Equal(0, receipt.DriveMinutesRemainder);
    }

    [Fact]
    public void ZeroMinutes_EarnsNothing()
    {
        CarHaulReceipt receipt = CarHaulReceiptBuilder.Build(
            "Test Hatch", carClass: 15, driverRating: 50, driveMinutes: 0);
        Assert.Equal(2.49, receipt.RatePerMin);
        Assert.Equal(0.0, receipt.TotalEarned);
    }

    [Fact]
    public void AnywayInputs_Clamp()
    {
        CarHaulReceipt receipt = CarHaulReceiptBuilder.Build(
            "Test Hatch", carClass: -3, driverRating: 105, driveMinutes: -5);
        Assert.Equal(1, receipt.Tier);
        Assert.Equal(100, receipt.DriverRating);
        Assert.Equal(0, receipt.DriveMinutes);
        Assert.Equal(0.0, receipt.TotalEarned);
    }

    [Fact]
    public void WithoutDealerOwner_FriendshipHidden()
    {
        CarHaulReceipt receipt = CarHaulReceiptBuilder.Build(
            "Test Hatch", carClass: 15, driverRating: 50, driveMinutes: 30);
        Assert.False(receipt.HasFriendship);
    }

    [Fact]
    public void WithDealerOwner_FriendshipRidesTheReceipt()
    {
        CarHaulReceipt receipt = CarHaulReceiptBuilder.Build(
            "Test Hatch", carClass: 15, driverRating: 50, driveMinutes: 30,
            dealerOwnerName: "Elena Marchetti", dealerFriendshipLevel: 2,
            dealerFriendshipTierIncreased: true);
        Assert.True(receipt.HasFriendship);
        Assert.Equal(2, receipt.DealerFriendshipLevel);
        Assert.True(receipt.DealerFriendshipTierIncreased);
    }
}
