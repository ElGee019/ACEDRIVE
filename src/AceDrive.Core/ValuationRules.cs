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

    /// <summary>Unity's score-gap divisor for the loser's consolation XP.</summary>
    public const double LossXpDivisor = 1000.0;

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
    /// Picks this week's venue from the placeholder map. TODO(Step 1): replace
    /// with Unity's live Overpass town/street discovery once the real Eifel
    /// map can resolve addresses; the two placeholder streets are the only
    /// venues that exist until then. The draw is uniform over every house on
    /// every street, so a street with more doors is proportionally likelier.
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
    /// condition as percentage points (fresh engine = 100, fully broken = 0).
    /// WHY horsepower / 10 instead of Unity's maxRpm / 250: the EVO car table
    /// carries no RPM column, and horsepower is its power figure at the same
    /// point scale (roughly 20-65 against RPM's 24-36). The rest of the
    /// formula is Unity's, term for term.
    /// </summary>
    public static double Score(CarSpec? car, int jobsWithCar, double engineBrokenPercent)
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
            + (1.0 - broken) * 100.0;
    }

    /// <summary>
    /// Unity ShowVerdict: ties favour the player, so every draw has a verdict.
    /// A winner's XP is the gap / 500; a loser's consolation is gap / 1000.
    /// </summary>
    public static ValuationVerdict Verdict(double ownScore, double competitorScore)
    {
        bool won = ownScore >= competitorScore;
        double gap = won ? ownScore - competitorScore : competitorScore - ownScore;
        double xp = won ? gap / WinXpDivisor : gap / LossXpDivisor;
        return new ValuationVerdict(won, gap, xp);
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
    /// are ignored. Every fresh uid joins the settled set so the next live
    /// snapshot cannot pay it again.
    /// </summary>
    public static ValuationOfflineGains ComputeOfflineGains(
        double ownScore,
        IReadOnlyCollection<string>? seenUids,
        IReadOnlyList<ValuationEntry>? freshEntries,
        string? ownUid)
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
            if (entry.Score < ownScore)
            {
                totalXp += (ownScore - entry.Score) / WinXpDivisor;
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
}
