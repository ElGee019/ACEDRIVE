using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The settle without a session (the S2 unblock): the delivery run prices, banks
/// and reports itself, and the two answers that used to need a whole overlay to
/// observe are asserted directly — per-store CredP, and an unowned car that pays
/// the run but banks no career progress.
/// </summary>
public sealed class JobSettlementTests
{
    private static readonly DeliveryStore Post = new(
        "adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries",
        new AddressLocation("Street 2", 5, HouseSide.Left));

    private static readonly DeliveryStore Bakery = new(
        "adenau-bakery", "Adenau Bakery", "Fuchs", 8, 20, 2.20, "groceries",
        new AddressLocation("Street 3", 7, HouseSide.Right));

    private static DeliveryCatalog Catalog => new([Post, Bakery], 0.7, 100);
    private static DeliveryRun Cart
    {
        get
        {
            DeliveryCatalog catalog = Catalog;
            DeliveryOrder[] orders =
            [
                catalog.Order(Post, Post.UnitsNeeded),
                catalog.Order(Bakery, Bakery.UnitsNeeded),
            ];
            // The route the run is charged for, stamped at DELIVER like a
            // taxi's ACCEPT distance; the tests never drive to earn it.
            return new DeliveryRun(orders) { DistanceKm = 8.2 };
        }
    }

    private static DeliveryCareer OwnedGroceryCareer()
    {
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])],
            CredPRamp.UnityDefault);
        var career = new DeliveryCareer(hubs, startCredP: 100, TestNames.For());
        Assert.True(career.TryOpenCareer(1, 0, 0)); // spends every CredP: the run starts empty
        return career;
    }

    [Fact]
    public void SettleDeliveryRun_SumsEveryStoreCredPAndBanksTheRun()
    {
        DeliveryCareer career = OwnedGroceryCareer();
        var (settlement, wallet, _, jobs, zone, online, host, _) = Build(career);

        settlement.SettleDeliveryRun(Cart, "Germany", host);

        // The producing hub's ramp pays per delivered store: 1 then 2.
        Assert.Equal(3.0, career.CredP);
        var receipt = Assert.IsType<DeliveryReceiptScreenViewModel>(Assert.Single(host.Receipts));
        Assert.True(receipt.HasCredP);
        Assert.Equal("Credibility Points earned: 3", receipt.CredPEarnedLine);
        Assert.Equal("Total Credibility Points: 3", receipt.TotalCredPLine);
        Assert.True(wallet.Cash > 35.0); // the goods margin plus the route fare
        Assert.Equal(1, jobs.Completed);
        Assert.Equal(1, zone.CountedJobs);
        Assert.Equal(1, host.ProgressBanked);
        Assert.Equal(1, host.DeliveryRunsCleared);
        Assert.Equal(1, host.DeliveryBoardRefreshes);
        Assert.Equal(("Germany", 8), Assert.Single(online.CountryPushes));
    }

    [Fact]
    public void SettleDeliveryRun_InAnUnownedCar_PaysTheRunButBanksNoProgress()
    {
        // A borrowed free-roam car must not distort the ownership economy: the
        // goods money still lands, the career counters and XP do not.
        DeliveryCareer career = OwnedGroceryCareer();
        var (settlement, wallet, garage, jobs, zone, online, host, _) =
            Build(career, liveCar: "ks_mazda_mx5_na");

        settlement.SettleDeliveryRun(Cart, "Germany", host);

        Assert.True(wallet.Cash > 35.0);
        Assert.Equal(0.0, wallet.Xp, 6);
        Assert.Equal(0, jobs.Completed);
        Assert.Equal(0, zone.CountedJobs);
        Assert.Empty(online.CountryPushes);
        Assert.Equal(0, host.ProgressBanked);
        Assert.Null(garage.Current); // the borrowed car is not in the garage
        Assert.IsType<DeliveryReceiptScreenViewModel>(Assert.Single(host.Receipts));
        Assert.Single(host.DriverSaves); // the cash still banks
    }

    [Fact]
    public void SettleDeliveryRun_SpendsTheProfitBoostCard_AndScalesTheGoods()
    {
        DeliveryCareer career = OwnedGroceryCareer();
        var trunk = new Trunk(
        [
            new TrunkItem("DELIVERY PROFIT BOOST", 0.0) { Quantity = 1 },
        ]);
        var (settlement, wallet, _, _, _, _, host, carrying) =
            Build(career, trunk: trunk);

        settlement.SettleDeliveryRun(Cart, "Germany", host);

        var receipt = Assert.IsType<DeliveryReceiptScreenViewModel>(
            Assert.Single(host.Receipts));
        Assert.True(receipt.HasProfitBoost);
        Assert.Contains("20%", receipt.ProfitBoostLine, StringComparison.Ordinal);
        // 20% on the 86.76 CR goods payout, the fare untouched: 104.11 in.
        Assert.Equal("€104.11", receipt.ReceivedLine.Amount);
        Assert.DoesNotContain(carrying.Items, item => item.Name == "DELIVERY PROFIT BOOST");
        Assert.True(wallet.Cash > 0.0);
    }

    [Fact]
    public void SettleDeliveryRun_SpendsOneChargeOfACounterCard()
    {
        // A completed job is one ride: a carried "next 5 rides" card counts
        // down, and the settle banks that wear with the job's driver save.
        DeliveryCareer career = OwnedGroceryCareer();
        var trunk = new Trunk(
        [
            new TrunkItem("HEAT-FREE RIDES", 0.0)
            {
                Quantity = 1,
                UsesLeft = 5,
                UsesMax = 5,
            },
        ]);
        var (settlement, _, _, _, _, _, host, carrying) =
            Build(career, trunk: trunk);

        settlement.SettleDeliveryRun(Cart, "Germany", host);

        Assert.Same(trunk, carrying);
        TrunkItem card = Assert.Single(carrying.Items);
        Assert.Equal(4, card.UsesLeft);
        Assert.Equal(5, card.UsesMax);
    }

    private static (
        JobSettlement Settlement, DriverWallet Wallet, Garage Garage, JobLedger Jobs,
        OperatingZone Zone, FakePlayerTelemetry Online,
        FakeJobSettlementHost Host, Trunk Trunk)
        Build(DeliveryCareer career, string? liveCar = null, Trunk? trunk = null)
    {
        // Deterministic locale: the receipt copy is asserted in en-US.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        DeliveryCatalog catalog = Catalog;
        var clock = new FakeClock();
        var game = new GameClock(clock, clock.UtcNow);
        var wallet = new DriverWallet(new DriverState(0.0, 0.0));
        var garage = new Garage([]);
        var brands = new FakeCarBrandSource
        {
            CurrentIdentity = liveCar is null ? null : new CarIdentity(liveCar, null),
        };
        var fleet = new FleetService(
            garage, [], game, wallet, InsuranceRules.Default, new NullLogger(),
            brands, new FakeContentSource(TestSession.EmptyContent));
        var zone = new OperatingZone(null, seed: 0, AddressMap.Default);
        var jobs = new JobLedger();
        var online = new FakePlayerTelemetry();
        var carrying = trunk ?? new Trunk();
        var glovebox = new Glovebox(RewardTable.Default, wallet);
        var settlement = new JobSettlement(
            wallet, garage, new TaxiShift(), zone, jobs, glovebox, carrying,
            new DailyStreak(clock, null), clock,
            fleet, new FakeCarRarity(), new FakeFriendshipStore(), new FakeBrandMileageStore(),
            online, new FakeRivals(), new DeliveryBoard(catalog), career,
            DeliveryFareTable.Default,
            FareTable.Default,
            TaxiRules.Default, new VoiceBeat(clock), null, new NullLogger());
        return (settlement, wallet, garage, jobs, zone, online,
            new FakeJobSettlementHost(), carrying);
    }
}
