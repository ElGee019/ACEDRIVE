// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 12 and
// LATESTDRIVEBS2 ShiftStandingsService.CalculatePlayerRank/Refresh.
namespace AceDrive.Core;

/// <summary>
/// Best-single-shift world standings. The board ranks by earnings, but two
/// equal records are split by who reached the value first: an earlier
/// first-earned stamp keeps the higher place. The board itself is somebody
/// else's job; this file is only the pure rank math.
/// </summary>
public static class ShiftStandings
{
    /// <summary>1 + everyone who beats the player's record (value or tie-break).</summary>
    public static int Rank(
        double earnings, long firstEarnedTicks, IReadOnlyList<ShiftBoardEntry> otherEntries)
    {
        if (otherEntries is null || otherEntries.Count == 0)
        {
            return 1;
        }

        int ahead = 0;
        for (int i = 0; i < otherEntries.Count; i++)
        {
            ShiftBoardEntry other = otherEntries[i];
            if (other.Earnings > earnings
                || (other.Earnings == earnings && other.FirstEarnedTicks < firstEarnedTicks))
            {
                ahead++;
            }
        }
        return ahead + 1;
    }

    /// <summary>
    /// A fresh result against the loaded board, or the previous result while
    /// the board is missing. The first board has no earlier rank to compare
    /// with, so its size becomes the "before" (Unity parity).
    /// </summary>
    public static ShiftStandingsResult? Refresh(
        double earnings,
        long firstEarnedTicks,
        IReadOnlyList<ShiftBoardEntry>? otherEntries,
        bool boardLoaded,
        ShiftStandingsResult? last)
    {
        if (!boardLoaded)
        {
            return last;
        }

        int count = otherEntries?.Count ?? 0;
        int current = Rank(earnings, firstEarnedTicks, otherEntries ?? []);
        int total = count + 1;
        int previous = last?.CurrentRank ?? total;
        return new ShiftStandingsResult(
            current, total, previous - current, previous, last is null);
    }
}
