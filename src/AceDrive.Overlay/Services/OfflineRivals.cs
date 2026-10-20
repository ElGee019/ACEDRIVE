using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Null-object standings backend (PORTPLAN 60): no board is ever loaded,
/// so <see cref="Refresh"/> answers with the snapshot restored from the
/// driver save — Unity's "fallback standings from saved data" — and
/// <see cref="PushXp"/> drops its payload. The receipt already renders the
/// full mechanism on this; the Firestore board arrives as a new
/// implementation at the composition root, no branches in stable code.
/// </summary>
public sealed class OfflineRivals : IRivalsService
{
    public RivalsResult? LastResult { get; private set; }

    // Never raised offline (there is no async board that could land);
    // declared with explicit empty accessors so screens subscribe exactly
    // like they would against the live backend.
    public event Action<RivalsResult>? ResultUpdated
    {
        add { }
        remove { }
    }

    public void RestoreSnapshot(DriverState? saved)
    {
        // 0 = never saved (Unity's LoadFromPlayerData guard).
        if (saved is null || saved.RivalsRank <= 0 || saved.RivalsTotal <= 0)
        {
            return;
        }

        LastResult = new RivalsResult(
            saved.RivalsRank, saved.RivalsTotal, 0, saved.RivalsRank, false);
    }

    public RivalsResult? Refresh(double playerXp) =>
        RivalsStandings.Refresh(playerXp, otherXp: null, boardLoaded: false, last: LastResult);

    public void PushXp(double xp, string displayName)
    {
    }
}
