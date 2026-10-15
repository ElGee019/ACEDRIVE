using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Tier bands + first-name rule behind the receipt box (PORTPLAN 45).</summary>
public sealed class FriendshipLevelsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(9, 2)]
    [InlineData(10, 3)]
    [InlineData(12, 3)]
    [InlineData(13, 4)]
    [InlineData(15, 4)]
    [InlineData(16, 5)]
    [InlineData(30, 5)]
    public void GetTier_MapsBoundaries(int level, int expected) =>
        Assert.Equal(expected, FriendshipLevels.GetTier(level));

    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(2, 3, true)]
    [InlineData(9, 10, true)]
    [InlineData(12, 13, true)]
    [InlineData(15, 16, true)]
    [InlineData(5, 6, false)]
    [InlineData(10, 11, false)]
    [InlineData(1, 2, false)]
    public void TierIncreased_Crossings(int before, int after, bool expected) =>
        Assert.Equal(expected, FriendshipLevels.TierIncreased(before, after));

    [Fact]
    public void DisplayFirstName_SignoreKeepsFullName() =>
        Assert.Equal("Signore Moro", FriendshipLevels.DisplayFirstName("Signore Moro"));

    [Fact]
    public void DisplayFirstName_OthersUseFirstToken() =>
        Assert.Equal("Finn", FriendshipLevels.DisplayFirstName("Finn Marsh"));

    [Fact]
    public void Builder_CarriesFriendshipToReceipt()
    {
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, 1, 1, 1, 100, true, 1, FareTable.Default, 0.0,
            new FriendshipResult("Finn Marsh", 5, true));
        Assert.True(receipt.HasFriendship);
        Assert.Equal("Finn Marsh", receipt.FriendshipName);
        Assert.Equal(5, receipt.FriendshipLevel);
        Assert.True(receipt.FriendshipTierIncreased);
    }

    [Fact]
    public void Builder_WithoutFriendship_HidesBox()
    {
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, 1, 1, 1, 100, true, 1, FareTable.Default, 0.0);
        Assert.False(receipt.HasFriendship);
        Assert.Equal("", receipt.FriendshipName);
    }
}
