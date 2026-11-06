using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// Grandma's pancakes (PORTPLAN items 26/52): the catalogue row the intro and
/// the weekly visit hand over, the plate that comes off the meter, and the stack
/// the appetite's one-shot event asks the trunk for. The hunger value is always
/// the catalogue's, never a copy taken when the stack was packed.
/// </summary>
public sealed class PancakeVisitTests
{
    private static StoreCatalog Catalog(params StoreItem[] items) =>
        new(items.Length == 0
            ? [new StoreItem(PancakeVisit.ItemName, 0.0, 0.5, 14, "pancakes")]
            : items, 20);

    private static PancakeMeal Serve(Trunk trunk, Hunger hunger, StoreCatalog catalog) =>
        PancakeVisit.Serve(trunk, hunger, catalog)
            ?? throw new InvalidOperationException("the catalogue lost the pancake row");

    [Fact]
    public void IsCarried_FollowsTheTrunkStack()
    {
        var empty = new Trunk();
        var carrying = new Trunk([new TrunkItem(PancakeVisit.ItemName, 0.5) { Quantity = 1 }]);

        Assert.False(PancakeVisit.IsCarried(empty));
        Assert.True(PancakeVisit.IsCarried(carrying));
    }

    [Fact]
    public void Serve_TakesThePlateOffTheMeterAndPacksAStack()
    {
        var trunk = new Trunk();
        var hunger = new Hunger(
            HungerRules.Default, new HungerState(20, ThresholdTriggered: false));

        PancakeMeal meal = Serve(trunk, hunger, Catalog());

        Assert.Equal(20, meal.Before);
        Assert.Equal(6, meal.After);
        Assert.Equal(14, meal.Served);
        Assert.Equal(6, hunger.Value);
        // The road stack rides the same catalogue row: its volume is the data's,
        // so the trunk's free space stays honest, and the one-shot finds it by
        // name next time.
        Assert.True(PancakeVisit.IsCarried(trunk));
        Assert.Equal(1, trunk.Items.Single().Quantity);
        Assert.Equal(0.5, trunk.Items.Single().UnitVolume);
        Assert.Equal(0.5, trunk.UsedLitres);
    }

    [Fact]
    public void Serve_ClampsAtZeroLikeEveryBite()
    {
        var trunk = new Trunk();
        var hunger = new Hunger(
            HungerRules.Default, new HungerState(5, ThresholdTriggered: false));

        PancakeMeal meal = Serve(trunk, hunger, Catalog());

        Assert.Equal(0, meal.After);
        Assert.Equal(5, meal.Served);
        Assert.Equal(0, hunger.Value);
    }

    [Fact]
    public void Serve_PacksOntoAnExistingStack()
    {
        var trunk = new Trunk([new TrunkItem(PancakeVisit.ItemName, 0.5) { Quantity = 2 }]);
        var hunger = new Hunger(
            HungerRules.Default, new HungerState(14, ThresholdTriggered: false));

        _ = PancakeVisit.Serve(trunk, hunger, Catalog());

        Assert.Equal(3, trunk.Items.Single().Quantity);
    }

    [Fact]
    public void Serve_WithoutTheCatalogueRow_ChangesNothing()
    {
        // A hand-edited catalogue must not buy an appetite drop for free: no
        // plate, and no stack that could never be eaten either.
        var trunk = new Trunk();
        var hunger = new Hunger(
            HungerRules.Default, new HungerState(20, ThresholdTriggered: false));
        var lost = new StoreCatalog(
            [new StoreItem("BANANA", 2.0, 0.3, 4, "groceries")], 20);

        Assert.Null(PancakeVisit.Serve(trunk, hunger, lost));
        Assert.Equal(20, hunger.Value);
        Assert.Empty(trunk.Items);
    }

    [Fact]
    public void Serve_RowThatFeedsNothing_IsNotFood()
    {
        // Hunger 0 is the catalogue's "not food" signal (StoreItem.IsFood): the
        // visit must not hand over a keepsake it can never eat.
        var trunk = new Trunk();
        var hunger = new Hunger(HungerRules.Default);
        var keepsake = new StoreCatalog(
            [new StoreItem(PancakeVisit.ItemName, 0.0, 0.5, 0, "pancakes")], 20);

        Assert.Null(PancakeVisit.Serve(trunk, hunger, keepsake));
        Assert.Empty(trunk.Items);
    }
}
