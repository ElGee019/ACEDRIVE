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
    public void NeedsIntroCredP_OnlyWhileNothingIsBankedOrOpened()
    {
        var broke = Fresh(0);
        Assert.True(broke.NeedsIntroCredP);

        broke.AddCredP(100); // Max's first store visit
        Assert.False(broke.NeedsIntroCredP);

        // A hub bought earlier is progress even when the balance is back to 0.
        var opened = Fresh(100);
        Assert.True(opened.TryOpenCareer(1, 0, 0));
        Assert.Equal(0, opened.CredP);
        Assert.False(opened.NeedsIntroCredP);
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
        // The map identity is Unity's category_slug id; the pickup leg resolves
        // the hub's POI by exactly this key.
        Assert.Equal("groceries_the_food_hub", hub.Key);
        Assert.Same(hub, career.OwnedFor("groceries"));
        Assert.Null(career.OwnedFor("coffee_beans"));
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

    // Owner + CredP ramp (Unity ApplyStoreDeliveryEffects, PORTPLAN 18):
    // every delivered store pays its producing hub's ramp rate.
    [Fact]
    public void RegisterDelivery_EarnsTheRamp_PerHubLevel()
    {
        // Level 1 tier 1: min(start 1 + deliveries before, cap 10) — 1, 2, 3.
        var career = Fresh();
        Assert.True(career.TryOpenCareer(1, 0, 0));
        Assert.Equal(1, career.RegisterDelivery("groceries").CredPEarned);
        Assert.Equal(2, career.RegisterDelivery("groceries").CredPEarned);
        Assert.Equal(3, career.RegisterDelivery("groceries").CredPEarned);
        // ...still climbing toward the cap (start 1 + count)...
        for (int earned = 4; earned <= 10; earned++)
        {
            Assert.Equal(earned, career.RegisterDelivery("groceries").CredPEarned);
        }
        // ...then pinned at 10, table times tier.
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(10, career.RegisterDelivery("groceries").CredPEarned);
        }
        // 100 CredP fell to 0 on the purchase, then the ramp paid 105 back.
        Assert.Equal(105, career.CredP);
    }

    [Fact]
    public void RegisterDelivery_ScalesByHubLevel()
    {
        // Level 2 of the column (The Food Hub): start rate 2, cap 20.
        var career = Fresh(250);
        Assert.True(career.TryOpenCareer(1, 0, 1));
        Assert.Equal(2, career.RegisterDelivery("groceries").CredPEarned);

        // No hub of the category: nothing earned, no owner.
        var none = new DeliveryCareer(HubCatalog.Default, 100);
        Assert.Equal(0, none.RegisterDelivery("groceries").CredPEarned);
        Assert.Equal("", none.RegisterDelivery("groceries").OwnerName);
    }

    [Fact]
    public void OwnedFor_AnswersTheNewestHubOfTheCategory()
    {
        var career = Fresh(300);
        Assert.True(career.TryOpenCareer(1, 0, 0));
        Assert.True(career.TryOpenCareer(1, 0, 1));

        Assert.Equal("The Food Hub", career.OwnedFor("groceries")!.Name);
        Assert.Equal("groceries_the_food_hub", career.OwnedFor("groceries")!.Key);
    }

    [Fact]
    public void RegisterDelivery_NewestOwnedHubIsTheGoodsSource()
    {
        // Buying the next level is the player's upgrade, so the ramp (and
        // its count) follows the newest purchase of the category.
        var career = Fresh(250);
        career.TryOpenCareer(1, 0, 0); // Grocers and Sons, level 1
        career.TryOpenCareer(1, 0, 1); // The Food Hub, level 2
        Assert.Equal(2, career.RegisterDelivery("groceries").CredPEarned);
        Assert.Equal(3, career.RegisterDelivery("groceries").CredPEarned);
        // 250 - 100 - 150 = 0 spent, +5 ramped back.
        Assert.Equal(5, career.CredP);
    }

    // Unity's owner pool (PORTPLAN 18): the data file carries it; the test
    // fixture mirrors the built-in Unity names.
    private static readonly string[] OwnerPool =
    [
        "Elena Marchetti", "Luca Barbieri", "Sofia Romano", "Marco Conti",
        "Alessia Ferrari", "Matteo Bianchi", "Giulia Rossi", "Andrea Esposito",
        "Francesca Colombo", "Antonio Ricci", "Chiara Gallo", "Paolo Moretti",
    ];

    [Fact]
    public void Owner_ResolvesOncePerHub_StableAcrossCareers()
    {
        // Unity assigns the owner (GetHubOwnerName) at purchase, keyed off
        // the hub name — the same name always carries the same person, so
        // the freeroam map can pin its POI to them later.
        var realized = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])],
            CredPRamp.UnityDefault, OwnerPool);
        var first = new DeliveryCareer(realized, 100);
        first.TryOpenCareer(1, 0, 0);
        var second = new DeliveryCareer(realized, 100);
        second.TryOpenCareer(1, 0, 0);
        Assert.Equal(first.Owned[0].OwnerName, second.Owned[0].OwnerName);

        // The pick comes from the catalog's pool and the delivery hands it
        // to the receipt as the friendship owner.
        string owner = first.RegisterDelivery("groceries").OwnerName;
        Assert.Contains(owner, OwnerPool);
        Assert.Equal(first.Owned[0].OwnerName, owner);
    }
}
