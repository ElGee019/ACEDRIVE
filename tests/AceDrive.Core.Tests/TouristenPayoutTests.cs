using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The delivered order's money rule: the goods at their own price on an
/// ordinary day, doubled while the real-world Nürburgring calendar runs an
/// event, and never cheaper than the goods when the factor is malformed.
/// </summary>
public sealed class TouristenPayoutTests
{
    private static TouristenOrder Order() => new(
    [
        new TouristenOrderLine("coffee", "TouristenItemCoffee", 2, 3.5),
        new TouristenOrderLine("soup", "TouristenItemSoup", 1, 8.5),
    ]);

    [Fact]
    public void Of_PaysTheOrderAtFullPriceOrWithTheRaceWeekendBonus()
    {
        // 2 x 3.50 + 8.50 = 15.50.
        Assert.Equal(15.5, TouristenPayout.Of(Order(), 1.0));
        Assert.Equal(31.0, TouristenPayout.Of(Order(), 2.0));
    }

    [Fact]
    public void Of_LeavesThePriceAloneWhenTheFactorIsNotUsable()
    {
        Assert.Equal(15.5, TouristenPayout.Of(Order(), 0.25));
        Assert.Equal(15.5, TouristenPayout.Of(Order(), double.NaN));
    }
}
