using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The pump price (PORTPLAN item 37, Unity GameClockManager
/// UpdateFuelPrice): Unity's 1.65 seed, one uniform drift per game day inside
/// the authored window, clamped to the band, and a resumed save that keeps its
/// day stamp instead of re-seeding.</summary>
public sealed class FuelMarketTests
{
    private static FuelRules Rules => FuelRules.Default;

    [Fact]
    public void FirstLaunch_StartsAtUnitysSeed_AndWaitsForItsFirstMidnight()
    {
        var market = new FuelMarket(Rules, saved: null, gameDay: 100, seed: 1);

        Assert.Equal(1.65, market.PricePerLitre, 10);
        Assert.Equal(100, market.LastRollDay);
        Assert.False(market.RollTo(100)); // the day it opened on has already rolled
    }

    [Fact]
    public void DayFlip_MovesThePriceInsideUnitysWindow_AndStampsTheDay()
    {
        var market = new FuelMarket(Rules, saved: null, gameDay: 100, seed: 7);

        Assert.True(market.RollTo(101));

        double change = market.PricePerLitre - 1.65;
        Assert.InRange(change, -0.15, 0.15);
        Assert.Equal(101, market.LastRollDay);
        Assert.False(market.RollTo(101)); // one roll per day, never per poll
    }

    [Fact]
    public void TheDailyRoll_ClampsToUnitysBand()
    {
        // A pinned window makes the direction deterministic, so the clamp is
        // what the assertion tests rather than the seed's luck.
        FuelRules rising = Rules with { DailyChangeMin = 0.15, DailyChangeMax = 0.15 };
        var market = new FuelMarket(rising, new FuelPriceState(2.19, 100), 100, seed: 1);
        market.RollTo(101);
        Assert.Equal(2.20, market.PricePerLitre, 10);

        FuelRules falling = Rules with { DailyChangeMin = -0.15, DailyChangeMax = -0.15 };
        market = new FuelMarket(falling, new FuelPriceState(1.41, 100), 100, seed: 1);
        market.RollTo(101);
        Assert.Equal(1.40, market.PricePerLitre, 10);
    }

    [Fact]
    public void DaysMissedWhileClosed_CatchUpOneRollEach_AndStayInTheBand()
    {
        var market = new FuelMarket(Rules, new FuelPriceState(1.80, 97), 97, seed: 5);

        Assert.True(market.RollTo(100));

        Assert.Equal(100, market.LastRollDay);
        Assert.InRange(market.PricePerLitre, Rules.MinPricePerLitre, Rules.MaxPricePerLitre);
    }

    [Fact]
    public void AResumedSave_KeepsItsPrice()
    {
        var market = new FuelMarket(Rules, new FuelPriceState(1.90, 50), gameDay: 50, seed: 1);

        Assert.Equal(1.90, market.PricePerLitre, 10);
        Assert.Equal(50, market.LastRollDay);
        Assert.False(market.RollTo(50));
    }

    [Fact]
    public void ADayStampAheadOfTheClock_IsReanchored_SoTheDriftResumes()
    {
        // A hand-edited or foreign save must not freeze the market until the
        // old stamp passes.
        var market = new FuelMarket(Rules, new FuelPriceState(1.90, 500), gameDay: 100, seed: 1);

        Assert.Equal(100, market.LastRollDay);
        Assert.True(market.RollTo(101));
    }

    [Fact]
    public void ANonPositiveSavedPrice_StartsOverFromTheShippedSeed()
    {
        var market = new FuelMarket(Rules, new FuelPriceState(-3.0, 100), gameDay: 100, seed: 1);

        Assert.Equal(1.65, market.PricePerLitre, 10);
        Assert.Equal(100, market.LastRollDay);
    }

    [Fact]
    public void Snapshot_RoundTripsTheReadingTheStoreBanks()
    {
        var market = new FuelMarket(Rules, new FuelPriceState(1.90, 50), gameDay: 50, seed: 1);
        market.RollTo(51);

        FuelPriceState snapshot = market.Snapshot();

        Assert.Equal(market.PricePerLitre, snapshot.PricePerLitre, 10);
        Assert.Equal(51, snapshot.LastRollDay);
    }
}
