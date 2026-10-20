using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The login-bonus curve pinned against Unity's
/// ConsecutiveDayBonus (item 47): doubles multiply in the same order, and
/// the AwayFromZero round of the product is the CR payout.</summary>
public sealed class ConsecutiveDayBonusTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]   // 1 * 1.95 = 1.95  -> 2
    [InlineData(3, 4)]   // 1.95 * 1.90      -> 4
    [InlineData(4, 7)]   // * 1.85            -> 7
    [InlineData(5, 12)]  // * 1.80            -> 12
    [InlineData(6, 22)]  // * 1.75            -> 22
    [InlineData(7, 37)]  // * 1.70            -> 37
    [InlineData(10, 150)] // * 1.55           -> 150
    public void Bonus_FollowsTheCurve(int days, int expected)
    {
        Assert.Equal(expected, ConsecutiveDayBonus.ComputeBonus(days));
    }

    [Fact]
    public void Bonus_PlatformsFromDayTwentyOne()
    {
        // The factor floor (1.0) lands at n=21, so the running total stops
        // growing: Unity's "the streak never decays" plateaus.
        int plateau = ConsecutiveDayBonus.ComputeBonus(21);
        Assert.Equal(plateau, ConsecutiveDayBonus.ComputeBonus(22));
        Assert.Equal(plateau, ConsecutiveDayBonus.ComputeBonus(40));
    }

    [Fact]
    public void Bonus_NeverNegative()
    {
        Assert.Equal(1, ConsecutiveDayBonus.ComputeBonus(0));
        Assert.Equal(1, ConsecutiveDayBonus.ComputeBonus(-3));
    }
}
