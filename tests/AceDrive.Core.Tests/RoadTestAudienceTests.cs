using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The road test's audience and earnings math (PORTPLAN item 49, port of Unity
/// RoadTestAudienceCalculator): friends count whole and acquaintances half,
/// Unity's banker's rounding carries over digit for digit, and a settled test
/// always leaves at least one reputation point.
/// </summary>
public sealed class RoadTestAudienceTests
{
    [Fact]
    public void PeopleReached_CountsFriendsWholeAndAcquaintancesHalf()
    {
        // (3 + 2 × 0.5) × 1000 / 100 = 40 people.
        Assert.Equal(40, RoadTestAudience.PeopleReached(citizens: 1000, friends: 3, acquaintances: 2));
    }

    [Fact]
    public void PeopleReached_RoundsLikeUnitysBankersRounding()
    {
        // 0.5 people: Mathf.RoundToInt answers 0 (the even digit), not 1.
        Assert.Equal(0, RoadTestAudience.PeopleReached(citizens: 100, friends: 0, acquaintances: 1));
        // 1.5 people: the even digit is 2.
        Assert.Equal(2, RoadTestAudience.PeopleReached(citizens: 300, friends: 0, acquaintances: 1));
    }

    [Fact]
    public void Earnings_AreTheForfeitPlusTheCommission()
    {
        // 40 people buying a 50 CR magazine: a tenth is 200 CR, plus 400.
        Assert.Equal(200.0, RoadTestAudience.Commission(40, 50.0, RoadTestAudienceRules.Default));
        Assert.Equal(600.0, RoadTestAudience.Earnings(40, 50.0, RoadTestAudienceRules.Default));
    }

    [Fact]
    public void MagazinePrice_IsHalfTheJournalistReputation()
    {
        Assert.Equal(32.5, RoadTestAudience.MagazinePrice(65.0, RoadTestAudienceRules.Default), precision: 6);
    }

    [Fact]
    public void ResultReputation_StaysAtUnitysNeutralFifty()
    {
        Assert.Equal(
            RoadTestAudience.NeutralResultReputation,
            RoadTestAudience.ResultReputation(
                RoadTestAudience.NeutralOutcome, RoadTestAudience.NeutralOutcome));
    }

    [Fact]
    public void ResultReputation_NeverDropsToZero()
    {
        Assert.Equal(1, RoadTestAudience.ResultReputation(0.0, 0.0));
    }

    [Fact]
    public void NextNaturalReputation_RisesOneAndStopsAtTheCap()
    {
        Assert.Equal(1, RoadTestAudience.NextNaturalReputation(0));
        Assert.Equal(
            RoadTestAudience.MaxNaturalReputation,
            RoadTestAudience.NextNaturalReputation(RoadTestAudience.MaxNaturalReputation));
    }

    [Fact]
    public void CarJournalistReputation_ScalesByTheSquareRootOfVariety()
    {
        Assert.Equal(100.0, RoadTestAudience.CarJournalistReputation(2, 50, 1.0), precision: 6);
        Assert.Equal(
            50.0 * Math.Sqrt(0.25),
            RoadTestAudience.CarJournalistReputation(1, 50, 0.25),
            precision: 6);
    }

    [Fact]
    public void Variety_FirstPhotoIsFull()
    {
        Assert.Equal(1.0, RoadTestAudience.Variety(false, 0.0, 10_000.0));
    }

    [Fact]
    public void Variety_IsTheShareOfTheZonesWidestSpan()
    {
        Assert.Equal(0.5, RoadTestAudience.Variety(true, 5_000.0, 10_000.0), precision: 6);
    }

    [Fact]
    public void Variety_SameSpotKeepsUnitysFloor()
    {
        // A repeat shoot pays the 0.1% floor, exactly like Unity's clamp.
        Assert.Equal(0.001, RoadTestAudience.Variety(true, 0.0, 10_000.0), precision: 6);
    }

    [Fact]
    public void Variety_ClampsOvershootAndUnusableGeometry()
    {
        Assert.Equal(1.0, RoadTestAudience.Variety(true, 20_000.0, 10_000.0), precision: 6);
        Assert.Equal(1.0, RoadTestAudience.Variety(true, double.NaN, 10_000.0));
        Assert.Equal(1.0, RoadTestAudience.Variety(true, 100.0, 0.0));
    }
}
