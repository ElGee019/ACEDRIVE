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
    private static DeliveryRun Cart => new(
        [Post.Key, Bakery.Key], PaidForGoods: 28.0, ReceivedForGoods: 63.0);

    private static DeliveryCareer OwnedGroceryCareer()
    {
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])],
            CredPRamp.UnityDefault, ["Elena Marchetti"]);
        var career = new DeliveryCareer(hubs, startCredP: 100);
        Assert.True(career.TryOpenCareer(1, 0, 0)); // spends every CredP: the run starts empty
        return career;
    }

    [Fact]
    public void SettleDeliveryRun_SumsEveryStoreCredPAndBanksTheRun()
    {
        DeliveryCareer career = OwnedGroceryCareer();
        var (settlement, wallet, _, jobs, zone, online, meter, host) = Build(career);
        Drive(meter);

        settlement.SettleDeliveryRun(Cart, "Germany", host);

        // The producing hub's ramp pays per delivered store: 1 then 2.
        Assert.Equal(3.0, career.CredP);
        var receipt = Assert.IsType<DeliveryReceiptScreenViewModel>(Assert.Single(host.Receipts));
        Assert.True(receipt.HasCredP);
        Assert.Equal("Credibility Points earned: 3", receipt.CredPEarnedLine);
        Assert.Equal("Total Credibility Points: 3", receipt.TotalCredPLine);
        Assert.True(wallet.Cash > 35.0); // the goods margin plus the measured distance
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
        var (settlement, wallet, garage, jobs, zone, online, meter, host) =
            Build(career, liveCar: "ks_mazda_mx5_na");
        Drive(meter);

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

    // 8.2 km at 360 km/h in half-second polls: the meter ignores a stalled tick
    // (dt ≥ 1 s), so the drive is fed the way the 10 Hz poll would.
    private static void Drive(TaxiDriveMeter meter)
    {
        for (int i = 0; i < 164; i++)
        {
            meter.Add(360f, 0.5);
        }
    }

    private static (
        JobSettlement Settlement, DriverWallet Wallet, Garage Garage, JobLedger Jobs,
        OperatingZone Zone, FakePlayerTelemetry Online, TaxiDriveMeter Meter,
        FakeJobSettlementHost Host)
        Build(DeliveryCareer career, string? liveCar = null)
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
        var zone = new OperatingZone(null, seed: 0);
        var jobs = new JobLedger();
        var online = new FakePlayerTelemetry();
        var meter = new TaxiDriveMeter(TaxiRules.Default.MaxMeterSpeedKmh);
        var settlement = new JobSettlement(
            wallet, garage, new TaxiShift(), zone, jobs, new DailyStreak(clock, null), clock,
            fleet, new FakeCarRarity(), new FakeFriendshipStore(), new FakeBrandMileageStore(),
            online, new FakeRivals(), new DeliveryBoard(catalog), career,
            new CityEconomy(catalog.Stores, CityRules.Default), DeliveryFareTable.Default,
            meter, FareTable.Default,
            TaxiRules.Default, new VoiceBeat(clock), null, new NullLogger());
        return (settlement, wallet, garage, jobs, zone, online, meter,
            new FakeJobSettlementHost());
    }
}
