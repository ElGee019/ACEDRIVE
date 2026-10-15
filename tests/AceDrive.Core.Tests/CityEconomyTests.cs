using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>City economy rules (PORTPLAN 25, port of Unity
/// StoreManager.OnMinuteTick): stock bands drive citizens up or down on the
/// citizen tick, demand empties the shelves only once a career exists, and
/// the snapshot is what a restart reopens. Deterministic by construction —
/// time arrives as game moments, never as counted polls.</summary>
public sealed class CityEconomyTests
{
    private static readonly DateTime Start =
        new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    // 40-unit shelf at any fill the tests want.
    private static DeliveryStore Shelf(int stocked, int capacity = 40) =>
        new("store-a", "Store A", "Hahn", stocked, capacity, 2.00);

    // CitizenTickMinutes far out: isolates the demand tick from growth.
    private static readonly CityRules GrowthFrozen =
        CityRules.Default with { CitizenTickMinutes = 100_000 };

    private static CityEconomy Town(DeliveryStore store, CityRules? rules = null) =>
        new([store], rules ?? CityRules.Default);

    private static int Citizens(CityEconomy city, DeliveryStore store) =>
        city.TotalCitizens([store]);

    private static int Stock(CityEconomy city, DeliveryStore store) =>
        city.Live(store).StockedUnits;

    // Game minutes to moments at the clock's pace (2.2 real s per game
    // minute) is MainViewModel's job; the town only ever sees moments.
    private static DateTime Later(int gameMinutes) => Start.AddMinutes(gameMinutes);

    [Fact]
    public void FreshTown_StartsAtTheRulesCitizens()
    {
        DeliveryStore store = Shelf(12);
        var city = Town(store);

        Assert.Equal(100, Citizens(city, store));
        Assert.Equal(12, Stock(city, store));
    }

    [Fact]
    public void AdvanceTo_PrimesOnTheFirstMomentWithoutPayingOut()
    {
        // The town starts living at the resumed game moment: a session never
        // re-pays the hours an earlier one already banked.
        DeliveryStore store = Shelf(40);
        var city = Town(store);

        Assert.False(city.AdvanceTo(Later(600), hasDeliveryCareer: true));
        Assert.Equal(100, Citizens(city, store));
        Assert.Equal(40, Stock(city, store));
    }

    [Fact]
    public void FullShelves_GrowOneCitizenPerTick()
    {
        DeliveryStore store = Shelf(40);
        var city = Town(store);
        city.AdvanceTo(Start, hasDeliveryCareer: false);

        Assert.True(city.AdvanceTo(Later(30), hasDeliveryCareer: false));
        Assert.Equal(101, Citizens(city, store));

        city.AdvanceTo(Later(60), hasDeliveryCareer: false);
        Assert.Equal(102, Citizens(city, store));
    }

    [Fact]
    public void PartialMinutes_NeverTick()
    {
        DeliveryStore store = Shelf(40);
        var city = Town(store);
        city.AdvanceTo(Start, hasDeliveryCareer: false);

        Assert.False(city.AdvanceTo(Later(29), hasDeliveryCareer: false));
        Assert.Equal(100, Citizens(city, store));

        Assert.True(city.AdvanceTo(Later(30), hasDeliveryCareer: false));
        Assert.Equal(101, Citizens(city, store));
    }

    [Fact]
    public void HalfCitizens_BankBeforeTheyShow()
    {
        // 60 % fill is the medium band: +0.5 per 30 minutes, so the citizen
        // only appears on the second tick (Unity's growthAccumulator).
        DeliveryStore store = Shelf(24);
        var city = Town(store);
        city.AdvanceTo(Start, hasDeliveryCareer: false);

        city.AdvanceTo(Later(30), hasDeliveryCareer: false);
        Assert.Equal(100, Citizens(city, store));
        Assert.Equal(0.5, city.Snapshot()[0].GrowthAccumulator);

        city.AdvanceTo(Later(60), hasDeliveryCareer: false);
        Assert.Equal(101, Citizens(city, store));
        Assert.Equal(0, city.Snapshot()[0].GrowthAccumulator);
    }

    [Fact]
    public void Decline_StopsAtTheFloor()
    {
        DeliveryStore store = Shelf(0);
        var city = Town(store);
        city.AdvanceTo(Start, hasDeliveryCareer: false);

        // 24 game hours of empty shelves: -1 per 30 minutes, so the town
        // would be at 52 without the minCitizens floor.
        city.AdvanceTo(Later(24 * 60), hasDeliveryCareer: false);
        Assert.Equal(CityRules.Default.MinCitizens, Citizens(city, store));

        city.AdvanceTo(Later(48 * 60), hasDeliveryCareer: false);
        Assert.Equal(CityRules.Default.MinCitizens, Citizens(city, store));
    }

    [Theory]
    [InlineData(40, 2)]   // 100 % -> high band
    [InlineData(24, 1)]   //  60 % -> medium
    [InlineData(22, 0)]   //  55 % -> neutral
    [InlineData(12, -1)]  //  30 % -> low
    [InlineData(6, -2)]   //  15 % -> very low / empty
    public void GrowthPerHour_FollowsTheStockBands(int stocked, int expected)
    {
        // Unity's display mapping, derived from the same bands the tick pays.
        DeliveryStore store = Shelf(stocked);
        var city = Town(store);

        Assert.Equal(expected, city.GrowthPerHour(store));
        Assert.Equal(expected, city.TotalGrowthPerHour([store]));
    }

    [Fact]
    public void Restock_FillsTheGivenShelvesAndSaysNothingTwice()
    {
        // The drop-off is the only way a shelf jumps up, and it jumps to the
        // store's own capacity. The moved flag answers false for a shelf that
        // was already full and for a store the board never held — that is what
        // keeps transition-only logging honest.
        DeliveryStore store = Shelf(12);
        var city = Town(store);

        Assert.True(city.Restock([store.Key]));
        Assert.Equal(40, Stock(city, store));
        Assert.False(city.Restock([store.Key]));

        DeliveryStore stranger = Shelf(8) with { Key = "gone" };
        Assert.Equal(8, city.Live(stranger).StockedUnits);
    }

    [Fact]
    public void Restock_LeavesTheUnorderedShelves()
    {
        // A cart holds what the player ordered, so a delivery never refills a
        // store nobody bought anything for.
        DeliveryStore ordered = Shelf(12);
        DeliveryStore waiting = Shelf(8) with { Key = "store-b" };
        var city = new CityEconomy([ordered, waiting], CityRules.Default);

        Assert.True(city.Restock([ordered.Key]));
        Assert.Equal(40, Stock(city, ordered));
        Assert.Equal(8, Stock(city, waiting));
    }

    [Fact]
    public void Restock_TheTownFollowsTheNewBand()
    {
        // No second call wires the growth: the tick reads the fill, so the
        // shelf that declined before the drop-off grows after it — and the
        // citizens already lost stay lost until the town grows them back.
        DeliveryStore store = Shelf(0);
        var city = Town(store);
        city.AdvanceTo(Start, hasDeliveryCareer: false);

        city.AdvanceTo(Later(30), hasDeliveryCareer: false);
        Assert.Equal(99, Citizens(city, store));

        city.Restock([store.Key]);
        Assert.Equal(2, city.GrowthPerHour(store));

        city.AdvanceTo(Later(60), hasDeliveryCareer: false);
        Assert.Equal(100, Citizens(city, store));
    }

    [Fact]
    public void Demand_WaitsForTheDeliveryCareer()
    {
        DeliveryStore store = Shelf(12);
        var city = Town(store, GrowthFrozen);
        city.AdvanceTo(Start, hasDeliveryCareer: false);

        // Three demand intervals with no hub owned: nothing is pulled.
        city.AdvanceTo(Later(270), hasDeliveryCareer: false);
        Assert.Equal(12, Stock(city, store));

        // And the missed intervals never dump: from the purchase onward one
        // interval is due, so exactly one unit leaves the shelf, not four.
        city.AdvanceTo(Later(360), hasDeliveryCareer: true);
        Assert.Equal(11, Stock(city, store));
    }

    [Theory]
    [InlineData(100, 1)]  // 0-199 citizens -> one unit
    [InlineData(250, 2)]  // 200-499 -> two
    [InlineData(550, 3)]  // 500-899 -> three
    [InlineData(950, 4)]  // 900-1399 -> four
    public void Demand_Ladder_RisesWithCitizens(int citizens, int expectedUnits)
    {
        DeliveryStore store = Shelf(40);
        var saved = new StoreState[] { new(store.Key, citizens, 40, 0) };
        var city = new CityEconomy([store], GrowthFrozen, saved);
        city.AdvanceTo(Start, hasDeliveryCareer: true);

        Assert.True(city.AdvanceTo(Later(90), hasDeliveryCareer: true));
        Assert.Equal(40 - expectedUnits, Stock(city, store));
    }

    [Fact]
    public void Demand_NeverDigsBelowAnEmptyShelf()
    {
        DeliveryStore store = Shelf(1);
        var city = new CityEconomy([store], GrowthFrozen, [new StoreState(store.Key, 950, 1, 0)]);
        city.AdvanceTo(Start, hasDeliveryCareer: true);

        city.AdvanceTo(Later(900), hasDeliveryCareer: true);
        Assert.Equal(0, Stock(city, store));
    }

    [Fact]
    public void Snapshot_ResumesTheSameTown()
    {
        DeliveryStore store = Shelf(12);
        var first = Town(store);
        first.AdvanceTo(Start, hasDeliveryCareer: false);
        first.AdvanceTo(Later(300), hasDeliveryCareer: false);
        IReadOnlyList<StoreState> saved = first.Snapshot();

        var restarted = new CityEconomy([store], CityRules.Default, saved);
        Assert.Equal(Citizens(first, store), Citizens(restarted, store));
        Assert.Equal(Stock(first, store), Stock(restarted, store));
    }

    [Fact]
    public void Snapshot_ClampsWhatContentNoLongerAllows()
    {
        // city.json edited by hand, or a capacity shrunk in a content update:
        // the town clamps instead of letting the percent math lie.
        DeliveryStore store = Shelf(12);
        var saved = new StoreState[] { new(store.Key, 10, 999, 4) };
        var city = new CityEconomy([store], CityRules.Default, saved);

        Assert.Equal(CityRules.Default.MinCitizens, Citizens(city, store));
        Assert.Equal(store.UnitCapacity, Stock(city, store));
    }

    [Fact]
    public void UnknownStore_ContributesNothing()
    {
        // A row the town never held (a store dropped from the catalog): the
        // numbers say nothing instead of throwing in the middle of a poll.
        var city = Town(Shelf(40));
        DeliveryStore stranger = Shelf(30) with { Key = "gone" };

        Assert.Equal(0, city.GrowthPerHour(stranger));
        Assert.Equal(0, city.TotalCitizens([stranger]));
        Assert.Equal(30, city.Live(stranger).StockedUnits);
    }
}
