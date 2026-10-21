using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The errand completion rule (trunk contents decide the phase).</summary>
public sealed class ErrandTests
{
    private static PointOfInterest Poi(params string[] guaranteed) =>
        new("poi_1", "Max's Grocery Store", new AddressLocation("Street 1", 1, HouseSide.Left),
            GuaranteedItems: guaranteed);

    [Fact]
    public void IsComplete_WhenEveryGuaranteedItemIsCarried()
    {
        var trunk = new Trunk([new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);

        Assert.True(Errand.IsComplete(Poi("MAPLE SYRUP"), trunk));
    }

    [Fact]
    public void IsComplete_NeedsEveryItemOfAMultiItemErrand()
    {
        var trunk = new Trunk([new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);

        Assert.False(Errand.IsComplete(Poi("MAPLE SYRUP", "PANCAKE MIX"), trunk));
    }

    [Fact]
    public void IsComplete_IgnoresAnEmptyStack()
    {
        var trunk = new Trunk([new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 0 }]);

        Assert.False(Errand.IsComplete(Poi("MAPLE SYRUP"), trunk));
    }

    [Fact]
    public void IsComplete_IsNeverTrueWithoutGuaranteedItems()
    {
        Assert.False(Errand.IsComplete(Poi(), new Trunk()));
    }

    [Fact]
    public void IsComplete_MatchesNamesCaseInsensitively()
    {
        var trunk = new Trunk([new TrunkItem("Maple Syrup", 0.4) { Quantity = 2 }]);

        Assert.True(Errand.IsComplete(Poi("MAPLE SYRUP"), trunk));
    }
}
