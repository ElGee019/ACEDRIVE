// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 49 and
// LATESTDRIVEBS2 RoadTestStandingsService.CompareResults.
namespace AceDrive.Core;

/// <summary>Sorting rules shared by the acceleration and brake boards.</summary>
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
}
