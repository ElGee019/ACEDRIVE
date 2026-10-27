// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 49 and
// LATESTDRIVEBS2 ValuationService / ValuationScoreService /
// ValuationEntriesService / ValuationOfflineService.
namespace AceDrive.Core;

/// <summary>
/// Weekly vehicle-valuation showcase rules: Monday's 8am-8pm entry window,
/// the car score the jury compares, the competitor draw and the XP a verdict
/// pays. The list empties on the Sunday-to-Monday midnight game tick, entries
/// expire after 48 real hours, and the permanent house entry always answers
/// with a freshly drawn car. Pure helpers only; the flow owns the state.
/// </summary>
public static class ValuationRules
{
    public const string HouseEntryName = "John and Jane Doe";
    public const string HouseEntryDocId = "house-entry";
    public const int EntryExpiryHours = 48;

    /// <summary>Unity's score-gap divisor for the winner's XP payout.</summary>
    public const double WinXpDivisor = 500.0;

    /// <summary>
    /// The least any verdict pays. A loss banks this consolation instead of
    /// a gap-proportional payout: a lost showcase must never read as "no
    /// progress", and it must never be able to out-earn the job it took to
    /// get there (a 5000-point gap used to pay five XP, sixty short rides).
    /// </summary>
    public const double ConsolationXp = 0.1;

    /// <summary>Most points a car can earn from its stretch leaderboard rank.</summary>
    public const double LeaderboardMaxPoints = 150.0;

    /// <summary>How many of the player's best stretches count toward the ranking.</summary>
    public const int LeaderboardRanksCounted = 3;

    // The ranking curve: a linear percentile is squared, so only a near-perfect
    // ranking is worth the full LeaderboardMaxPoints.
    private const double LeaderboardCurveExponent = 2.0;

    // Monday, the valuation day. Unity dayOfWeek is 1 = Sunday, 2 = Monday.
    public const int MondayDayOfWeek = 2;

    // The Monday the week key counts from: 2024-01-01 was a Monday, so the
    // arithmetic below never needs a culture calendar.
    private static readonly DateTime MondayEpoch =
        new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>True exactly on the Sunday-to-Monday midnight game tick.</summary>
    public static bool IsWipeDue(int dayOfWeek, int hour, int minute) =>
        dayOfWeek == MondayDayOfWeek && hour == 0 && minute == 0;

    /// <summary>Unity IsValuationWindow: Monday 8am-8pm.</summary>
    public static bool IsWindow(int dayOfWeek, int hour) =>
        dayOfWeek == MondayDayOfWeek && hour >= 8 && hour < 20;

    /// <summary>
    /// Unity's 6am preload slot: the venue is drawn before the job-centre
    /// button reveals it at 8am, and a late launch retries inside the same
    /// window instead of losing the week.
    /// </summary>
    public static bool IsPreloadWindow(int dayOfWeek, int hour) =>
        dayOfWeek == MondayDayOfWeek && hour >= 6 && hour < 20;

    /// <summary>
    /// The week a moment belongs to, counted in Mondays since 2024-01-01.
    /// A week changes exactly on the Sunday-to-Monday midnight crossing, so a
    /// persisted key catches up after the app was closed over the tick.
    /// </summary>
    public static int WeekKey(DateTime moment) =>
        (int)Math.Floor((moment.Date - MondayEpoch).TotalDays / 7.0);

    /// <summary>
    /// True when a Monday midnight boundary is newer than the last reset —
    /// the app was closed over the tick, or the wipe never completed.
    /// </summary>
    public static bool IsResetOverdue(
        int lastResetGameDay, int currentGameDay, int currentDayOfWeek)
    {
        if (currentGameDay <= 0)
        {
            return false;
        }
        int daysSinceMonday = ((currentDayOfWeek - MondayDayOfWeek) % 7 + 7) % 7;
        int mostRecentMonday = currentGameDay - daysSinceMonday;
        return mostRecentMonday > 0 && lastResetGameDay < mostRecentMonday;
    }

    /// <summary>
    /// Picks this week's venue from the map the caller can address. TODO(Step 1):
    /// replace with Unity's live Overpass town/street discovery once the real
    /// Eifel map can resolve addresses; until then the venue is a house on the
    /// operating zone's streets (the caller passes <c>zone.ZoneMap</c>, so the
    /// showcase never lands off-zone). The draw is uniform over every house on
    /// every street of that map, so a street with more doors is proportionally
    /// likelier.
    /// </summary>
    public static ValuationVenue? PickVenue(AddressMap map, Func<int, int> nextInt)
    {
        if (map.Streets is not { Count: > 0 } || nextInt is null)
        {
            return null;
        }

        int total = 0;
        foreach (AddressStreet street in map.Streets)
        {
            total += Math.Max(0, HouseNumberPlan.Count(street, map));
        }
        if (total <= 0)
        {
            return null;
        }

        int pick = Math.Clamp(nextInt(total), 0, total - 1);
        foreach (AddressStreet street in map.Streets)
        {
            int count = Math.Max(0, HouseNumberPlan.Count(street, map));
            if (pick < count)
            {
                return new ValuationVenue(street.Id, street.Name, pick + 1);
            }
            pick -= count;
        }
        return null;
    }

    /// <summary>
    /// Unity ValuationScoreService.ComputeScore: car class + top speed/2 +
    /// engine character + price/1000 + completed jobs with the car + engine
    /// condition as percentage points (fresh engine = 100, fully broken = 0) +
    /// the stretch leaderboard term (<see cref="LeaderboardPoints"/>).
    /// WHY horsepower / 10 instead of Unity's maxRpm / 250: the EVO car table
    /// carries no RPM column, and horsepower is its power figure at the same
    /// point scale (roughly 20-65 against RPM's 24-36). The rest of the
    /// formula is Unity's, term for term.
    /// </summary>
    public static double Score(
        CarSpec? car, int jobsWithCar, double engineBrokenPercent,
        double leaderboardRanking)
    {
        if (car is null)
        {
            return 0.0;
        }

        double broken = Math.Clamp(engineBrokenPercent, 0.0, 1.0);
        return car.Class
            + car.TopSpeedKmh / 2.0
            + car.Horsepower / 10.0
            + car.Price / 1000.0
            + Math.Max(0, jobsWithCar)
            + (1.0 - broken) * 100.0
            + LeaderboardPoints(leaderboardRanking);
    }

    /// <summary>
    /// A car model's leaderboard ranking from its stretch standings: the
    /// player's percentile on every stretch, the best
    /// <see cref="LeaderboardRanksCounted"/> averaged, 0 when nothing is
    /// ranked. Percentile = (field size - rank) / field size, so first of ten
    /// reads 0.9 and a one-entrant field reads 0. Rows with no field (or a
    /// non-finite count) are skipped, so a broken board degrades to "unranked"
    /// instead of poisoning the score with NaN.
    /// </summary>
    public static double LeaderboardRanking(IReadOnlyList<StretchRank>? ranks)
    {
        if (ranks is not { Count: > 0 })
        {
            return 0.0;
        }

        // Cold path (one call per weekly entry), so the list allocation is the
        // honest way to sort; the 10 Hz poll never reaches this.
        List<double> percentiles = new(ranks.Count);
        foreach (StretchRank rank in ranks)
        {
            if (rank is null || rank.TotalSameCarPlayers <= 0)
            {
                continue;
            }

            double percentile =
                (double)(rank.TotalSameCarPlayers - rank.SameCarRank) / rank.TotalSameCarPlayers;
            percentiles.Add(Math.Clamp(percentile, 0.0, 1.0));
        }

        if (percentiles.Count == 0)
        {
            return 0.0;
        }

        percentiles.Sort(static (a, b) => b.CompareTo(a));
        int counted = Math.Min(LeaderboardRanksCounted, percentiles.Count);
        double sum = 0.0;
        for (int i = 0; i < counted; i++)
        {
            sum += percentiles[i];
        }
        return sum / counted;
    }

    /// <summary>
    /// What a ranking is worth: the percentile squared, scaled to
    /// <see cref="LeaderboardMaxPoints"/>. Clamped, so a hand-written ranking
    /// can never out-earn a perfect one.
    /// </summary>
    public static double LeaderboardPoints(double ranking)
    {
        double clamped = Math.Clamp(ranking, 0.0, 1.0);
        return Math.Pow(clamped, LeaderboardCurveExponent) * LeaderboardMaxPoints;
    }

    /// <summary>
    /// Unity ShowVerdict, with one change: ties still favour the player, and
    /// every verdict pays at least <see cref="ConsolationXp"/>. A winner
    /// banks the gap / 500; a loser banks the flat consolation (never a share
    /// of the gap, so losing badly cannot pay better than losing narrowly).
    /// </summary>
    public static ValuationVerdict Verdict(double ownScore, double competitorScore)
    {
        bool won = ownScore >= competitorScore;
        double gap = won ? ownScore - competitorScore : competitorScore - ownScore;
        double xp = won ? gap / WinXpDivisor : 0.0;
        return new ValuationVerdict(won, gap, Math.Max(xp, ConsolationXp));
    }

    /// <summary>Picks the house entry's car; null when the pool is empty.</summary>
    public static string? PickRandomCarId(IReadOnlyList<string> carIds, Func<int, int> nextInt)
    {
        if (carIds is null || carIds.Count == 0 || nextInt is null)
        {
            return null;
        }
        int index = Math.Clamp(nextInt(carIds.Count), 0, carIds.Count - 1);
        return carIds[index];
    }

    /// <summary>
    /// Unity SelectCompetitor: one random entry that is not the player
    /// themselves and not the remote house doc (the house entry is local, so
    /// a legacy cloud copy must not shadow the fresh local draw). Null when
    /// nobody else is listed.
    /// </summary>
    public static ValuationEntry? SelectCompetitor(
        IReadOnlyList<ValuationEntry>? entries, string? ownUid, Func<int, int> nextInt)
    {
        if (entries is null || nextInt is null)
        {
            return null;
        }

        List<ValuationEntry> others = [];
        foreach (ValuationEntry entry in entries)
        {
            if (entry is null || string.IsNullOrEmpty(entry.Uid))
            {
                continue;
            }
            if (string.Equals(entry.Uid, HouseEntryDocId, StringComparison.Ordinal))
            {
                continue;
            }
            if (!string.IsNullOrEmpty(ownUid)
                && string.Equals(entry.Uid, ownUid, StringComparison.Ordinal))
            {
                continue;
            }
            others.Add(entry);
        }

        if (others.Count == 0)
        {
            return null;
        }
        int index = Math.Clamp(nextInt(others.Count), 0, others.Count - 1);
        return others[index];
    }

    /// <summary>
    /// Unity FindUnseenEntries: entries not settled before and not the
    /// caller's own (a self entry can never win or lose against itself).
    /// </summary>
    public static IReadOnlyList<ValuationEntry> FindUnseen(
        IReadOnlyCollection<string>? seenUids,
        IReadOnlyList<ValuationEntry>? entries,
        string? ownUid)
    {
        List<ValuationEntry> fresh = [];
        if (entries is null)
        {
            return fresh;
        }

        foreach (ValuationEntry entry in entries)
        {
            if (entry is null || string.IsNullOrEmpty(entry.Uid))
            {
                continue;
            }
            if (!string.IsNullOrEmpty(ownUid)
                && string.Equals(entry.Uid, ownUid, StringComparison.Ordinal))
            {
                continue;
            }
            if (seenUids is not null && seenUids.Contains(entry.Uid))
            {
                continue;
            }
            fresh.Add(entry);
        }
        return fresh;
    }

    /// <summary>
    /// Unity ComputeOfflineGains: wins only (a loss while away pays nothing,
    /// the live path pays the consolation). Ties and higher newcomer scores
    /// are ignored. A win pays exactly what the live path pays
    /// (<see cref="Verdict"/>: gap / 500, floored at the consolation). Every
    /// fresh uid joins the settled set so the next live snapshot cannot pay it
    /// again.
    /// An <paramref name="entryExpiresUtc"/> caps the catch-up at the entry's
    /// two-day window: a newcomer who entered after it pays nothing, however
    /// long the overlay was closed.
    /// </summary>
    public static ValuationOfflineGains ComputeOfflineGains(
        double ownScore,
        IReadOnlyCollection<string>? seenUids,
        IReadOnlyList<ValuationEntry>? freshEntries,
        string? ownUid,
        DateTime? entryExpiresUtc = null)
    {
        double totalXp = 0.0;
        int wins = 0;
        List<string> settled = [];
        if (freshEntries is null)
        {
            return new ValuationOfflineGains(0.0, 0, settled);
        }

        foreach (ValuationEntry entry in freshEntries)
        {
            if (entry is null || string.IsNullOrEmpty(entry.Uid))
            {
                continue;
            }
            if (!settled.Contains(entry.Uid, StringComparer.Ordinal))
            {
                settled.Add(entry.Uid);
            }
            if (!string.IsNullOrEmpty(ownUid)
                && string.Equals(entry.Uid, ownUid, StringComparison.Ordinal))
            {
                continue;
            }
            if (seenUids is not null && seenUids.Contains(entry.Uid))
            {
                continue;
            }
            if (entryExpiresUtc is { } expiry
                && !EnteredWithinWindow(entry, expiry))
            {
                continue;
            }
            if (entry.Score < ownScore)
            {
                totalXp += Verdict(ownScore, entry.Score).Xp;
                wins++;
            }
        }
        return new ValuationOfflineGains(totalXp, wins, settled);
    }

    /// <summary>The TTL stamp written with a new entry (entry time + 48 h).</summary>
    public static DateTime ComputeExpiryUtc(DateTime enteredUtc)
    {
        DateTime utc = enteredUtc.Kind == DateTimeKind.Utc
            ? enteredUtc
            : enteredUtc.ToUniversalTime();
        return utc.AddHours(EntryExpiryHours);
    }

    /// <summary>
    /// True once the entry has outlived <see cref="EntryExpiryHours"/> real
    /// hours. The cap exists so an entry cannot keep earning comparisons for
    /// a whole week while its driver is not playing: two days of absence are
    /// still banked, nothing after them.
    /// </summary>
    public static bool IsEntryExpired(DateTime enteredUtc, DateTime nowUtc)
    {
        DateTime now = nowUtc.Kind == DateTimeKind.Utc ? nowUtc : nowUtc.ToUniversalTime();
        return now >= ComputeExpiryUtc(enteredUtc);
    }

    /// <summary>
    /// True when a rival turned up inside the entry's own earning window. A
    /// newcomer who entered after the window closed must not pay a comparison
    /// the capped entry never covered, or the two-day cap would be a fiction
    /// for anyone who returns late. A doc without a stamp stays eligible: the
    /// list is already bounded to the week, and a missing stamp must not void
    /// a rival the client cannot date.
    /// </summary>
    public static bool EnteredWithinWindow(ValuationEntry entry, DateTime entryExpiresUtc)
    {
        if (entry.EnteredAt is not { } entered)
        {
            return true;
        }

        DateTime utc = entered.Kind == DateTimeKind.Utc ? entered : entered.ToUniversalTime();
        return utc <= entryExpiresUtc;
    }
}
