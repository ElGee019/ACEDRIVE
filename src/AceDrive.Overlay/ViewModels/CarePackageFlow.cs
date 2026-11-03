using System.Globalization;
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The care-package delivery run (PORTPLAN item 21, port of Unity
/// CarePackageDeliveryHandler): DELIVER CARE PACKAGES sends the player to the
/// stores holding kits, the companion repins the next door after each PICK UP,
/// and once the last store is served it pins the community centre. DELIVER
/// there hands the kits over and opens the receipt page. A run serves at most
/// <see cref="CarePackageLedger.MaxBagsPerRun"/> kits; leftovers wait for the
/// next run, exactly like Unity's 5-bag budget.
/// The delivered page carries the run's reward pair (PORTPLAN 61): two cards
/// the community centre offers, one claim, then CONTINUE back to the jobs page —
/// Unity's rewards canvas on the same completion edge.
///
/// The route is session state, re-derived from the ledger on the next tap; the
/// ledger itself is persisted, so a restart after a pickup resumes at the
/// community centre with the kits still in the car (Unity
/// PlayerData.carePackagesCarried). Arrival rides the one 10 Hz poll through
/// the HUD's cached arrival — the flow only reacts to the ENTER tap, never to
/// its own timer.
/// </summary>
public sealed class CarePackageFlow
{
    private readonly PoiDirectory _directory;
    private readonly CarePackageLedger _ledger;
    // The completed block is the player's evidence that Sonja arranged the
    // kits; the run is only offered once it lists the bag threshold.
    private readonly ITodoList _todos;
    private readonly CarePackageReward _rewards;
    private readonly AddressMap _map;
    private readonly ITelemetryHud? _hud;
    // The root's driver bank, with the standings snapshot it owns: the flow
    // never assembles a save and never wipes the board a null snapshot would.
    private readonly Action _saveDriver;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    // WHY late: the host is the coordinator, which is built after this flow.
    // The attach closes in the coordinator's constructor, before any DELIVER
    // tap can reach the flow.
    private ICarePackageHost _host = null!;

    private CarePackagePhase _phase = CarePackagePhase.Idle;
    private List<CarePackagePlacement> _legs = [];
    private int _legIndex;
    private readonly List<string> _pickedUpAt = [];
    private PointOfInterest? _centre;

    public CarePackageFlow(
        PoiDirectory directory, CarePackageLedger ledger, ITodoList todos,
        CarePackageReward rewards, AddressMap map, ITelemetryHud? hud,
        Action saveDriver, VoiceBeat beat, ILog log)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(todos);
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(saveDriver);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(log);
        _directory = directory;
        _ledger = ledger;
        _todos = todos;
        _rewards = rewards;
        _map = map;
        _hud = hud;
        _saveDriver = saveDriver;
        _beat = beat;
        _log = log;
    }

    /// <summary>True from the DELIVER tap until the community centre took the kits.</summary>
    public bool InFlight => _phase != CarePackagePhase.Idle;

    /// <summary>
    /// True when the jobs page should offer DELIVER CARE PACKAGES: the
    /// completed block lists the five tips Sonja arranged kits for (Unity's bag
    /// threshold) and the town holds those kits, or a previous session already
    /// put kits in the car, which must stay deliverable even when no tip row
    /// remains.
    /// </summary>
    /// <remarks>
    /// WHY the completed rows and not the kit total: a run carries at most
    /// <see cref="CarePackageLedger.MaxBagsPerRun"/> kits, so a run that served
    /// five of six leaves one on its shelf. Counting kits re-raised the button
    /// at four completed tips — a fifth kit the player never saw complete — so
    /// the block the page prints is what unlocks the run, and the leftover
    /// waits for the next five tips.
    /// </remarks>
    public bool CanOffer =>
        !InFlight
        && _directory.Find(StoryCommunityCentre.Definition.Id) is not null
        && (_ledger.HasBagsInCar
            || (_todos.CompletedCount >= CarePackageLedger.BagThreshold
                && _ledger.Total >= CarePackageLedger.BagThreshold));

    /// <summary>Attaches the coordinator that owns the panel, the receipt and the way back.</summary>
    public void Attach(ICarePackageHost host) => _host = host;

    /// <summary>
    /// The control page's DELIVER CARE PACKAGES tap. False when the page did not
    /// offer the run (see <see cref="CanOffer"/>) or there is no store door to
    /// start at, so the page can leave the tip block and its button alone. A car
    /// already carrying kits from a previous session resumes at the drop-off
    /// instead of drawing a second route.
    /// </summary>
    public bool Begin()
    {
        if (_phase != CarePackagePhase.Idle)
        {
            return false;
        }

        _centre = _directory.Find(StoryCommunityCentre.Definition.Id);
        if (_centre is null)
        {
            _log.LogWarning("care packages not started: the community centre is not discovered");
            return false;
        }

        // The button and the tap share one rule: a command invoked behind a
        // hidden button must not start a run the page never offered.
        if (!CanOffer)
        {
            // The refusal leaves no half-started run behind, so the centre the
            // check just found must not linger into the idle state.
            _centre = null;
            _log.LogInfo(
                $"care packages not started: {_todos.CompletedCount} completed tip(s) and " +
                $"{_ledger.Total} kit(s); {CarePackageLedger.BagThreshold} of each needed");
            return false;
        }

        if (_ledger.HasBagsInCar)
        {
            _pickedUpAt.Clear();
            _host.HidePanel();
            EnterDropoff();
            _log.LogInfo($"care packages resumed: {_ledger.Carried} kit(s) already in the car");
            return true;
        }

        _legs = AllocateLegs();
        if (_legs.Count == 0)
        {
            _log.LogWarning("care packages not started: no store door holds a kit");
            return false;
        }

        _legIndex = 0;
        _pickedUpAt.Clear();
        _phase = CarePackagePhase.Pickup;
        _host.HidePanel();
        PinNextPickup();
        _log.LogInfo(
            $"care packages started: {_legs.Count} store(s), " +
            $"{_ledger.Outstanding} kit(s) on the shelves");
        return true;
    }

    /// <summary>
    /// The companion's ENTER at the pinned door: PICK UP loads the current
    /// store's kits (then repins the next door or the community centre), and
    /// DELIVER hands every carried kit over and opens the receipt.
    /// </summary>
    public void ConfirmStop()
    {
        if (_phase == CarePackagePhase.Pickup)
        {
            PickupCurrent();
            return;
        }

        if (_phase == CarePackagePhase.Dropoff)
        {
            Deliver();
        }
    }

    // The delivered page's CONTINUE: the run is over, so back to the jobs page
    // it was started from.
    private void Finish() => _host.ShowControl();

    private void PickupCurrent()
    {
        CarePackagePlacement leg = _legs[_legIndex];
        int picked = _ledger.Pickup(leg.StoreId, leg.Count);
        if (picked > 0)
        {
            string name = _directory.Find(leg.StoreId)?.Name ?? leg.StoreId;
            _pickedUpAt.Add(name);
            _saveDriver();
            _hud?.DestinationEntered();
            _log.LogInfo(
                $"care packages picked up at {name}: {picked} kit(s), " +
                $"{_ledger.Carried} in the car");
        }

        _legIndex++;
        PinNextPickup();
    }

    private void PinNextPickup()
    {
        while (_legIndex < _legs.Count)
        {
            CarePackagePlacement leg = _legs[_legIndex];
            if (_directory.Find(leg.StoreId) is { } store)
            {
                _hud?.SetDestination(store, PickupCopy(store, _legIndex + 1, _legs.Count));
                return;
            }

            // A saved store id the directory no longer knows: its kits stay on
            // the ledger for the next run, never picked up from a door that is
            // not there.
            _legIndex++;
        }

        EnterDropoff();
    }

    private void EnterDropoff()
    {
        _phase = CarePackagePhase.Dropoff;
        if (_centre is null)
        {
            Reset();
            return;
        }

        _hud?.SetDestination(_centre, DropoffCopy(_centre));
        _log.LogInfo(
            $"care packages collected: drive to {_centre.Name} " +
            $"({_ledger.Carried} kit(s) in the car)");
    }

    private void Deliver()
    {
        int delivered = _ledger.Deliver();
        _saveDriver();
        var receipt = new CarePackageReceipt(
            delivered, _ledger.Outstanding, [.. _pickedUpAt]);
        _hud?.SetDestination(null);
        Reset();
        _log.LogInfo(
            $"care packages delivered: {delivered} kit(s), " +
            $"{receipt.Remaining} left for a later run");
        // The reward pair rides the same completion edge as the receipt (Unity's
        // AllCarePackagesDeliveredRequested) and is drawn once, here, so a page
        // rebuild can never re-roll the offer. A catalogue too thin to fill a
        // page closes the run with the receipt alone.
        RewardChooser? reward = _rewards.Offer() is { } offer
            ? new RewardChooser(offer, _rewards)
            : null;
        if (reward is null)
        {
            _log.LogWarning("reward cards skipped: the catalogue cannot offer two cards");
        }

        _host.PresentPage(new CarePackageReceiptScreenViewModel(
            receipt, reward, _beat, new RelayCommand(Finish)));
    }

    // Only the stores the directory can still name join the route; the draw's
    // cap was already applied, so dropping a missing door shortens the run
    // rather than making it up from a later store.
    private List<CarePackagePlacement> AllocateLegs()
    {
        IReadOnlyList<CarePackagePlacement> allocated = _ledger.AllocateRun();
        List<CarePackagePlacement> legs = new(allocated.Count);
        foreach (CarePackagePlacement leg in allocated)
        {
            if (_directory.Find(leg.StoreId) is not null)
            {
                legs.Add(leg);
            }
        }

        return legs;
    }

    // The pickup leg's copy: the shared address shape first ({0} name, {1}
    // street, {2} number, {3} city), then the run's two readings ({4} leg,
    // {5} legs), so a translation keeps the door and can reorder the run line.
    private HudDestinationCopy PickupCopy(
        PointOfInterest store, int leg, int legs) =>
        new(
            "HudCarePickupDrive",
            "HudCarePickupArrived",
            "HudCarePickupAction",
            [
                store.Name,
                store.Address.StreetName,
                store.Address.HouseNumber.ToString(CultureInfo.CurrentCulture),
                _map.City,
                leg.ToString(CultureInfo.CurrentCulture),
                legs.ToString(CultureInfo.CurrentCulture),
            ]);

    // The drop-off leg names the community centre and nothing else: the arrival
    // is its DELIVER button.
    private HudDestinationCopy DropoffCopy(PointOfInterest centre) =>
        new(
            "HudCareDropoffDrive",
            "HudCareDropoffArrived",
            "HudCareDropoffAction",
            [
                centre.Name,
                centre.Address.StreetName,
                centre.Address.HouseNumber.ToString(CultureInfo.CurrentCulture),
                _map.City,
            ]);

    private void Reset()
    {
        _phase = CarePackagePhase.Idle;
        _legs.Clear();
        _legIndex = 0;
        _pickedUpAt.Clear();
        _centre = null;
    }
}
