using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// The missing-person fee (ToWhereIsWilly.md §2.5): both multipliers at every
/// band edge, the 100 CR floor, Unity's banker's rounding, the average car
/// value, the shipped defaults, and the data file's validation and fallback.
/// </summary>
public sealed class WhereIsWillyRulesTests
{
    private static readonly WhereIsWillyRules Rules = WhereIsWillyRules.Default;

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.0)]
    [InlineData(2, 1.0)]
    [InlineData(3, 1.25)]
    [InlineData(5, 1.25)]
    [InlineData(6, 1.5)]
    [InlineData(9, 1.5)]
    [InlineData(10, 1.75)]
    [InlineData(14, 1.75)]
    [InlineData(15, 2.0)]
    [InlineData(40, 2.0)]
    public void ShopMultiplier_CoversEveryBandEdge(int shops, double expected) =>
        Assert.Equal(expected, Rules.ShopMultiplier(shops));

    [Theory]
    // Three for a first-try find, one less per failure, floored at 0.5.
    [InlineData(10, 0, 3.0)]
    [InlineData(10, 1, 2.0)]
    [InlineData(10, 2, 1.0)]
    [InlineData(10, 3, 0.5)]
    [InlineData(10, 10, 0.5)]
    // Never above the town's shop count: a one-shop town always multiplies 1.
    [InlineData(1, 0, 1.0)]
    [InlineData(2, 0, 2.0)]
    public void AttemptMultiplier_IsCappedByTheShopCount(
        int shops, int attempts, double expected) =>
        Assert.Equal(expected, Rules.AttemptMultiplier(shops, attempts));

    [Fact]
    public void Reward_AppliesTheUnityFormula()
    {
        // 30,000 CR average -> 1,200 base; a ten-shop town pays x1.75 while
        // the first-try attempt pays x3.0 -> 6,300 CR.
        Assert.Equal(6300, Rules.Reward(30000.0, 10, 0));
        // Two attempts on the same town: x1.0 instead of x3.0.
        Assert.Equal(2100, Rules.Reward(30000.0, 10, 2));
    }

    [Fact]
    public void Reward_NeverFallsBelowTheHundredCreditFloor() =>
        Assert.Equal(100, Rules.Reward(0.0, 1, 0));

    [Fact]
    public void Reward_RoundsHalfToEven()
    {
        // Unity's Mathf.RoundToInt is Math.Round (banker's rounding), not
        // away from zero: 2.5 lands on 2 and 7.5 on 8. The floor is disabled
        // so the rounding itself is what the assertion reads.
        WhereIsWillyRules rules = Rules with { MinReward = 0.0 };

        // base 1.0 * attemptMult 2 * shopMult 1.25 = 2.5
        Assert.Equal(2, rules.Reward(25.0, 3, 1));
        // base 3.0 * attemptMult 2 * shopMult 1.25 = 7.5
        Assert.Equal(8, rules.Reward(75.0, 3, 1));
    }

    [Fact]
    public void AverageCarValue_SkipsPricelessCarsAndAnEmptyGarage()
    {
        CarSpec[] cars =
        [
            Car(10000.0),
            Car(0.0),
            Car(30000.0),
        ];

        Assert.Equal(20000.0, WhereIsWillyRules.AverageCarValue(cars), 6);
        Assert.Equal(0.0, WhereIsWillyRules.AverageCarValue([]), 6);
        Assert.Equal(0.0, WhereIsWillyRules.AverageCarValue([Car(0.0)]), 6);
        Assert.Equal(0.0, WhereIsWillyRules.AverageCarValue([null]), 6);
    }

    [Fact]
    public void Default_MatchesUnitysNumbers()
    {
        Assert.Equal(0.04, Rules.FinderFeeRate, 6);
        Assert.Equal(100.0, Rules.MinReward, 6);
        Assert.Equal(3.0, Rules.AttemptBase, 6);
        Assert.Equal(0.5, Rules.AttemptFloor, 6);
        Assert.Equal(5, Rules.ShopBands.Count);
        Assert.Null(Rules.ShopBands[^1].MaxShops);
        Assert.True(Rules.IsValid);
    }

    [Fact]
    public void IsValid_RejectsBrokenTables()
    {
        Assert.False((Rules with { FinderFeeRate = 0.0 }).IsValid);
        Assert.False((Rules with { MinReward = -1.0 }).IsValid);
        Assert.False((Rules with { AttemptFloor = 0.0 }).IsValid);
        Assert.False((Rules with { ShopBands = [] }).IsValid);
        // A closed last band would leave a large town unpriced.
        Assert.False((Rules with
        {
            ShopBands = [new WhereIsWillyShopBand(2, 1.0), new WhereIsWillyShopBand(5, 1.25)],
        }).IsValid);
        // An open-ended band before the last one is ambiguous.
        Assert.False((Rules with
        {
            ShopBands = [new WhereIsWillyShopBand(null, 1.0), new WhereIsWillyShopBand(5, 1.25)],
        }).IsValid);
        // Non-ascending ceilings would silently skip a band.
        Assert.False((Rules with
        {
            ShopBands =
            [
                new WhereIsWillyShopBand(5, 1.0),
                new WhereIsWillyShopBand(3, 1.25),
                new WhereIsWillyShopBand(null, 1.5),
            ],
        }).IsValid);
        Assert.False((Rules with
        {
            ShopBands = [new WhereIsWillyShopBand(null, 0.0)],
        }).IsValid);
    }

    [Fact]
    public void IsWindow_IsWednesdayEightToEight()
    {
        int wednesday = 4;
        Assert.True(WhereIsWillyRules.IsWindow(wednesday, 8));
        Assert.True(WhereIsWillyRules.IsWindow(wednesday, 19));
        Assert.False(WhereIsWillyRules.IsWindow(wednesday, 7));
        Assert.False(WhereIsWillyRules.IsWindow(wednesday, 20));
        Assert.False(WhereIsWillyRules.IsWindow(2, 12));
        Assert.Equal(wednesday, WhereIsWillyRules.DayOfWeek);
    }

    [Fact]
    public void SameDoor_IgnoresCaseAndTheSide()
    {
        var home = new AddressLocation("Street 1", 12, HouseSide.Left);
        Assert.True(WhereIsWillyRules.SameDoor(
            new AddressLocation("street 1 ", 12, HouseSide.Right), home));
        Assert.False(WhereIsWillyRules.SameDoor(
            new AddressLocation("Street 2", 12, HouseSide.Left), home));
        Assert.False(WhereIsWillyRules.SameDoor(
            new AddressLocation("Street 1", 13, HouseSide.Left), home));
    }

    [Fact]
    public void HasCoffeeCareer_AcceptsAshopDoorOrAnOwnedCoffeeHub()
    {
        var career = new DeliveryCareer(HubCatalog.Default, 0);

        Assert.False(WhereIsWillyRules.HasCoffeeCareer(new PoiDirectory(), career));
        Assert.True(WhereIsWillyRules.HasCoffeeCareer(
            new PoiDirectory([Shop("coffee_shop", 1)]), career));
        Assert.True(WhereIsWillyRules.HasCoffeeCareer(
            new PoiDirectory([Shop("groceries_store", 1)]), CoffeeCareer()));
    }

    [Fact]
    public void JsonRules_MissingFileFallsBackWithWarning()
    {
        var log = new TestLog();
        var store = new JsonWhereIsWillyRules(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "willy_rules.json"),
            log);

        Assert.Equal(WhereIsWillyRules.Default, store.Load());
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void JsonRules_InvalidContentFallsBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();
            Assert.Equal(WhereIsWillyRules.Default, new JsonWhereIsWillyRules(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void JsonRules_NonAscendingBandsFallBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                {"FinderFeeRate":0.04,"MinReward":100,"AttemptBase":3,
                 "AttemptFloor":0.5,
                 "ShopBands":[{"MaxShops":5,"Multiplier":1.0},
                              {"MaxShops":2,"Multiplier":1.25},
                              {"MaxShops":null,"Multiplier":1.5}]}
                """);
            var log = new TestLog();
            Assert.Equal(WhereIsWillyRules.Default, new JsonWhereIsWillyRules(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void JsonRules_ValidFileLoadsItsNumbers()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                {"FinderFeeRate":0.05,"MinReward":250,"AttemptBase":4,
                 "AttemptFloor":1.0,
                 "ShopBands":[{"MaxShops":3,"Multiplier":1.1},
                              {"MaxShops":null,"Multiplier":2.5}]}
                """);
            var log = new TestLog();

            WhereIsWillyRules rules = new JsonWhereIsWillyRules(file, log).Load();

            Assert.Equal(0.05, rules.FinderFeeRate, 6);
            Assert.Equal(250.0, rules.MinReward, 6);
            Assert.Equal(4.0, rules.AttemptBase, 6);
            Assert.Equal(2, rules.ShopBands.Count);
            Assert.Null(rules.ShopBands[^1].MaxShops);
            Assert.Equal(2.5, rules.ShopMultiplier(4), 6);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static CarSpec Car(double price) =>
        new("ks_willy", 2020, "Test", "One", Price: price);

    private static PointOfInterest Shop(string type, int number) =>
        new(type + "_" + number, "Shop " + number,
            new AddressLocation("Street 1", number, HouseNumberPlan.SideOf(number)),
            type);

    private static DeliveryCareer CoffeeCareer()
    {
        var career = new DeliveryCareer(
            new HubCatalog(
                [new HubTier(1, true,
                    [new HubCareer("coffee_beans", "COFFEE BEANS", [new HubLevel("CoffeeUs", 100)])])],
                Ramp: null,
                OwnerNames: []),
            1000.0);
        Assert.True(career.TryOpenCareer(1, 0, 0));
        return career;
    }
}
