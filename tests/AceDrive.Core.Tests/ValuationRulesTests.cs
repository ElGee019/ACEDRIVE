using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// Weekly valuation rules (PORTPLAN 49): the Monday schedule, the week key,
/// the placeholder venue draw, the jury score and the draw/verdict/offline
/// maths Unity runs on the weekly list.
/// </summary>
public sealed class ValuationRulesTests
{
    [Fact]
    public void Wipe_IsDueOnlyOnMondayMidnight()
    {
        Assert.True(ValuationRules.IsWipeDue(dayOfWeek: 2, hour: 0, minute: 0));
        Assert.False(ValuationRules.IsWipeDue(dayOfWeek: 2, hour: 0, minute: 1));
        Assert.False(ValuationRules.IsWipeDue(dayOfWeek: 3, hour: 0, minute: 0));
    }

    [Fact]
    public void Reset_IsOverdue_WhenAMondayPassedWithoutAWipe()
    {
        // Current game day 10 is a Wednesday; the most recent Monday is day 8.
        Assert.True(ValuationRules.IsResetOverdue(
            lastResetGameDay: 3, currentGameDay: 10, currentDayOfWeek: 4));
        Assert.False(ValuationRules.IsResetOverdue(
            lastResetGameDay: 8, currentGameDay: 10, currentDayOfWeek: 4));
        Assert.False(ValuationRules.IsResetOverdue(
            lastResetGameDay: 0, currentGameDay: 0, currentDayOfWeek: 4));
    }

    [Theory]
    [InlineData(2, 7, false)]
    [InlineData(2, 8, true)]
    [InlineData(2, 12, true)]
    [InlineData(2, 19, true)]
    [InlineData(2, 20, false)]
    [InlineData(3, 12, false)]
    [InlineData(1, 12, false)]
    public void Window_IsMondayEightToEight(int dayOfWeek, int hour, bool expected) =>
        Assert.Equal(expected, ValuationRules.IsWindow(dayOfWeek, hour));

    [Theory]
    [InlineData(2, 5, false)]
    [InlineData(2, 6, true)]
    [InlineData(2, 7, true)]
    [InlineData(2, 19, true)]
    [InlineData(2, 20, false)]
    [InlineData(3, 6, false)]
    public void Preload_OpensTwoHoursBeforeTheWindow(int dayOfWeek, int hour, bool expected) =>
        Assert.Equal(expected, ValuationRules.IsPreloadWindow(dayOfWeek, hour));

    [Fact]
    public void WeekKey_ChangesOnTheSundayToMondayMidnight()
    {
        // 2024-01-01 was a Monday, the epoch the key counts from.
        Assert.Equal(0, ValuationRules.WeekKey(new DateTime(2024, 1, 1, 0, 0, 0)));
        Assert.Equal(0, ValuationRules.WeekKey(new DateTime(2024, 1, 7, 23, 59, 59)));
        Assert.Equal(1, ValuationRules.WeekKey(new DateTime(2024, 1, 8, 0, 0, 0)));
        Assert.Equal(1, ValuationRules.WeekKey(new DateTime(2024, 1, 14, 23, 59, 59)));
        Assert.Equal(2, ValuationRules.WeekKey(new DateTime(2024, 1, 15, 0, 0, 0)));
    }

    [Fact]
    public void Venue_DrawsEveryHouseOnBothStreetsExactlyOnce()
    {
        AddressMap map = AddressMap.Default;
        int total = 0;
        foreach (AddressStreet street in map.Streets)
        {
            total += HouseNumberPlan.Count(street, map);
        }
        Assert.True(total > 1);

        HashSet<(string StreetId, int House)> drawn = [];
        for (int index = 0; index < total; index++)
        {
            ValuationVenue? venue = ValuationRules.PickVenue(map, _ => index);
            Assert.NotNull(venue);
            Assert.InRange(
                venue!.HouseNumber, 1, HouseNumberPlan.Count(
                    Assert.Single(map.Streets, s => s.Id == venue.StreetId), map));
            drawn.Add((venue.StreetId, venue.HouseNumber));
        }

        // Uniform over the houses: every door is reachable, and both streets
        // take their share (the map's two-placeholder-street rule).
        Assert.Equal(total, drawn.Count);
        Assert.Equal(
            map.Streets.Select(street => street.Id).Distinct().Count(),
            drawn.Select(venue => venue.StreetId).Distinct().Count());
    }

    [Fact]
    public void Venue_Draw_IsNullWithoutAStreet()
    {
        var empty = new AddressMap("Germany", "Brands", 1929.0, 30.0, []);
        Assert.Null(ValuationRules.PickVenue(empty, _ => 0));
        Assert.Null(ValuationRules.PickVenue(AddressMap.Default, null!));
    }

    [Fact]
    public void HouseEntry_PicksOneOfTheProvidedCars()
    {
        string? car = ValuationRules.PickRandomCarId(
            ["a", "b", "c"], _ => 1);
        Assert.Equal("b", car);
        Assert.Null(ValuationRules.PickRandomCarId([], _ => 0));
    }

    [Fact]
    public void Score_AddsEveryUnityTerm()
    {
        var car = new CarSpec(
            "ks_test", 2020, "Test", "One", Class: 5, Price: 20000.0,
            Horsepower: 150, TopSpeedKmh: 200);

        // class 5 + 200/2 + 150/10 + 20000/1000 + 3 jobs + a fresh engine's 100.
        Assert.Equal(243.0, ValuationRules.Score(car, jobsWithCar: 3, 0.0), 6);
    }

    [Fact]
    public void Score_ClampsJobsAndTheEnginePercent()
    {
        var car = new CarSpec(
            "ks_test", 2020, "Test", "One", Class: 5, Price: 20000.0,
            Horsepower: 150, TopSpeedKmh: 200);

        Assert.Equal(
            ValuationRules.Score(car, jobsWithCar: 0, engineBrokenPercent: 1.0),
            ValuationRules.Score(car, jobsWithCar: 0, engineBrokenPercent: 4.0), 6);
        Assert.Equal(
            ValuationRules.Score(car, jobsWithCar: 0, engineBrokenPercent: 0.0),
            ValuationRules.Score(car, jobsWithCar: 0, engineBrokenPercent: -2.0), 6);
        // A negative job count never takes points off the car.
        Assert.Equal(
            ValuationRules.Score(car, jobsWithCar: 0, engineBrokenPercent: 0.0),
            ValuationRules.Score(car, jobsWithCar: -5, engineBrokenPercent: 0.0), 6);
    }

    [Fact]
    public void Score_WithoutACar_IsZero() => Assert.Equal(0.0, ValuationRules.Score(null, 3, 0.0));

    [Fact]
    public void Verdict_TiesFavourThePlayer()
    {
        ValuationVerdict tie = ValuationRules.Verdict(100.0, 100.0);
        Assert.True(tie.Won);
        Assert.Equal(0.0, tie.Gap);
        Assert.Equal(0.0, tie.Xp);
    }

    [Fact]
    public void Verdict_WinnerPaysTheGapOverFiveHundred()
    {
        ValuationVerdict verdict = ValuationRules.Verdict(100.0, 90.0);
        Assert.True(verdict.Won);
        Assert.Equal(10.0, verdict.Gap, 6);
        Assert.Equal(10.0 / 500.0, verdict.Xp, 6);
    }

    [Fact]
    public void Verdict_LoserBanksTheGapOverOneThousand()
    {
        ValuationVerdict verdict = ValuationRules.Verdict(90.0, 100.0);
        Assert.False(verdict.Won);
        Assert.Equal(10.0, verdict.Gap, 6);
        Assert.Equal(10.0 / 1000.0, verdict.Xp, 6);
    }

    [Fact]
    public void Competitor_SkipsThePlayerAndTheLegacyHouseDoc()
    {
        ValuationEntry own = Entry("me", 100.0);
        ValuationEntry house = Entry(ValuationRules.HouseEntryDocId, 50.0);
        ValuationEntry first = Entry("rival-1", 90.0);
        ValuationEntry second = Entry("rival-2", 110.0);

        Assert.Same(first, ValuationRules.SelectCompetitor(
            [own, house, first, second], "me", _ => 0));
        Assert.Same(second, ValuationRules.SelectCompetitor(
            [own, house, first, second], "me", _ => 1));
        Assert.Null(ValuationRules.SelectCompetitor([own, house], "me", _ => 0));
    }

    [Fact]
    public void FindUnseen_SkipsSelfAndSettledEntries()
    {
        ValuationEntry own = Entry("me", 100.0);
        ValuationEntry settled = Entry("old", 90.0);
        ValuationEntry fresh = Entry("new", 110.0);

        IReadOnlyList<ValuationEntry> unseen = ValuationRules.FindUnseen(
            ["old"], [own, settled, fresh], "me");

        Assert.Same(fresh, Assert.Single(unseen));
    }

    [Fact]
    public void OfflineGains_PayWinsOnlyAndSettleEveryFreshUid()
    {
        // Unity's catch-up: a win pays, a loss and a tie pay nothing, and every
        // fresh uid joins the settled set so the live path cannot pay it again.
        ValuationEntry win = Entry("win", 90.0);
        ValuationEntry loss = Entry("loss", 110.0);
        ValuationEntry tie = Entry("tie", 100.0);
        ValuationEntry own = Entry("me", 100.0);
        ValuationEntry settled = Entry("old", 10.0);

        ValuationOfflineGains gains = ValuationRules.ComputeOfflineGains(
            100.0, ["old"], [win, loss, tie, own, settled], "me");

        Assert.Equal(1, gains.WinCount);
        Assert.Equal(10.0 / 500.0, gains.TotalXp, 6);
        // Every fresh uid settles, own included (the live path skips it twice
        // over); a uid already seen keeps its settled state.
        Assert.Equal(["win", "loss", "tie", "me", "old"], gains.NewSeenUids);
    }

    [Fact]
    public void Expiry_IsEntryPlusFortyEightHours()
    {
        var entered = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(entered.AddHours(48), ValuationRules.ComputeExpiryUtc(entered));
    }

    private static ValuationEntry Entry(string uid, double score) =>
        new(uid, uid, "ks_test", 1, 0.0, score);
}
