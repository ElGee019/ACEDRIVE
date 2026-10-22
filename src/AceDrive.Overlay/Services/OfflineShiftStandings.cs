using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Null-object shift-standings backend: no board is ever loaded, so
/// <see cref="Refresh"/> answers with the snapshot restored from the driver
/// save, exactly like <see cref="OfflineRivals"/> for XP. The receipt and the
/// shift-ended page render the full mechanism on this; the Firestore board
/// arrives as a new implementation at the composition root.
/// </summary>
public sealed class OfflineShiftStandings : IShiftStandings
{
    public ShiftStandingsResult? LastResult { get; private set; }

    // Never raised offline (there is no async board that could land);
    // declared with explicit empty accessors so screens subscribe exactly
    // like they would against the live backend.
    public event Action<ShiftStandingsResult>? ResultUpdated
    {
        add { }
        remove { }
    }

    public void RestoreSnapshot(DriverState? saved)
    {
        // 0 = never saved (Unity's LoadFromPlayerData guard).
        if (saved is null || saved.ShiftRank <= 0 || saved.ShiftTotal <= 0)
        {
            return;
        }

        LastResult = new ShiftStandingsResult(
            saved.ShiftRank, saved.ShiftTotal, 0, saved.ShiftRank, false);
    }

    public void Start()
    {
        // Nothing to load: the restored snapshot is the whole board.
    }

    public ShiftStandingsResult? Refresh(double earnings, long firstEarnedTicks) =>
        ShiftStandings.Refresh(
            earnings, firstEarnedTicks, otherEntries: null,
            boardLoaded: false, last: LastResult);
}
