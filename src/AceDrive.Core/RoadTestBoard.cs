// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 49 and
// LATESTDRIVEBS2 RoadTestStandingsService.CompareResults.
namespace AceDrive.Core;

/// <summary>
/// Sorting rules shared by the acceleration and brake boards, and by the
/// all-cars record book built from them.
/// </summary>
public static class RoadTestBoard
{
    /// <summary>Lower value wins; an identical value keeps the earlier stamp ahead.</summary>
    public static int Compare(RoadTestEntry a, RoadTestEntry b)
    {
        int byValue = a.Value.CompareTo(b.Value);
        if (byValue != 0)
        {
            return byValue;
        }
        int byStamp = string.CompareOrdinal(a.LastUpdatedAt, b.LastUpdatedAt);
        return byStamp != 0 ? byStamp : string.CompareOrdinal(a.Uid, b.Uid);
    }

    /// <summary>Sorted best-first, capped at <paramref name="count"/> rows.</summary>
    public static List<RoadTestEntry> Top(IReadOnlyList<RoadTestEntry> entries, int count)
    {
        List<RoadTestEntry> rows = [.. entries];
        rows.Sort(Compare);
        if (count >= 0 && rows.Count > count)
        {
            rows.RemoveRange(count, rows.Count - count);
        }
        return rows;
    }

    /// <summary>
    /// The rows the page paints after a test (Unity's PopulateStandings): the
    /// best <paramref name="topCount"/> runs, then — when the player's run is
    /// not already among them — the player's own row, so a slow first run is
    /// never invisible. The own row keeps its true position on the whole
    /// board: Unity printed <c>top10.Count + 1</c>, which reads as a wrong rank
    /// once more than one board exists. An unknown uid simply yields the top
    /// slice, which is also the offline reading.
    /// </summary>
    public static List<RoadTestStanding> Standings(
        IReadOnlyList<RoadTestEntry> entries, string? ownUid, int topCount)
    {
        List<RoadTestEntry> sorted = Top(entries, entries.Count);
        var rows = new List<RoadTestStanding>();
        bool ownShown = false;
        int shown = topCount < 0 ? sorted.Count : Math.Min(topCount, sorted.Count);
        for (int i = 0; i < shown; i++)
        {
            RoadTestEntry entry = sorted[i];
            bool isOwn = IsOwn(entry, ownUid);
            ownShown |= isOwn;
            rows.Add(new RoadTestStanding(i + 1, entry.PlayerName, entry.Value, isOwn));
        }

        if (ownShown || string.IsNullOrEmpty(ownUid))
        {
            return rows;
        }

        for (int i = shown; i < sorted.Count; i++)
        {
            if (!IsOwn(sorted[i], ownUid))
            {
                continue;
            }
            rows.Add(new RoadTestStanding(i + 1, sorted[i].PlayerName, sorted[i].Value, true));
            break;
        }
        return rows;
    }

    /// <summary>
    /// The all-cars record book (Unity LoadGlobalBestPerCar): every car that has
    /// a recorded run, one row per car holding that car's best value, best
    /// first. Ties keep the run that was set first. An entry without a car key
    /// cannot be placed on a per-car board, so it is dropped.
    /// </summary>
    public static List<RoadTestEntry> BestPerCar(IReadOnlyList<RoadTestEntry> entries)
    {
        var best = new Dictionary<string, RoadTestEntry>(StringComparer.Ordinal);
        foreach (RoadTestEntry entry in entries)
        {
            if (string.IsNullOrEmpty(entry.CarKey))
            {
                continue;
            }
            if (!best.TryGetValue(entry.CarKey, out RoadTestEntry current)
                || Compare(entry, current) < 0)
            {
                best[entry.CarKey] = entry;
            }
        }

        List<RoadTestEntry> rows = [.. best.Values];
        rows.Sort(Compare);
        return rows;
    }

    /// <summary>
    /// Merges the just-set run into a loaded board when the server round-trip
    /// has not stored it yet (Unity KeepLocalRowVisible): the boards are
    /// best-first, so the local row slots in where its value belongs and ties
    /// stay behind the stored runs that were set first. A stored row for the
    /// same player and car is replaced when the local run beats it, so an
    /// improvement shows at once instead of waiting for the push to land; a
    /// slower run changes nothing.
    /// </summary>
    public static List<RoadTestEntry> KeepLocalRow(
        IReadOnlyList<RoadTestEntry> entries, RoadTestEntry local) =>
        MergeLocal(entries, local, row =>
            string.Equals(row.Uid, local.Uid, StringComparison.Ordinal)
            && string.Equals(row.CarKey, local.CarKey, StringComparison.Ordinal));

    /// <summary>
    /// Keeps the just-set run on the all-cars board when the server round-trip
    /// has not stored it yet: the loaded board already holds this car's best, so
    /// a slower local run changes nothing while a faster one replaces the row
    /// (the stored best is beaten, not duplicated). A car the board has never
    /// seen slots in where its value belongs, like <see cref="KeepLocalRow"/>.
    /// </summary>
    public static List<RoadTestEntry> KeepLocalCar(
        IReadOnlyList<RoadTestEntry> entries, RoadTestEntry local) =>
        MergeLocal(entries, local, row =>
            string.Equals(row.CarKey, local.CarKey, StringComparison.Ordinal));

    // One run into one best-first board: <paramref name="sameSlot"/> names the
    // row the run would occupy (the player's row on a car's board, the car's row
    // on the record book). A slot the board already fills keeps its stored value
    // unless the run beats it; ties stay with the run that was set first.
    private static List<RoadTestEntry> MergeLocal(
        IReadOnlyList<RoadTestEntry> entries, RoadTestEntry local,
        Func<RoadTestEntry, bool> sameSlot)
    {
        List<RoadTestEntry> rows = Top(entries, entries.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            if (!sameSlot(rows[i]))
            {
                continue;
            }
            if (rows[i].Value <= local.Value)
            {
                return rows;
            }
            rows.RemoveAt(i);
            break;
        }

        rows.Insert(InsertionPoint(rows, local.Value), local);
        return rows;
    }

    // Where a value belongs on a best-first board: behind every stored run that
    // is at least as good, so a tie stays with the run that was set first.
    private static int InsertionPoint(List<RoadTestEntry> rows, double value)
    {
        int at = 0;
        while (at < rows.Count && rows[at].Value <= value)
        {
            at++;
        }
        return at;
    }

    private static bool IsOwn(RoadTestEntry entry, string? ownUid) =>
        !string.IsNullOrEmpty(ownUid)
        && string.Equals(entry.Uid, ownUid, StringComparison.Ordinal);
}
