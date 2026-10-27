using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The session-start catch-up on its own (PORTPLAN items 30/47): the starter car
/// a pre-garage save never created, and the login streak's recount plus its
/// once-a-day grant. The coordinator is not involved, so each test sees only
/// this collaborator's decisions: which gate held, which save it ran.
/// </summary>
public sealed class DriverCatchUpTests
{
    // FakeClock's default day; the saved streak dates are pinned against it so
    // "yesterday" is a real consecutive return.
    private const string Today = "2026-10-12";
    private const string Yesterday = "2026-10-11";
    private const string Starter = "ks_mazda_mx5_na";

    private sealed record Fixture(
        DriverCatchUp CatchUp, Garage Garage, DriverWallet Wallet, DailyStreak Streak,
        FakeDriverStore Drivers, FakeGarageStore Garages, TestLog Log);

    public DriverCatchUpTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static Fixture Build(
        bool onboardingDone, string? starterCarId = null,
        GarageState? savedGarage = null, DriverState? savedDriver = null)
    {
        var clock = new FakeClock();
        IReadOnlyList<CarSpec> pool = TestRoadCars.Pool;
        var garage = new Garage(pool, savedGarage);
        var wallet = new DriverWallet(savedDriver);
        var streak = new DailyStreak(clock, savedDriver);
        var drivers = new FakeDriverStore(savedDriver);
        var garages = new FakeGarageStore();
        var onboarding = new FakeOnboardingStore();
        onboarding.Save(OnboardingState.Fresh with
        {
            GrandmaCarId = starterCarId,
            BirthdayDone = onboardingDone,
        });
        var log = new TestLog();
        // One instance per service graph: every state owner is the instance the
        // assertions read, so the saver banks exactly what the rules changed.
        var saver = new GameStateSaver(
            clock, new GameClock(clock, clock.UtcNow),
            new FakeGameClockStore(), drivers, garages, new FakeCityStore(),
            new FakeHungerStore(), new FakeRoadTestStore(), new FakeShiftStore(),
            wallet, streak, new OperatingZone(null, seed: 0), new Trunk(), garage,
            new CityEconomy(DeliveryCatalog.Default.Stores, CityRules.Default),
            new Hunger(HungerRules.Default), TestRoadCars.Ledger(), new TaxiShift(),
            new ShiftRecord(null), new JobLedger(), new DestinationPins(), new VisitLedger(),
            new ValuationLedger(), new WhereIsWillyLedger());
        var catchUp = new DriverCatchUp(
            onboarding, garage, streak, wallet, new Random(0), saver, new FakeRivals(), log);
        return new Fixture(catchUp, garage, wallet, streak, drivers, garages, log);
    }

    [Fact]
    public void Run_SeedsGrandmasCarIntoAnEmptyGarage_AndBanksIt()
    {
        // A save whose onboarding already ran but whose garage is still empty
        // (every save written before the garage existed) opens on the car the
        // player picked instead of an empty garage.
        var fixture = Build(onboardingDone: true, starterCarId: Starter);

        fixture.CatchUp.Run(displayWeek: 3);

        OwnedCar car = Assert.Single(fixture.Garage.Cars);
        Assert.Equal(Starter, car.Key);
        Assert.Equal(3, car.PurchaseWeek);
        Assert.Equal(Starter, fixture.Garage.CurrentKey);
        Assert.Equal(1, fixture.Garages.Saves);
        Assert.Equal(Starter, fixture.Garages.Load()!.CurrentKey);
        Assert.Contains($"garage seeded with grandma's car: {Starter}", fixture.Log.Infos);
    }

    [Fact]
    public void Run_LeavesAResumedGarageAlone()
    {
        var resumed = new GarageState(
            Starter, [new OwnedCarState(Starter, PurchaseWeek: 2, Odometer: 12.0)]);
        var fixture = Build(
            onboardingDone: true, starterCarId: Starter, savedGarage: resumed);

        fixture.CatchUp.Run(displayWeek: 5);

        OwnedCar car = Assert.Single(fixture.Garage.Cars);
        Assert.Equal(12.0, car.Odometer);
        Assert.Equal(2, car.PurchaseWeek);
        Assert.Equal(0, fixture.Garages.Saves);
    }

    [Fact]
    public void Run_HoldsTheSeedBackWhileTheIntroductionRuns()
    {
        // Unity creates the starter car at the pick itself, so an unfinished
        // walk is the only state where a saved car id exists and the garage is
        // legitimately still empty: boot must not seed it early.
        var fixture = Build(onboardingDone: false, starterCarId: Starter);

        fixture.CatchUp.Run(displayWeek: 1);

        Assert.Empty(fixture.Garage.Cars);
        Assert.Equal(0, fixture.Garages.Saves);
    }

    [Fact]
    public void Run_GrantsTodaysBonusOnce_AndBanksTheDriver()
    {
        // Unity's GrantDailyBonusIfDue: a yesterday login continues the streak
        // to day 3, whose curve pays 4 CR, then day 3 hands three random skill
        // points out. The grant is progress, so it banks straight away.
        var saved = new DriverState(10.0, 0.0, DaysInARow: 2, LastLoginDate: Yesterday);
        var fixture = Build(onboardingDone: true, savedDriver: saved);

        fixture.CatchUp.Run(displayWeek: 1);

        Assert.Equal(14.0, fixture.Wallet.Cash);
        Assert.Equal(3, fixture.Streak.DaysInARow);
        Assert.Equal(63.0,
            fixture.Wallet.Rating.Cornering + fixture.Wallet.Rating.Concentration
                + fixture.Wallet.Rating.OffTheLine,
            precision: 9);
        DriverState granted = Assert.Single(fixture.Drivers.Saved);
        Assert.Equal(3, granted.DaysInARow);
        Assert.Equal(Today, granted.LastLoginDate);
        Assert.Equal(Today, granted.LastDailyBonusDate);
        Assert.Equal(14.0, granted.Cash, precision: 9);
        Assert.Contains(
            fixture.Log.Infos,
            line => line.StartsWith("[Streak] consecutive-day bonus granted: 4 CR", StringComparison.Ordinal));

        // A second launch the same day recounts the same day and pays nothing.
        fixture.CatchUp.Run(displayWeek: 1);
        Assert.Equal(14.0, fixture.Wallet.Cash);
        Assert.Single(fixture.Drivers.Saved);
    }

    [Fact]
    public void Run_RecountsTheStreakButPaysNothingBeforeOnboarding()
    {
        // Unity's firstSceneComplete gate: the counter follows the saved dates
        // from the first launch on, only the grant waits for the introduction.
        var saved = new DriverState(10.0, 0.0, DaysInARow: 2, LastLoginDate: Yesterday);
        var fixture = Build(onboardingDone: false, savedDriver: saved);

        fixture.CatchUp.Run(displayWeek: 1);

        Assert.Equal(3, fixture.Streak.DaysInARow);
        Assert.Equal(Today, fixture.Streak.LastLoginDate);
        Assert.Equal(10.0, fixture.Wallet.Cash);
        Assert.Null(fixture.Streak.LastDailyBonusDate);
        Assert.Empty(fixture.Drivers.Saved);
    }

    [Fact]
    public void Run_AlreadyPaidToday_WritesNothing()
    {
        // The quiet launch: same day, nothing due, no seed pending. One read of
        // the onboarding file and no disk write at all.
        var saved = new DriverState(
            10.0, 0.0, DaysInARow: 3, LastLoginDate: Today, LastDailyBonusDate: Today);
        var fixture = Build(onboardingDone: true, savedDriver: saved);

        fixture.CatchUp.Run(displayWeek: 1);

        Assert.Equal(10.0, fixture.Wallet.Cash);
        Assert.Empty(fixture.Drivers.Saved);
        Assert.Equal(0, fixture.Garages.Saves);
    }
}
