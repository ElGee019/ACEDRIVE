using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The Touristenfahrten draws: 1-3 distinct corners per run, the job order
/// inside its item ranges and never under the per-dropoff floor, the uneven
/// split that keeps every unit and fills each stop, and the shipped prices.
/// </summary>
public sealed class TouristenRulesTests
{
    [Fact]
    public void DrawDropoffs_OneToThreeDistinctCorners()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            IReadOnlyList<TouristenDropoffDefinition> drawn =
                TouristenRules.DrawDropoffs(TouristenMenu.Default, new Random(seed));

            Assert.InRange(drawn.Count, 1, 3);
            Assert.Equal(drawn.Count, drawn.Select(place => place.Id).Distinct().Count());
            Assert.All(
                drawn,
                place => Assert.Contains(
                    TouristenMenu.Default.Dropoffs, known => known.Id == place.Id));
        }
    }

    [Fact]
    public void DrawOrder_CoffeeHoldsAtLeastTwoItemsWithinTheRanges()
    {
        TouristenPickup coffee = Pickup("coffee");
        for (int seed = 0; seed < 100; seed++)
        {
            TouristenOrder order = TouristenRules.DrawOrder(
                coffee, TouristenMenu.Default.MinItemsPerOrder, new Random(seed));

            Assert.True(order.ItemCount >= 2);
            Assert.Equal(coffee.Items.Count, order.Lines.Count);
            for (int i = 0; i < order.Lines.Count; i++)
            {
                Assert.InRange(
                    order.Lines[i].Quantity, coffee.Items[i].Min, coffee.Items[i].Max);
            }
        }
    }

    [Fact]
    public void DrawOrder_PizzaKeepsItsGuaranteedSlice()
    {
        TouristenPickup pizza = Pickup("pizza");
        for (int seed = 0; seed < 100; seed++)
        {
            TouristenOrder order = TouristenRules.DrawOrder(pizza, 2, new Random(seed));

            Assert.True(order.ItemCount >= 2);
            Assert.True(order.Lines[0].Quantity >= 1);
        }
    }

    [Fact]
    public void Order_TotalIsTheSumOfThePricedLines()
    {
        var order = new TouristenOrder(
        [
            new TouristenOrderLine("coffee", "TouristenItemCoffee", 2, 3.5),
            new TouristenOrderLine("beer", "TouristenItemBeer", 1, 5.5),
            new TouristenOrderLine("soup", "TouristenItemSoup", 0, 8.5),
        ]);

        Assert.Equal(12.5, order.Total, 2);
        Assert.Equal(3, order.ItemCount);
    }

    [Fact]
    public void SplitOrder_DealsOneJobOrderOutAndKeepsEveryUnit()
    {
        var order = new TouristenOrder(
        [
            new TouristenOrderLine("coffee", "TouristenItemCoffee", 4, 3.5),
            new TouristenOrderLine("beer", "TouristenItemBeer", 3, 5.5),
            new TouristenOrderLine("soup", "TouristenItemSoup", 2, 8.5),
        ]);

        for (int seed = 0; seed < 20; seed++)
        {
            IReadOnlyList<TouristenOrder> shares =
                TouristenRules.SplitOrder(order, 3, new Random(seed));

            Assert.Equal(3, shares.Count);
            Assert.All(shares, share => Assert.True(share.ItemCount >= 1));
            Assert.Equal(order.ItemCount, shares.Sum(share => share.ItemCount));
            Assert.Equal(order.Total, shares.Sum(share => share.Total), 2);
        }

        // A line keeps its own identity and unit price in the share it lands in.
        IReadOnlyList<TouristenOrder> split = TouristenRules.SplitOrder(order, 3, new Random(1));
        TouristenOrderLine line = Assert.Single(
            split[0].Lines, share => share.Key == "coffee");
        Assert.Equal("TouristenItemCoffee", line.NameKey);
        Assert.Equal(3.5, line.UnitPrice);
    }

    [Fact]
    public void SplitOrder_DoesNotSplitTheLoadEvenly()
    {
        var order = new TouristenOrder(
            [new TouristenOrderLine("coffee", "TouristenItemCoffee", 12, 3.5)]);

        // One seeded deal that lands equal shares is possible; the run must not
        // produce it on every draw, which is what an even split would.
        bool uneven = false;
        for (int seed = 0; seed < 20 && !uneven; seed++)
        {
            IReadOnlyList<TouristenOrder> shares =
                TouristenRules.SplitOrder(order, 3, new Random(seed));
            uneven = shares[0].ItemCount != shares[1].ItemCount
                || shares[1].ItemCount != shares[2].ItemCount;
        }

        Assert.True(uneven);
    }

    [Fact]
    public void SplitOrder_LeavesTheLastSharesEmptyWhenTheLoadIsTooSmall()
    {
        var order = new TouristenOrder(
            [new TouristenOrderLine("coffee", "TouristenItemCoffee", 2, 3.5)]);

        IReadOnlyList<TouristenOrder> shares =
            TouristenRules.SplitOrder(order, 3, new Random(4));

        Assert.Equal(1, shares[0].ItemCount);
        Assert.Equal(1, shares[1].ItemCount);
        Assert.Equal(0, shares[2].ItemCount);
    }

    [Fact]
    public void SplitOrder_RejectsAZeroDropoffRun()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TouristenRules.SplitOrder(
            new TouristenOrder([]), 0, new Random(1)));
    }

    [Fact]
    public void SplitOrder_OfTheDrawnJobOrder_FillsEveryDropoffShare()
    {
        TouristenPickup coffee = Pickup("coffee");
        for (int seed = 0; seed < 50; seed++)
        {
            TouristenOrder order = TouristenRules.DrawOrder(coffee, 6, new Random(seed));

            IReadOnlyList<TouristenOrder> shares =
                TouristenRules.SplitOrder(order, 3, new Random(seed));

            Assert.Equal(3, shares.Count);
            Assert.All(shares, share => Assert.True(share.ItemCount >= 1));
            Assert.Equal(order.ItemCount, shares.Sum(share => share.ItemCount));
        }
    }

    [Fact]
    public void Default_CarriesTheSpecifiedUnitPrices()
    {
        Dictionary<string, double> prices = new(StringComparer.Ordinal);
        foreach (TouristenPickup pickup in TouristenMenu.Default.Pickups)
        {
            foreach (TouristenItemRule item in pickup.Items)
            {
                prices.TryAdd(item.Key, item.Price);
            }
        }

        Assert.Equal(3.5, prices["coffee"]);
        Assert.Equal(3.0, prices["espresso"]);
        Assert.Equal(5.5, prices["beer"]);
        Assert.Equal(5.0, prices["pizza_slice"]);
        Assert.Equal(7.0, prices["bratwurst_kraut"]);
        Assert.Equal(9.5, prices["currywurst_pommes"]);
        Assert.Equal(19.5, prices["schnitzel"]);
        Assert.Equal(8.5, prices["soup"]);
        // The pizzeria's tiramisu follows the store catalogue's own price.
        Assert.Equal(7.0, prices["tiramisu"]);
    }

    [Fact]
    public void Default_MapsBothPickupShopTypes()
    {
        Assert.True(TouristenMenu.Default.IsUsable);
        Assert.NotNull(TouristenMenu.Default.PickupFor(PoiTypes.CoffeeShop));
        Assert.NotNull(TouristenMenu.Default.PickupFor(PoiTypes.PizzaPlace));
        Assert.Null(TouristenMenu.Default.PickupFor(PoiTypes.Grocery));
        Assert.Null(TouristenMenu.Default.PickupFor(null));
    }

    [Fact]
    public void IsUsable_RejectsAnOrderFloorBelowTwoItems()
    {
        Assert.False((TouristenMenu.Default with { MinItemsPerOrder = 1 }).IsUsable);
        Assert.False((TouristenMenu.Default with { Dropoffs = [] }).IsUsable);
    }

    private static TouristenPickup Pickup(string kind) =>
        Assert.Single(TouristenMenu.Default.Pickups, pickup => pickup.Kind == kind);
}
