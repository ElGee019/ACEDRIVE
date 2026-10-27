// Operating-zone board seam (PORTPLAN item 58, Unity ZoneStandingsService):
// the expansion page only talks to this interface, so the offline null-object
// and the live Firestore board swap at the composition root.
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// World standings for the longest operating zone. Small on purpose (ISP):
/// rank reads, the live-update event, whether a board backs this build at all
/// and a refresh against the cached board — how a board is fetched stays
/// inside the implementation. The write of the player's own record lives on
/// <see cref="IPlayerTelemetry.PushZoneKm"/>: one owner per doc field.
/// </summary>
public interface IZoneStandings
{
    /// <summary>Latest known standings, or null when nothing is known yet.</summary>
    ZoneStandingsResult? LastResult { get; }

    /// <summary>
    /// Raised when a fresh board result lands — possibly off the UI thread,
    /// so subscribers only set bindable properties in their handler.
    /// </summary>
    event Action<ZoneStandingsResult>? ResultUpdated;

    /// <summary>
    /// False for the offline null-object: the expansion page then hides the
    /// block, exactly like Unity's build with rivals compiled out.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>Starts the boot-time board load without blocking startup.</summary>
    void Start();

    /// <summary>
    /// Recomputes the rank for the given zone area against the cached board
    /// and publishes it. While the board is missing the last known result
    /// stays. Never throws.
    /// </summary>
    void Refresh(double zoneKm);
}
