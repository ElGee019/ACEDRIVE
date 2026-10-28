using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The car-haul settle runs headless: the hauled car's band and the driver's
/// rating price the placeholder leg, the wallet banks the cash alone, the
/// calendar takes its checkpoint and the receipt is the caller's to show. The
/// assertions read the same wallet and clock store the flow writes.
/// </summary>
public sealed class CarHaulFlowTests
{
    // The receipt prints money in the current culture, and the suite shares
    // one process: pin en-US so a neighbour's language cannot leak in.
    public CarHaulFlowTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static (CarHaulFlow Flow, DriverWallet Wallet, FakeGameClockStore Clocks, TestLog Log)
        Create(
            CarIdentity? identity = null, IReadOnlyList<CarSpec>? cars = null,
            DriverWallet? wallet = null, int startWeek = 3)
    {
        var clock = new FakeClock();
        var game = new GameClock(clock, clock.UtcNow);
        var beat = new VoiceBeat(clock);
        var log = new TestLog();
        var walletInstance = wallet ?? new DriverWallet(new DriverState(0.0, 0.0));
        IReadOnlyList<CarSpec> pool = cars ?? DefaultPool;
        var brands = new FakeCarBrandSource { CurrentIdentity = identity };
        var garage = new Garage(pool);
        var trunk = new Trunk();
        var hunger = new Hunger(HungerRules.Default);
        var city = new CityEconomy([], CityRules.Default);
        var roads = new RoadTestLedger(pool, seed: 0);
        var shift = new TaxiShift();
        var clocks = new FakeGameClockStore();
        // One instance per service graph: the saver writes the wallet and the
        // clock store the assertions read.
        var saver = new GameStateSaver(
            clock, game, clocks, new FakeDriverStore(), new FakeGarageStore(),
            new FakeCityStore(), new FakeHungerStore(), new FakeRoadTestStore(),
            new FakeShiftStore(), walletInstance, new DailyStreak(clock, null),
            new OperatingZone(null, seed: 0, AddressMap.Default), trunk, garage, city, hunger, roads,
            shift, new ShiftRecord(null), new JobLedger(), new DestinationPins(), new VisitLedger(),
            new ValuationLedger(), new WhereIsWillyLedger(),
            new DeliveryCareer(HubCatalog.Default, 0));
        var day = new DayCycle(
            game, shift, new ShiftRecord(null), new FakeShiftStandings(),
            new FakePlayerTelemetry(), new FakeProfileStore(), new FakeRivals(),
            saver, beat, startWeek, log);
        var flow = new CarHaulFlow(brands, pool, walletInstance, day, saver, beat, log);
        return (flow, walletInstance, clocks, log);
    }

    // The receipt's own zero: a class band, so the placeholder rules do not
    // decide the tier by accident.
    private static readonly IReadOnlyList<CarSpec> DefaultPool =
    [
        new("ks_test_hatch", 2015, "Test", "Hatch", Passengers: 2, Class: 15),
    ];

    private static RelayCommand Back() => new RelayCommand(() => { });

    [Fact]
    public void Settle_NamesTheDrivenCarAndPricesItsBand()
    {
        var (flow, wallet, _, log) = Create(new CarIdentity("ks_test_hatch", null));

        CarHaulReceiptScreenViewModel receipt = flow.Settle(Back());

        // Class 15 -> tier 6; the wallet's default rating is 20, so the 30-minute
        // placeholder leg earns 29.88 at 0.996/min.
        Assert.Equal("TEST HATCH", receipt.CarLine);
        Assert.Equal("The current class is Tier 6", receipt.TierLine);
        Assert.Equal("Resulting in a rate of €1.00/min", receipt.RateLine);
        Assert.Equal("Wallet: €29.88", receipt.WalletLine);
        Assert.Equal(29.88, wallet.Cash);
        // Unity's haul pays cash alone: no XP tail on the wallet or the page.
        Assert.Equal(0.0, wallet.Xp);
        Assert.Contains(log.Infos, line => line.StartsWith("haul settled:", StringComparison.Ordinal));
    }

    [Fact]
    public void Settle_UnknownCar_KeepsThePlaceholderBand()
    {
        var (flow, wallet, _, log) = Create();

        CarHaulReceiptScreenViewModel receipt = flow.Settle(Back());

        Assert.Equal(Strings.Get("HaulReceiptCarUnknown"), receipt.CarLine);
        // Lowest band (placeholder class 1): 0.83 * (20 * 1 / 100) = 0.166,
        // cents-rounded to a 0.17 rate; 30 minutes earn 4.98.
        Assert.Equal("The current class is Tier 1", receipt.TierLine);
        Assert.Equal("Resulting in a rate of €0.17/min", receipt.RateLine);
        Assert.Equal(4.98, wallet.Cash);
        Assert.Contains(
            log.Infos,
            line => line.EndsWith("(placeholder, no pool class)", StringComparison.Ordinal));
    }

    [Fact]
    public void Settle_BanksTheCalendarAtTheDisplayWeek()
    {
        var (flow, _, clocks, _) = Create(new CarIdentity("ks_test_hatch", null), startWeek: 3);

        flow.Settle(Back());

        // Checkpoint bank, like the paid jobs: settled cash is visible progress.
        Assert.Equal(3, Assert.Single(clocks.Saved).Week);
    }

    [Fact]
    public void Settle_HandsTheReceiptBackWithTheCallersContinue()
    {
        var (flow, _, _, _) = Create(new CarIdentity("ks_test_hatch", null));
        bool back = false;
        var control = new RelayCommand(() => back = true);

        CarHaulReceiptScreenViewModel receipt = flow.Settle(control);
        receipt.ContinueCommand.Execute(null);

        // The flow owns the page, the caller owns where BACK lands.
        Assert.True(back);
    }
}
