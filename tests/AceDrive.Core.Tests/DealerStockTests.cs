using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The dealer's weekly floor (PORTPLAN item 29): Unity's
/// CarDealerSelectionService draw with the year band dropped (ACEDRIVE has no
/// year system), so the rules pinned here are week-determinism, the own-car
/// exclusion and the ordinal sort.</summary>
public sealed class DealerStockTests
{
    private static List<CarSpec> Pool(int count)
    {
        var pool = new List<CarSpec>(count);
        for (int i = 0; i < count; i++)
        {
            // Descending ids so the ordinal sort is observable, and ascending
            // prices so affordability never interferes with the draw.
            pool.Add(new CarSpec($"ks_car_{count - i:00}", 2000 + i, "Test", $"model {i}", Price: 1000.0 + i));
        }
        return pool;
    }

    private static readonly HashSet<string> None = new(StringComparer.Ordinal);

    [Fact]
    public void SameWeek_ShowsTheSameFloor()
    {
        var pool = Pool(60);
        var first = DealerStock.Build(pool, None, week: 7);
        var second = DealerStock.Build(pool, None, week: 7);
        Assert.Equal(first.Select(c => c.Key), second.Select(c => c.Key));
    }

    [Fact]
    public void DifferentWeek_ShowsADifferentFloor()
    {
        // Not a law of the algorithm, but with 60 sellable cars two different
        // weeks matching exactly would mean the seed stopped mattering.
        var pool = Pool(60);
        var week1 = DealerStock.Build(pool, None, week: 1);
        var week2 = DealerStock.Build(pool, None, week: 2);
        Assert.NotEqual(week1.Select(c => c.Key), week2.Select(c => c.Key));
        Assert.Equal(DealerRules.MaxShownCars, week1.Count);
        Assert.Equal(DealerRules.MaxShownCars, week2.Count);
    }

    [Fact]
    public void OwnedCars_AreExcludedBeforePicking()
    {
        var pool = Pool(30);
        var owned = new HashSet<string>(StringComparer.Ordinal) { pool[0].Key, pool[3].Key, "ks_not_in_pool" };
        var floor = DealerStock.Build(pool, owned, week: 4);
        Assert.DoesNotContain(pool[0].Key, floor.Select(c => c.Key));
        Assert.DoesNotContain(pool[3].Key, floor.Select(c => c.Key));
    }

    [Fact]
    public void OwnedCars_ShowFewerCarsInsteadOfRefilling()
    {
        // Unity's test of the same name: exclusivity happens first, so a
        // nearly-owned roster produces a shorter floor, never a padded one.
        var pool = Pool(20);
        var owned = new HashSet<string>(StringComparer.Ordinal);
        foreach (CarSpec car in pool.Take(15))
        {
            owned.Add(car.Key);
        }
        var floor = DealerStock.Build(pool, owned, week: 3);
        Assert.Equal(5, floor.Count);
    }

    [Fact]
    public void UnsellableRows_NeverAppear()
    {
        // Price <= 0 is "not for sale" (the game leaves a few cards unpriced):
        // the dealer skips the row instead of giving the car away.
        var pool = Pool(6);
        pool.Add(new CarSpec("ks_free_car", 2020, "Test", "free", Price: 0.0));
        Assert.DoesNotContain(
            "ks_free_car", DealerStock.Build(pool, None, week: 1).Select(c => c.Key));
    }

    [Fact]
    public void Floor_IsOrderedByKey()
    {
        var floor = DealerStock.Build(Pool(60), None, week: 11);
        string[] keys = [.. floor.Select(c => c.Key)];
        Assert.Equal(keys.OrderBy(k => k, StringComparer.Ordinal), keys);
    }

    [Fact]
    public void AllOwnedOrEmptyPool_ShowsNothing()
    {
        Assert.Empty(DealerStock.Build([], None, week: 1));
        var pool = Pool(4);
        var owned = new HashSet<string>(StringComparer.Ordinal);
        foreach (CarSpec car in pool)
        {
            owned.Add(car.Key);
        }
        Assert.Empty(DealerStock.Build(pool, owned, week: 1));
    }

    [Fact]
    public void VariantRows_AreSeparateFloorEntries()
    {
        // A showroom variant is its own row and its own key (PORTPLAN 62), so
        // the same folder can appear twice with different presets.
        var pool = new List<CarSpec>
        {
            new("ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Price: 12000.0),
            new("ks_mazda_mx5_na", 1990, "Mazda", "mx5 na eunos", Price: 18000.0)
            {
                Preset = "preset_mx5na_mech_2",
            },
        };
        var floor = DealerStock.Build(pool, None, week: 2);
        Assert.Equal(2, floor.Count);
        Assert.Equal("ks_mazda_mx5_na", floor[0].Key);
        Assert.Equal("ks_mazda_mx5_na#preset_mx5na_mech_2", floor[1].Key);
    }

    [Fact]
    public void WeekSeed_FollowsTheStrideAndGuardsZero()
    {
        Assert.Equal(DealerRules.WeekSeedStride * 5, DealerRules.SeedFor(5));
        // A hand-edited week 0 or a negative week still lands on the first
        // week's floor instead of a degenerate all-zero seed.
        Assert.Equal(DealerRules.SeedFor(1), DealerRules.SeedFor(0));
        Assert.Equal(DealerRules.SeedFor(1), DealerRules.SeedFor(-4));
        Assert.NotEqual(0, DealerRules.SeedFor(1));
    }
}
