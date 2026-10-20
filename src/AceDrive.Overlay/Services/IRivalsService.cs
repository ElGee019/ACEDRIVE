// Standings backend seam (PORTPLAN item 60, Unity RivalsService): the
// screens only ever talk to this interface, so the offline null-object and
// a future Firestore client are swappable at the composition root.
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// XP world standings against the rival board. Small on purpose (ISP):
/// rank reads, the live-update event, snapshot restore and the XP push —
/// how a board is fetched or stored stays inside the implementation.
/// </summary>
public interface IRivalsService
{
    /// <summary>Latest known standings, or null when nothing is known yet.</summary>
    RivalsResult? LastResult { get; }

    /// <summary>
    /// Raised when a fresh board result lands — possibly off the UI
    /// thread, so subscribers only set bindable properties in their handler.
    /// </summary>
    event Action<RivalsResult>? ResultUpdated;

    /// <summary>
    /// Seeds the fallback snapshot from the driver save (Unity
    /// LoadFromPlayerData): rank/total 0 means never saved, nothing to seed.
    /// </summary>
    void RestoreSnapshot(DriverState? saved);

    /// <summary>
    /// Recomputes standings for the given XP against the cached board.
    /// While the board is missing this returns the restored snapshot, and
    /// null when nothing is known yet. Never throws.
    /// </summary>
    RivalsResult? Refresh(double playerXp);

    /// <summary>
    /// Fire-and-forget XP write for the shared board (Unity PushXp). XP is
    /// monotonic, so the last push always wins; never throws.
    /// </summary>
    void PushXp(double xp, string displayName);
}
