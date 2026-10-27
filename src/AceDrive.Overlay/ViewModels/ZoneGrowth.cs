using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The operating-zone growth at the settle hooks (PORTPLAN 58, Unity
/// <c>ExpandOperatingZone</c>): once the settled-job counter reaches the
/// threshold curve and the introduction is done, one more street joins the zone
/// and the new length is pushed to the world board.
///
/// The gates read the onboarding file instead of arriving as a value: this runs
/// after a job was banked, never from the 10 Hz poll, so it is the same contract
/// <c>PoiWorld</c> and <c>TripBreakdowns</c> already use. The confirmed name
/// comes off the profile store exactly like <c>DayCycle</c>'s shift push. The
/// zone itself stays with the services that draw and bank it (<c>JobSettlement</c>,
/// <c>GameStateSaver</c>, <c>PoiWorld</c>); this collaborator only decides when
/// it grows.
/// </summary>
public sealed class ZoneGrowth
{
    private readonly OperatingZone _zone;
    // The placeholder map supplies the interim street length until real street
    // metadata arrives, so the zone km (and the starter bonus) keeps decaying
    // with every expansion (PORTPLAN 58).
    private readonly AddressMap _map;
    private readonly IOnboardingStore _onboarding;
    // The confirmed display name the world push carries; before the opening
    // screen is answered it stays nameless, like the old placeholder.
    private readonly IProfileStore _profiles;
    private readonly IPlayerTelemetry _online;
    private readonly ILog _log;
    // The fixed street pool is finite, so exhaustion is a one-time state
    // transition, not a per-settle event (AGENTS › hot-path logging).
    private bool _poolExhaustedLogged;

    public ZoneGrowth(
        OperatingZone zone, AddressMap map, IOnboardingStore onboarding,
        IProfileStore profiles, IPlayerTelemetry online, ILog log)
    {
        _zone = zone;
        _map = map;
        _onboarding = onboarding;
        _profiles = profiles;
        _online = online;
        _log = log;
    }

    /// <summary>
    /// Registers the next street when the settled jobs already reached the
    /// curve and answers what joined, or null when nothing did (introduction
    /// unfinished, curve not reached, street pool exhausted). Call it after a
    /// job's progress was banked — the counter is bumped there — and never
    /// from the poll: the onboarding gate is a disk read.
    /// </summary>
    public ZoneExpansion? Expand()
    {
        // Unity's firstSceneComplete gate: a fresh career is still in the
        // introduction, so the zone keeps its home street until it ends.
        if (!_onboarding.Load().IsComplete || !_zone.ExpansionDue)
        {
            return null;
        }

        // The map is the fixed street pool: once every row is in the zone
        // there is nothing left to name, and the counter stays due. Log the
        // completion once, not on every later settle.
        if (_zone.NextStreet is not { } street)
        {
            if (!_poolExhaustedLogged)
            {
                _poolExhaustedLogged = true;
                _log.LogInfo($"[Zone] street pool complete " +
                    $"({_zone.StreetIds.Count} streets); no further expansion");
            }
            return null;
        }

        double previousKm = _zone.Km;
        if (!_zone.TryExpand(street.Id, AddressBook.StreetLengthKm(_map, street)))
        {
            return null;
        }

        var expansion = new ZoneExpansion(
            street.Name, _zone.StreetIds.Count, previousKm, _zone.Km,
            _zone.JobsUntilNext);
        _log.LogInfo($"[Zone] expanded: {expansion.StreetName} joins " +
            $"(zone {expansion.StreetCount} streets, {expansion.NewKm:0.0} km)");
        _online.PushZoneKm(_zone.Km, PlayerName());
        return expansion;
    }

    // The opening screen supplies the display name; before it is confirmed the
    // push stays nameless like the old placeholder.
    private string PlayerName() => _profiles.Load()?.DisplayName ?? string.Empty;
}
