using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// One trunk bite (Unity YouScreen.OnTrunkItemClicked): the unit leaves the
/// stack first, the catalogue's hunger value lowers the meter, and a stack the
/// trunk does not hold or the catalogue no longer defines changes nothing.
/// </summary>
public sealed class TrunkEatTests
{
    private static readonly StoreItem Maple =
        new("MAPLE SYRUP", 45.99, 0.4, 3, "groceries");

    [Fact]
    public void Eat_ConsumesOneUnitAndLowersTheHunger()
    {
        var trunk = new Trunk([new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 2 }]);
        var hunger = new Hunger(
            HungerRules.Default, new HungerState(10, ThresholdTriggered: false));

        bool eaten = TrunkEat.Eat(
            trunk, hunger, new StoreCatalog([Maple], 20), "MAPLE SYRUP");

        Assert.True(eaten);
        Assert.Equal(1, trunk.Items.Single().Quantity);
        Assert.Equal(7, hunger.Value);
    }

    [Fact]
    public void Eat_LastUnitDropsTheStack()
    {
        var trunk = new Trunk([new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hunger = new Hunger(HungerRules.Default);

        Assert.True(TrunkEat.Eat(
            trunk, hunger, new StoreCatalog([Maple], 20), "MAPLE SYRUP"));
        Assert.Empty(trunk.Items);
    }

    [Fact]
    public void Eat_NeverGoesBelowZero()
    {
        var trunk = new Trunk([new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);
        var hunger = new Hunger(
            HungerRules.Default, new HungerState(1, ThresholdTriggered: false));

        Assert.True(TrunkEat.Eat(
            trunk, hunger, new StoreCatalog([Maple], 20), "MAPLE SYRUP"));
        Assert.Equal(0, hunger.Value);
    }

    [Fact]
    public void Eat_StackNotCarried_ChangesNothing()
    {
        var trunk = new Trunk([new TrunkItem("BANANA", 0.3) { Quantity = 1 }]);
        var hunger = new Hunger(
            HungerRules.Default, new HungerState(10, ThresholdTriggered: false));

        bool eaten = TrunkEat.Eat(
            trunk, hunger, new StoreCatalog([Maple], 20), "MAPLE SYRUP");

        Assert.False(eaten);
        Assert.Equal(1, trunk.Items.Single().Quantity);
        Assert.Equal(10, hunger.Value);
    }

    [Fact]
    public void Eat_ItemMissingFromTheCatalogue_ChangesNothing()
    {
        var trunk = new Trunk([new TrunkItem("OLD SPECIAL", 0.5) { Quantity = 1 }]);
        var hunger = new Hunger(
            HungerRules.Default, new HungerState(10, ThresholdTriggered: false));

        bool eaten = TrunkEat.Eat(
            trunk, hunger, new StoreCatalog([Maple], 20), "OLD SPECIAL");

        Assert.False(eaten);
        Assert.Equal(1, trunk.Items.Single().Quantity);
        Assert.Equal(10, hunger.Value);
    }

    [Fact]
    public void RemoveOne_MatchesTheNameCaseInsensitively()
    {
        var trunk = new Trunk([new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 2 }]);

        Assert.True(trunk.RemoveOne("maple syrup"));
        Assert.Equal(1, trunk.Items.Single().Quantity);
        Assert.False(trunk.RemoveOne("BANANA"));
        Assert.False(trunk.RemoveOne(" "));
    }
}
