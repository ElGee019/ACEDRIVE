using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The Saturday Touristenfahrten run: pick up a shop order (coffee or pizza),
/// then drive it to 1-3 Nordschleife corners, one receipt per dropoff. The
/// calendar row starts it; the companion guides every leg with its own task
/// text (the dropoff list on the way to the shop, the loaded order on the way
/// to a corner), and the HUD's action button confirms each stop
/// (PICK UP, then DELIVER ITEMS).
///
/// The receipt's CONTINUE is what moves the run on: it pins the next dropoff
/// and hides the panel again, or lands back on the calendar after the last one.
/// The shared drive meter prices each corner leg (reset when the goods load and
/// at every CONTINUE, so the pickup drive is not charged), exactly like the
/// career delivery run. The run is session state like a delivery run: a process
/// death loses it, and the doors it opened stay banked in the POI directory.
/// </summary>
public sealed class TouristenfahrtenFlow
{
    private readonly TouristenMenu _menu;
    private readonly ITouristenDoors _doors;
    private readonly ITelemetryHud? _hud;
    private readonly ITouristenSettlement _settlement;
    private readonly TaxiDriveMeter _meter;
    private readonly double _arrivalRadiusMeters;
    private readonly Random _rng;
    private readonly ILog _log;

    // WHY late: the host is the coordinator, which is built after this flow.
    // It attaches in the coordinator's constructor, before any calendar tap can
    // reach this class.
    private ITouristenfahrtenHost _host = null!;

    private TouristenRun? _run;
    // True while a dropoff's receipt is on screen: the receipt's CONTINUE is
    // the only thing that may move the run on, so a second HUD ENTER cannot
    // settle the same order twice.
    private bool _awaitingReceipt;

    public TouristenfahrtenFlow(
        TouristenMenu menu, ITouristenDoors doors, ITelemetryHud? hud,
        ITouristenSettlement settlement, TaxiDriveMeter meter, TaxiRules taxiRules,
        Random rng, ILog log)
    {
        ArgumentNullException.ThrowIfNull(taxiRules);
        ArgumentNullException.ThrowIfNull(meter);

        _menu = menu;
        _doors = doors;
        _hud = hud;
        _settlement = settlement;
        _meter = meter;
        _arrivalRadiusMeters = taxiRules.ArrivalRadiusMeters;
        _rng = rng;
        _log = log;
    }

    /// <summary>Attaches the shell that owns the panel, the receipt and the calendar.</summary>
    public void Attach(ITouristenfahrtenHost host) => _host = host;

    /// <summary>True from the calendar tap until the last dropoff's CONTINUE.</summary>
    public bool InProgress => _run is not null;

    /// <summary>
    /// The calendar row's tap: draws the pickup shop, 1-3 dropoffs and each
    /// dropoff's order, pins the shop and gets the panel out of the companion's
    /// way. A run already in progress is left alone; a missing door refuses the
    /// run instead of pinning a destination that cannot be reached.
    /// </summary>
    public void Begin()
    {
        if (_run is not null)
        {
            _log.LogInfo("touristenfahrten ignored: a run is already in progress");
            return;
        }

        if (_doors.Pickup() is not { } pickup
            || _menu.PickupFor(pickup.Type) is not { } kind)
        {
            _log.LogWarning("touristenfahrten not started: no pickup door");
            return;
        }

        IReadOnlyList<TouristenDropoffDefinition> places =
            TouristenRules.DrawDropoffs(_menu, _rng);
        var dropoffs = new List<TouristenDropoff>(places.Count);
        foreach (TouristenDropoffDefinition place in places)
        {
            if (_doors.Dropoff(place) is not { } door)
            {
                _log.LogWarning($"touristenfahrten dropoff skipped: {place.Name}");
                continue;
            }

            dropoffs.Add(new TouristenDropoff(
                door, TouristenRules.DrawOrder(kind, _menu.MinItemsPerOrder, _rng)));
        }

        if (dropoffs.Count == 0)
        {
            _log.LogWarning("touristenfahrten not started: no dropoff door");
            return;
        }

        _run = new TouristenRun(pickup, kind, dropoffs);
        _awaitingReceipt = false;
        _meter.Reset();
        PinPickup(_run);
        _host.HidePanel();
        _log.LogInfo($"touristenfahrten started: {pickup.Name} -> " +
            $"{dropoffs.Count} dropoff(s): {TouristenCopy.Names(dropoffs)}");
    }

    /// <summary>
    /// The one HUD ENTER: at the shop it loads the goods and pins the first
    /// corner; at a corner it settles that order through the receipt. The
    /// receipt's CONTINUE is what advances the run (see ContinueAfterReceipt).
    /// </summary>
    public void Deliver()
    {
        if (_run is not { } run || _awaitingReceipt)
        {
            return;
        }

        if (!run.Loaded)
        {
            run.Load();
            // The drive to the shop is the player's own; only the corner legs
            // are priced, so the loaded order starts the meter at zero.
            _meter.Reset();
            PinDropoff(run);
            _log.LogInfo($"touristenfahrten goods loaded at {run.Pickup.Name}: " +
                $"{run.Dropoffs.Count} dropoff(s)");
            return;
        }

        if (run.Current is not { } dropoff)
        {
            return;
        }

        _awaitingReceipt = true;
        _settlement.Settle(dropoff, _meter.DistanceKm, _host, ContinueAfterReceipt);
    }

    // The receipt's CONTINUE: the delivered dropoff is behind the player, so
    // the next corner is pinned (panel out of the way) or the run hands back to
    // the calendar it was started from.
    private void ContinueAfterReceipt()
    {
        _awaitingReceipt = false;
        if (_run is not { } run)
        {
            _host.GoToCalendar();
            return;
        }

        run.Advance();
        if (run.Current is not { } next)
        {
            Finish(run);
            return;
        }

        _host.HidePanel();
        // The delivered leg is banked; the next one starts from zero distance
        // so a corner is never charged for the drive that reached it.
        _meter.Reset();
        PinDropoff(run);
        _log.LogInfo($"touristenfahrten next dropoff: {next.Door.Name}");
    }

    // The run's last word: the companion line goes and the player is back on
    // the calendar, ready for the next Saturday.
    private void Finish(TouristenRun run)
    {
        _run = null;
        _awaitingReceipt = false;
        _meter.Reset();
        _hud?.SetJobGuidance(null);
        _host.GoToCalendar();
        _log.LogInfo($"touristenfahrten complete: {run.Dropoffs.Count} dropoff(s)");
    }

    // The pickup leg: the shop plus the run's whole dropoff list, so the player
    // reads what they are loading for before they arrive.
    private void PinPickup(TouristenRun run) =>
        _hud?.SetJobGuidance(new HudJobGuidance(
            run.Pickup.Name, run.Pickup.Address,
            "HudTouristenPickupDrive", "HudTouristenPickupArrived", "HudPickUp",
            _arrivalRadiusMeters,
            DriveArgs: [run.Pickup.Name, TouristenCopy.Names(run.Dropoffs)]));

    // The corner leg: the order on board and where it goes. The arrival line
    // and the DELIVER ITEMS button are the delivery run's own, so the two jobs
    // read the same wording at a door.
    private void PinDropoff(TouristenRun run)
    {
        if (run.Current is not { } dropoff)
        {
            return;
        }

        _hud?.SetJobGuidance(new HudJobGuidance(
            dropoff.Door.Name, dropoff.Door.Address,
            "HudTouristenDropoffDrive", "HudDeliveryStoreArrived", "HudDeliveryStoreAction",
            _arrivalRadiusMeters,
            DriveArgs: [TouristenCopy.Order(dropoff.Order), dropoff.Door.Name]));
    }

    /// <summary>
    /// The corner legs' distance: the shared meter is fed by the poll while the
    /// run is live, exactly like the taxi passenger leg and the delivery store
    /// legs, so the two receipts price a leg the same way.
    /// </summary>
    public void Advance(TelemetrySample sample, double dt) =>
        _meter.Add(sample.SpeedKmh, dt);
}
