using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Scriptable best-shift board: a fixed or restored result, a board
/// that can be made ready, and a refresh log for the shift-ended page.</summary>
public sealed class FakeShiftStandings : IShiftStandings
{
    public ShiftStandingsResult? LastResult { get; set; }
    public List<(double Earnings, long Ticks)> Refreshes { get; } = [];
    public ShiftStandingsResult? NextResult { get; set; }
    public int Starts { get; private set; }

    public event Action<ShiftStandingsResult>? ResultUpdated;

    public void RestoreSnapshot(DriverState? saved)
    {
        if (saved is null || saved.ShiftRank <= 0 || saved.ShiftTotal <= 0)
        {
            return;
        }
        LastResult = new ShiftStandingsResult(
            saved.ShiftRank, saved.ShiftTotal, 0, saved.ShiftRank, false);
    }

    public void Start() => Starts++;

    public ShiftStandingsResult? Refresh(double earnings, long firstEarnedTicks)
    {
        Refreshes.Add((earnings, firstEarnedTicks));
        if (NextResult is { } next)
        {
            LastResult = next;
        }
        return LastResult;
    }

    /// <summary>Lands a board result the way the live backend does.</summary>
    public void Publish(ShiftStandingsResult result)
    {
        LastResult = result;
        ResultUpdated?.Invoke(result);
    }
}
