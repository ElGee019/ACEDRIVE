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
        Assert.Equal(
            243.0, ValuationRules.Score(car, jobsWithCar: 3, 0.0, leaderboardRanking: 0.0), 6);
    }

    [Fact]
    public void Score_AddsTheLeaderboardTerm()
    {
        var car = new CarSpec(
            "ks_test", 2020, "Test", "One", Class: 5, Price: 20000.0,
            Horsepower: 150, TopSpeedKmh: 200);

        // The placeholder ranking (1.0 on every owned car) is worth the full
        // 150 points, so a fresh entry reads 243 + 150; an unranked car (the
        // house entry) adds nothing.
        Assert.Equal(
            243.0, ValuationRules.Score(car, jobsWithCar: 3, 0.0, leaderboardRanking: 0.0), 6);
        Assert.Equal(
            393.0, ValuationRules.Score(car, jobsWithCar: 3, 0.0, leaderboardRanking: 1.0), 6);
        Assert.Equal(
            243.0 + 37.5,
            ValuationRules.Score(car, jobsWithCar: 3, 0.0, leaderboardRanking: 0.5), 6);
    }

    [Fact]
    public void Score_ClampsJobsAndTheEnginePercent()
    {
        var car = new CarSpec(
            "ks_test", 2020, "Test", "One", Class: 5, Price: 20000.0,
            Horsepower: 150, TopSpeedKmh: 200);

        Assert.Equal(
            ValuationRules.Score(car, jobsWithCar: 0, engineBrokenPercent: 1.0, leaderboardRanking: 0.0),
            ValuationRules.Score(car, jobsWithCar: 0, engineBrokenPercent: 4.0, leaderboardRanking: 0.0), 6);
        Assert.Equal(
            ValuationRules.Score(car, jobsWithCar: 0, engineBrokenPercent: 0.0, leaderboardRanking: 0.0),
            ValuationRules.Score(car, jobsWithCar: 0, engineBrokenPercent: -2.0, leaderboardRanking: 0.0), 6);
        // A negative job count never takes points off the car.
        Assert.Equal(
            ValuationRules.Score(car, jobsWithCar: 0, engineBrokenPercent: 0.0, leaderboardRanking: 0.0),
            ValuationRules.Score(car, jobsWithCar: -5, engineBrokenPercent: 0.0, leaderboardRanking: 0.0), 6);
    }

    [Fact]
    public void LeaderboardPoints_SquareTheRankingOntoTheMaximum()
    {
        Assert.Equal(0.0, ValuationRules.LeaderboardPoints(0.0));
        Assert.Equal(37.5, ValuationRules.LeaderboardPoints(0.5), 6);
        Assert.Equal(ValuationRules.LeaderboardMaxPoints, ValuationRules.LeaderboardPoints(1.0), 6);
        // A hand-written ranking can never out-earn a perfect one, and a
        // negative one can never take points off the score.
        Assert.Equal(ValuationRules.LeaderboardMaxPoints, ValuationRules.LeaderboardPoints(4.0), 6);
        Assert.Equal(0.0, ValuationRules.LeaderboardPoints(-1.0));
    }

    [Fact]
    public void LeaderboardRanking_AveragesTheThreeBestPercentiles()
    {
        // Percentile = (field - rank) / field: 0.9, 0.5, 0.25, 0.1. The best
        // three average, so the weakest stretch does not drag the ranking down.
        StretchRank[] ranks =
        [
            new(SameCarRank: 1, TotalSameCarPlayers: 10),
            new(SameCarRank: 5, TotalSameCarPlayers: 10),
            new(SameCarRank: 3, TotalSameCarPlayers: 4),
            new(SameCarRank: 9, TotalSameCarPlayers: 10),
        ];

        double ranking = ValuationRules.LeaderboardRanking(ranks);

        Assert.Equal((0.9 + 0.5 + 0.25) / 3.0, ranking, 6);
    }

    [Fact]
    public void LeaderboardRanking_FewerThanThreeStretches_AveragesWhatExists()
    {
        Assert.Equal(
            0.9, ValuationRules.LeaderboardRanking([new(1, 10)]), 6);
        // A lone entrant has no percentile to brag about: the field is one.
        Assert.Equal(
            0.0, ValuationRules.LeaderboardRanking([new(1, 1)]), 6);
    }

    [Fact]
    public void LeaderboardRanking_WithNoUsableBoard_IsUnranked()
    {
        Assert.Equal(0.0, ValuationRules.LeaderboardRanking(null));
        Assert.Equal(0.0, ValuationRules.LeaderboardRanking([]));
        // A zero (or negative) field cannot produce a percentile; a rank past
        // the field clamps to the bottom instead of going negative.
        Assert.Equal(0.0, ValuationRules.LeaderboardRanking([new(1, 0)]));
        Assert.Equal(0.0, ValuationRules.LeaderboardRanking([new(15, 10)]), 6);
    }

    [Fact]
    public void Score_WithoutACar_IsZero() =>
        Assert.Equal(0.0, ValuationRules.Score(null, 3, 0.0, leaderboardRanking: 1.0));

    [Fact]
    public void Verdict_TiesFavourThePlayerAndPayTheConsolation()
    {
        ValuationVerdict tie = ValuationRules.Verdict(100.0, 100.0);
        Assert.True(tie.Won);
        Assert.Equal(0.0, tie.Gap);
        Assert.Equal(ValuationRules.ConsolationXp, tie.Xp, 6);
    }

    [Fact]
    public void Verdict_WinnerPaysTheGapOverFiveHundred()
    {
        ValuationVerdict verdict = ValuationRules.Verdict(200.0, 100.0);
        Assert.True(verdict.Won);
        Assert.Equal(100.0, verdict.Gap, 6);
        Assert.Equal(100.0 / 500.0, verdict.Xp, 6);
    }

    [Fact]
    public void Verdict_SmallWinStillPaysTheConsolation()
    {
        // A 10-point win would be 0.02 XP; every verdict keeps the floor so a
        // showcase never reads as zero progress.
        ValuationVerdict verdict = ValuationRules.Verdict(100.0, 90.0);
        Assert.True(verdict.Won);
        Assert.Equal(ValuationRules.ConsolationXp, verdict.Xp, 6);
    }

    [Fact]
    public void Verdict_LoserBanksTheFlatConsolation()
    {
        // A landslide and a near miss pay the same tiny consolation: losing
        // must never out-earn the job, and must never read as zero XP.
        ValuationVerdict verdict = ValuationRules.Verdict(90.0, 5000.0);
        Assert.False(verdict.Won);
        Assert.Equal(4910.0, verdict.Gap, 6);
        Assert.Equal(0.1, verdict.Xp, 6);
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
        // Unity's catch-up: a win pays the live rate, a loss and a tie pay
        // nothing, and every fresh uid joins the settled set so the live path
        // cannot pay it again.
        ValuationEntry win = Entry("win", 0.0);
        ValuationEntry loss = Entry("loss", 110.0);
        ValuationEntry tie = Entry("tie", 100.0);
        ValuationEntry own = Entry("me", 100.0);
        ValuationEntry settled = Entry("old", 10.0);

        ValuationOfflineGains gains = ValuationRules.ComputeOfflineGains(
            100.0, ["old"], [win, loss, tie, own, settled], "me");

        Assert.Equal(1, gains.WinCount);
        Assert.Equal(100.0 / 500.0, gains.TotalXp, 6);
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

    [Fact]
    public void Expired_OnlyAfterTwoFullDays()
    {
        var entered = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        DateTime expires = ValuationRules.ComputeExpiryUtc(entered);

        Assert.False(ValuationRules.IsEntryExpired(entered, entered));
        Assert.False(ValuationRules.IsEntryExpired(entered, expires.AddSeconds(-1)));
        Assert.True(ValuationRules.IsEntryExpired(entered, expires));
    }

    [Fact]
    public void OfflineGains_IgnoreTheNewcomersWhoArrivedAfterTheWindowClosed()
    {
        // The two-day cap: a late return pays the rivals who turned up while
        // the entry was still showcasing, never the ones after it.
        var entered = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        DateTime expires = ValuationRules.ComputeExpiryUtc(entered);
        ValuationEntry inside = Entry("inside", 0.0, expires.AddMinutes(-1));
        ValuationEntry after = Entry("after", 0.0, expires.AddMinutes(1));

        ValuationOfflineGains capped = ValuationRules.ComputeOfflineGains(
            100.0, null, [inside, after], "me", expires);

        Assert.Equal(1, capped.WinCount);
        Assert.Equal(100.0 / 500.0, capped.TotalXp, 6);
        // Both settle, so a later live snapshot cannot pay the late one either.
        Assert.Equal(["inside", "after"], capped.NewSeenUids);

        // Without the cap the same list pays both, which is what the window
        // exists to stop.
        Assert.Equal(2, ValuationRules.ComputeOfflineGains(
            100.0, null, [inside, after], "me").WinCount);
    }

    [Fact]
    public void UndatedEntry_StaysInsideTheWindow()
    {
        // A legacy doc without an EnteredAt stamp cannot be dated, and the list
        // is already bounded to the week: it must not be voided by the cap.
        var expires = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(ValuationRules.EnteredWithinWindow(Entry("old", 0.0), expires));
    }

    private static ValuationEntry Entry(
        string uid, double score, DateTime? enteredAt = null) =>
        new(uid, uid, "ks_test", 1, 0.0, score, enteredAt);
}
