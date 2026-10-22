// Best-single-shift board seam (PORTPLAN item 12, Unity ShiftStandingsService):
// the shift-ended page only talks to this interface, so the offline
// null-object and the live Firestore board swap at the composition root.
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// World standings for the player's best single shift. Small on purpose
/// (ISP): rank reads, the live-update event, snapshot restore and a refresh
/// against the cached board — how a board is fetched stays inside the
/// implementation. The write of the player's own record lives on
/// <see cref="IPlayerTelemetry.PushShiftEarnings"/>: one owner per doc field.
/// </summary>
public interface IShiftStandings
{
    /// <summary>Latest known standings, or null when nothing is known yet.</summary>
    ShiftStandingsResult? LastResult { get; }

    /// <summary>
    /// Raised when a fresh board result lands — possibly off the UI thread,
    /// so subscribers only set bindable properties in their handler.
    /// </summary>
    event Action<ShiftStandingsResult>? ResultUpdated;

    /// <summary>
    /// Seeds the fallback snapshot from the driver save (Unity
    /// LoadFromPlayerData): rank/total 0 means never saved, nothing to seed.
    /// </summary>
    void RestoreSnapshot(DriverState? saved);

    /// <summary>Starts the boot-time board load without blocking startup.</summary>
    void Start();

    /// <summary>
    /// Recomputes the rank for the given record against the cached board.
    /// While the board is missing this returns the restored snapshot, and
    /// null when nothing is known yet. Never throws.
    /// </summary>
    ShiftStandingsResult? Refresh(double earnings, long firstEarnedTicks);
}
