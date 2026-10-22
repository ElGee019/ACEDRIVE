// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 12 and LATESTDRIVEBS2 PlayerData
// highestShiftEarnings + highestShiftEarningsFirstEarnedAt.
namespace AceDrive.Core;

/// <summary>
/// The driver's best single-shift record, persisted across sessions. The
/// first-earned stamp is written only when the record improves, so a tie on
/// the world board keeps the earlier owner ahead.
/// </summary>
public sealed class ShiftRecord
{
    public ShiftRecord(DriverState? saved)
    {
        BestEarnings = Math.Max(0.0, saved?.BestShiftEarnings ?? 0.0);
        BestFirstEarnedTicks = Math.Max(0L, saved?.BestShiftFirstEarnedTicks ?? 0L);
        Rank = Math.Max(0, saved?.ShiftRank ?? 0);
        Total = Math.Max(0, saved?.ShiftTotal ?? 0);
    }

    public double BestEarnings { get; private set; }
    public long BestFirstEarnedTicks { get; private set; }

    /// <summary>Last observed world rank; 0 = never seen.</summary>
    public int Rank { get; private set; }

    /// <summary>Last observed competitor count; 0 = never seen.</summary>
    public int Total { get; private set; }

    /// <summary>
    /// Banks a finished shift. True only when the record improved; an equal
    /// shift keeps its earlier stamp, exactly like the world board.
    /// </summary>
    public bool Record(double earnings, long firstEarnedTicks)
    {
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
