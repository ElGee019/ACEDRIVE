// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 60 (Unity
// RivalsService.CalculatePlayerRank + RefreshStandings).
namespace AceDrive.Core;

/// <summary>
/// XP world-standings math; the rival board itself is somebody else's job
/// (the services layer owns everything that smells of a backend). Rank
/// counts only players with STRICTLY more XP, so ties stay behind the
/// player who reached the value first, like the Unity board. The change
/// pins to the last observed rank, never to what this session happens to
/// remember.
/// </summary>
public static class RivalsStandings
{
    /// <summary>Unity CalculatePlayerRank: 1 + everyone strictly ahead.</summary>
    public static int Rank(double playerXp, IReadOnlyList<double> otherXp)
    {
        if (otherXp is null || otherXp.Count == 0)
        {
            return 1;
        }

        int ahead = 0;
        for (int i = 0; i < otherXp.Count; i++)
        {
            if (otherXp[i] > playerXp)
            {
                ahead++;
            }
        }

        return ahead + 1;
    }

    /// <summary>
    /// Unity RefreshStandings: a fresh result against the loaded board, or
    /// the previous result untouched while the board is missing (its
    /// "fallback standings from saved data" path — no change, no event).
    /// A board seen for the first time has no earlier rank to compare with,
    /// so the board size itself becomes the "before", like Unity does.
    /// </summary>
    public static RivalsResult? Refresh(
        double playerXp,
        IReadOnlyList<double>? otherXp,
        bool boardLoaded,
        RivalsResult? last)
    {
        if (!boardLoaded)
        {
            return last;
        }

        int count = otherXp?.Count ?? 0;
        int current = Rank(playerXp, otherXp ?? []);
        int total = count + 1;
        int previous = last?.CurrentRank ?? total;
        return new RivalsResult(current, total, previous - current, previous, last is null);
    }
}
