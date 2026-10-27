using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Null-object operating-zone board: no board is ever loaded, so
/// <see cref="LastResult"/> stays null and the expansion page hides the block
/// (Unity's build with rivals compiled out). Screens subscribe exactly like
/// they would against the live backend; the event is simply never raised.
/// </summary>
public sealed class OfflineZoneStandings : IZoneStandings
{
    public ZoneStandingsResult? LastResult => null;

    // Never raised offline (there is no async board that could land);
    // declared with explicit empty accessors so screens subscribe exactly
    // like they would against the live backend.
    public event Action<ZoneStandingsResult>? ResultUpdated
    {
        add { }
        remove { }
    }

    public bool IsAvailable => false;

    public void Start()
    {
        // Nothing to load: there is no board behind this build.
    }

    public void Refresh(double zoneKm)
    {
        // Nothing to rank against: the page hides the whole block.
    }
}
