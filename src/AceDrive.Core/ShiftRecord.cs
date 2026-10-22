// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 12 and LATESTDRIVEBS2 PlayerData
// highestShiftEarnings + highestShiftEarningsFirstEarnedAt +
// longestShiftJobCount + totalShiftsCompleted.
namespace AceDrive.Core;

/// <summary>
/// The driver's lifetime shift record, persisted across sessions: the best
/// single shift, the longest shift, how many shifts were completed and the
/// last observed world rank. The first-earned stamp is written only when the
/// earnings record improves, so a tie on the world board keeps the earlier
/// owner ahead.
/// </summary>
public sealed class ShiftRecord
{
    public ShiftRecord(DriverState? saved)
    {
        BestEarnings = Math.Max(0.0, saved?.BestShiftEarnings ?? 0.0);
        BestFirstEarnedTicks = Math.Max(0L, saved?.BestShiftFirstEarnedTicks ?? 0L);
        Rank = Math.Max(0, saved?.ShiftRank ?? 0);
        Total = Math.Max(0, saved?.ShiftTotal ?? 0);
        ShiftsCompleted = Math.Max(0, saved?.ShiftsCompleted ?? 0);
        LongestJobs = Math.Max(0, saved?.LongestShiftJobs ?? 0);
    }

    public double BestEarnings { get; private set; }
    public long BestFirstEarnedTicks { get; private set; }

    /// <summary>Last observed world rank; 0 = never seen.</summary>
    public int Rank { get; private set; }

    /// <summary>Last observed competitor count; 0 = never seen.</summary>
    public int Total { get; private set; }

    /// <summary>Shifts finished, the current one excluded (Unity totalShiftsCompleted).</summary>
    public int ShiftsCompleted { get; private set; }

    /// <summary>Most jobs completed in one shift (Unity longestShiftJobCount).</summary>
    public int LongestJobs { get; private set; }

    /// <summary>
    /// Banks a finished shift: the lifetime counters always grow, while the
    /// earnings record only improves on a strictly higher shift. An equal
    /// shift keeps its earlier stamp, exactly like the world board.
    /// </summary>
    public bool Complete(int jobs, double earnings, long firstEarnedTicks)
    {
        ShiftsCompleted++;
        if (jobs > LongestJobs)
        {
            LongestJobs = jobs;
        }
        if (earnings <= BestEarnings || firstEarnedTicks <= 0)
        {
            return false;
        }
        BestEarnings = earnings;
        BestFirstEarnedTicks = firstEarnedTicks;
        return true;
    }

    /// <summary>Restores the last known board snapshot from the driver save.</summary>
    public void RestoreStandings(int rank, int total)
    {
        if (rank <= 0 || total <= 0)
        {
            return;
        }
        Rank = rank;
        Total = total;
    }
}
