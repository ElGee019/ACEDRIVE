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
    /// curve. Call it after a job's progress was banked — the counter is bumped
    /// there — and never from the poll: the onboarding gate is a disk read.
    /// </summary>
    public void Expand()
    {
        // Unity's firstSceneComplete gate: a fresh career is still in the
        // introduction, so the zone keeps its home street until it ends.
        if (!_onboarding.Load().IsComplete || !_zone.ExpansionDue)
        {
            return;
        }

        // Interim ids: the concrete Eifel street table (PORTPLAN Step 1) is
        // still pending, so the nth expansion claims "s{n}".
        string streetId = $"s{_zone.ExpansionCount + 1}";
        if (!_zone.TryExpand(streetId, AddressBook.AverageStreetLengthKm(_map)))
        {
            return;
        }

        _log.LogInfo($"[Zone] expanded: {streetId} joins " +
            $"(zone {_zone.StreetIds.Count} streets, {_zone.Km:0.0} km)");
        _online.PushZoneKm(_zone.Km, PlayerName());
    }

    // The opening screen supplies the display name; before it is confirmed the
    // push stays nameless like the old placeholder.
    private string PlayerName() => _profiles.Load()?.DisplayName ?? string.Empty;
}
