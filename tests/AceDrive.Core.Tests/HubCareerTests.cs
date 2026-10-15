using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Delivery career rules (PORTPLAN 18): spending CredP on hubs,
/// owning categories and the store-connection eligibility, headless and
/// deterministic (port of Unity HubCareerCatalog/DeliveryHubScreen rules).</summary>
public sealed class HubCareerTests
{
    private static readonly HubCareer Groceries = new(
        "groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100), new HubLevel("The Food Hub", 150)]);
    private static readonly HubCareer Coffee = new(
        "coffee_beans", "COFFEE BEANS", [new HubLevel("CoffeeUs", 100)]);
    private static readonly HubCareer Locked = new(
        "medicine", "MEDICINE", [new HubLevel("HealthHub Pharma", 180)]);

    private static readonly HubCatalog Hubs = new(
    [
        new HubTier(1, true, [Groceries, Coffee]),
        new HubTier(4, false, [Locked]),
    ]);

    private static DeliveryCareer Fresh(double credP = 100) => new(Hubs, credP);

    private static readonly DeliveryStore Post =
        new("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries");
    private static readonly DeliveryStore Kiosk =
        new("nurburg-kiosk", "Nürburg Kiosk", "Fuchs", 6, 40, 2.20, "coffee_beans");
    private static readonly DeliveryStore Open =
        new("open-shop", "Open Shop", "Weber", 15, 60, 3.10);

    private static readonly DeliveryCatalog Board = new([Post, Kiosk, Open], 0.7, 100);

    [Fact]
    public void Seeds_WithMaxsGift()
    {
        var career = Fresh();
        Assert.Equal(100, career.CredP);
        Assert.Empty(career.Owned);
    }

    [Fact]
    public void TryOpenCareer_SpendsCredPAndRecordsHub()
    {
        var career = Fresh(250);
        Assert.True(career.TryOpenCareer(tierId: 1, careerIndex: 0, levelIndex: 1));

        Assert.Equal(100, career.CredP);
        OwnedHub hub = Assert.Single(career.Owned);
        Assert.Equal("The Food Hub", hub.Name);
        Assert.Equal("groceries", hub.Category);
        Assert.Equal(150, hub.Cost);
        // Unity numbers the owned level from 1: the CredP ramp indexes by it.
        Assert.Equal(1, hub.Tier);
        Assert.Equal(2, hub.Level);
    }

    [Fact]
    public void TryOpenCareer_RefusesWhenNotAffordable_Owned_OrBogus()
    {
        var career = Fresh(100);
        Assert.True(career.TryOpenCareer(1, 0, 0));

        Assert.False(career.TryOpenCareer(1, 0, 0)); // same hub again, by name
        Assert.False(career.TryOpenCareer(1, 0, 1)); // 150 > remaining 0
        Assert.False(career.TryOpenCareer(1, 1, 5)); // level outside the column
        Assert.False(career.TryOpenCareer(1, 9, 0)); // career outside the tier
        Assert.False(career.TryOpenCareer(9, 0, 0)); // tier not in the catalog
        Assert.False(career.TryOpenCareer(4, 0, 0)); // disabled tier
        Assert.Equal(0, career.CredP);
        Assert.Single(career.Owned);
    }

    [Fact]
    public void CanBuy_MirrorsTheOpenCareerRules()
    {
        var career = Fresh(100);
        Assert.True(career.CanBuy(1, 0, 0));
        Assert.False(career.CanBuy(1, 0, 1));  // 150 > 100
        Assert.False(career.CanBuy(1, 0, 9));  // nothing there
        Assert.False(career.CanBuy(4, 0, 0));  // disabled tier

        Assert.True(career.TryOpenCareer(1, 0, 0));
        Assert.False(career.CanBuy(1, 0, 0));  // owned now
    }

    [Fact]
    public void OwningOneCareer_ConnectsOnlyItsOwnStores()
    {
        var career = Fresh();
        Assert.True(career.TryOpenCareer(1, 0, 0));

        Assert.True(career.OwnsCategory("groceries"));
        Assert.False(career.OwnsCategory("coffee_beans"));
        Assert.True(career.Connects(Post));
        Assert.False(career.Connects(Kiosk));
        // Unity's guided early content: no category, never career-gated.
        Assert.True(career.Connects(Open));

        Assert.Equal(
            ["adenau-post", "open-shop"],
            career.VisibleStores(Board).Select(s => s.Key).ToArray());
    }

    [Fact]
    public void HeaderStats_SumVisibleStoresOnly()
    {
        // Unity StoreManager totals count live (backfilled) stores only: an
        // unopened category is not on the map, so its town cell counts for
        // nothing in the header. Citizens start at the rules' 100 per store,
        // and the band follows the store's own shelf fill.
        var career = Fresh();
        var city = new CityEconomy(Board.Stores, CityRules.Default);
        Assert.Equal(100, city.TotalCitizens(career.VisibleStores(Board)));
        Assert.Equal(-2, city.TotalGrowthPerHour(career.VisibleStores(Board)));

        Assert.True(career.TryOpenCareer(1, 1, 0));
        Assert.Equal(200, city.TotalCitizens(career.VisibleStores(Board)));
        Assert.Equal(-4, city.TotalGrowthPerHour(career.VisibleStores(Board)));
    }

    [Fact]
    public void DefaultCatalog_NothingCanBeOpened()
    {
        var career = new DeliveryCareer(HubCatalog.Default, 100);
        Assert.False(career.TryOpenCareer(1, 0, 0));
        Assert.False(career.CanBuy(1, 0, 0));
        Assert.Equal(100, career.CredP);
    }
}
