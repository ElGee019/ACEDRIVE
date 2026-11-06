using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The delivery run from DELIVER to receipt (PORTPLAN 19, Unity's
/// DeliveryJobHandler): the breakdown roll at the trip start, the ordered hub
/// pickups and store drop-offs, the shelves each stop fills, and the settle
/// after the last stop.
///
/// Owns the live run, the route it prices at DELIVER (the hub and store doors
/// on the closed lap, never the driven distance), and the board page the run
/// is staged on. The panel, the companion and the settle arrive through
/// <see cref="IDeliveryHost"/>. The board itself (which shops exist) stays
/// outside: it grows while the overlay runs, so it has a holder of its own.
/// </summary>
public sealed class DeliveryFlow
{
    private readonly DeliveryBoard _board;
    private readonly DeliveryCareer _career;
    private readonly CityEconomy _city;
    private readonly IFriendshipStore _friends;
    private readonly DriverWallet _wallet;
    private readonly Glovebox _glovebox;
    private readonly Trunk _trunk;
    private readonly Func<double> _trunkCapacity;
    private readonly AddressMap _addressMap;
    private readonly Func<DeliveryStop, AddressLocation?> _stopDoor;
    private readonly JobMileageMeter _mileage;
    private readonly TripBreakdowns _breakdowns;
    private readonly IClock _clock;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    // WHY late: the host is the coordinator, which cannot exist before this flow
    // does. It attaches once at construction; every command below is only
    // reachable from the UI after that.
    private IDeliveryHost _host = null!;

    // The live run. Null outside one; the taxi job and this cannot overlap
    // because a call/board is refused while either is in progress.
    private DeliveryJob? _job;
    // EVO's live lap length when the static block reported one: the doors are
    // fractions of it, exactly like the taxi's, so the priced route follows the
    // track the HUD pins instead of the placeholder map's own length.
    private double? _trackLengthMeters;
    // Built once and reused: the pickup cart survives leaving the page, like
    // Unity's player.pickupCart, and the receipt's CONTINUE comes back to it.
    private DeliveryJobScreenViewModel? _boardPage;

    public DeliveryFlow(
        DeliveryBoard board, DeliveryCareer career, CityEconomy city,
        IFriendshipStore friends, DriverWallet wallet, Glovebox glovebox, Trunk trunk,
        Func<double> trunkCapacity, AddressMap addressMap,
        Func<DeliveryStop, AddressLocation?> stopDoor,
        JobMileageMeter mileage, TripBreakdowns breakdowns,
        IClock clock, VoiceBeat beat, ILog log)
    {
        _board = board;
        _career = career;
        _city = city;
        _friends = friends;
        _wallet = wallet;
        _glovebox = glovebox;
        _trunk = trunk;
        _trunkCapacity = trunkCapacity;
        _addressMap = addressMap;
        _stopDoor = stopDoor;
        _mileage = mileage;
        _breakdowns = breakdowns;
        _clock = clock;
        _beat = beat;
        _log = log;
    }

    /// <summary>Attaches the shell that owns the panel, the HUD and the settle.</summary>
    public void Attach(IDeliveryHost host) => _host = host;

    /// <summary>True from DELIVER until the last store settles.</summary>
    public bool InProgress => _job is { IsComplete: false };

    /// <summary>The cached board page, or null before the player first opened it.</summary>
    public object? Board => _boardPage;

    /// <summary>
    /// The board page, refreshed against the live board (filled shelves, a shop
    /// that connected, the spent CredP) and built on the first visit.
    /// </summary>
    public object OpenBoard()
    {
        _boardPage ??= new DeliveryJobScreenViewModel(
            _board.Catalog, _career, _city, _wallet, _trunk, _trunkCapacity, _log,
            new RelayCommand(() => _host.ShowPage(_host.ControlPage())),
            Start,
            new RelayCommand(() => _host.OpenHub()),
            _clock, _beat);
        _boardPage.Refresh(_board.Catalog);
        return _boardPage;
    }

    /// <summary>Re-reads the board: a door opened or a hub was bought since the last paint.</summary>
    public void RefreshBoard() => _boardPage?.Refresh(_board.Catalog);

    /// <summary>
    /// The board's timed notice edge: the 10 Hz poll clears an expired
    /// fully-stocked refusal even while another page is shown, so the cached
    /// board never re-opens mid-hold.
    /// </summary>
    public void UpdateNotice() => _boardPage?.UpdateNotice();

    /// <summary>
    /// DELIVER: charges the goods, rolls the trip start, plans the stops in
    /// cart order, prices the planned route and pins the first leg. The
    /// wholesale sum leaves cash here — before the trip risk — so a breakdown
    /// or crash refund returns money the run actually paid. False when the
    /// cash cannot cover the goods: nothing is charged, planned or pinned, and
    /// the page keeps the cart. A triggered breakdown loses the cargo (no
    /// settle, no credit) and charges every producing hub's owner 1
    /// familiarity before its page opens (Unity's BeginTripSession).
    /// </summary>
    public bool Start(DeliveryRun? run)
    {
        if (run is null || run.Orders.Count == 0 || InProgress)
        {
            return false;
        }

        // WHY the hand-over charge: the receipt nets the goods cost out of the
        // payout, so the money has to have left here for that subtraction to
        // be honest. The Delivery Insurance Shield's refund (below and in the
        // accident path) then returns a real debit instead of minting cash.
        if (run.PaidForGoods > 0.0 && !_wallet.TrySpend(run.PaidForGoods))
        {
            _log.LogWarning(
                $"delivery start refused, cash short: €{run.PaidForGoods:0.00} " +
                $"> €{_wallet.Cash:0.00}");
            return false;
        }

        if (_breakdowns.Roll(BreakdownKind.Delivery) is { } breakdown)
        {
            // Delivery Insurance Shield refunds the goods money the run had
            // already paid; the friendship penalty still lands (Unity's
            // BreakdownFlow consumes only the insurance card — its loyalty
            // shield is reserved for accidents). The refund rides the
            // breakdown page as a note.
            var notes = new List<HudPromptNote>(1);
            bool insured = run.PaidForGoods > 0.0
                && _glovebox.TryConsume(RewardCardEffect.DeliveryInsurance, _trunk);
            if (insured)
            {
                _wallet.AddSale(run.PaidForGoods);
                notes.Add(new HudPromptNote(
                    "RewardDeliveryInsuranceNote",
                    [run.PaidForGoods.ToString("0.00", CultureInfo.CurrentCulture)]));
            }
            foreach (string owner in SourceOwners(run))
            {
                _friends.AddFamiliarity(owner, -1);
            }
            _log.LogInfo(
                $"delivery cut by breakdown: {run.Orders.Count} stores lost, " +
                $"insurance {(insured ? "refunded" : "none")}, friendship -1 per hub owner");
            _host.RevealPage(_breakdowns.Interrupt(
                breakdown, new RelayCommand(() => _host.ShowPage(_host.ControlPage())), notes));
            return true;
        }

        IReadOnlyList<DeliveryStop> stops =
            DeliveryJobPlanner.Plan(_board.Catalog, _career, run.Orders);
        // The charge is the planned route, priced here like the taxi's ACCEPT:
        // the doors the companion is about to pin, hub first, measured on the
        // closed lap. The drive never moves it (PORTPLAN 19), so a detour, a
        // shortcut or a longer way round buys nothing.
        run = run with { DistanceKm = ChargeableKilometers(stops) };
        _job = new DeliveryJob(stops, run);
        // The run's start odometer: the last store's settle banks the drive
        // (PORTPLAN 44), not the route charge above.
        _mileage.StartJob();
        _log.LogInfo($"delivery started: {stops.Count} stops, " +
            $"{run.Orders.Count} stores, goods €{run.PaidForGoods:0.00}, " +
            $"route {run.DistanceKm:0.00} km");
        _host.HidePanel();
        PinStop();
        return true;
    }

    /// <summary>
    /// EVO's live lap length when its static block reports one, so the doors
    /// keep the offsets the HUD pins and the priced route follows the same
    /// track. Falls back to the address map's own length.
    /// </summary>
    public void SetTrackLength(double meters) => _trackLengthMeters = meters;

    // The route the run is paid for: every planned stop's door in the order the
    // companion will pin them, hub first (the freight door) then the stores.
    // The drive to the first hub is the player's own approach and stays
    // uncharged, exactly the boundary the meter's store-leg reset drew before.
    private double ChargeableKilometers(IReadOnlyList<DeliveryStop> stops)
    {
        var doors = new AddressLocation?[stops.Count];
        for (int i = 0; i < stops.Count; i++)
        {
            doors[i] = _stopDoor(stops[i]);
        }

        return LapRouteDistance.Kilometers(
            _addressMap, startNormalizedPosition: null, doors, TrackLengthMeters());
    }

    private double TrackLengthMeters() =>
        _trackLengthMeters ?? _addressMap.TrackLengthMeters;

    /// <summary>
    /// The one ENTER action: at a hub it loads the freight, at a store it fills
    /// the shelves for that stop and advances. The receipt waits for the last
    /// stop, unlike Unity's per-stop page, so the run settles as one job.
    /// </summary>
    public void Deliver()
    {
        if (_job is not { } job || job.Current is not { } stop)
        {
            return;
        }

        if (stop.Kind == DeliveryStopKind.Store)
        {
            bool moved = _city.AddStock(stop.Key, stop.Units);
            _log.LogInfo($"delivery stop served: {stop.Name}" +
                (moved ? "" : " (already full)"));
        }
        else
        {
            _log.LogInfo($"delivery pickup: {stop.Name}");
        }

        job.Advance();
        PinStop();
    }

    /// <summary>
    /// The crash that ends a run (Unity's delivery accident): the cargo is
    /// lost — the goods were paid at DELIVER, so only a Delivery Insurance
    /// Shield refunds them — every producing hub's owner loses one familiarity
    /// unless an Accident Loyalty Shield was consumed, and the run is dropped
    /// without a receipt. Returns the CR invested in the goods (null when no
    /// run was live) so the caller can price the insurance refund.
    /// </summary>
    public double? FailForAccident(bool shielded = false) => Fail("accident", shielded);

    /// <summary>
    /// The dry tank that ends a run (Unity's FailJobOnFuelOut delivery branch):
    /// the cargo is lost and every producing hub's owner loses one familiarity.
    /// Returns the CR invested in the goods, or null when no run was live.
    /// </summary>
    public double? FailForRoadService() => Fail("road service", shielded: false);

    private double? Fail(string reason, bool shielded)
    {
        if (_job is not { IsComplete: false } run)
        {
            return null;
        }

        if (!shielded)
        {
            foreach (string owner in SourceOwners(run.Run))
            {
                _friends.AddFamiliarity(owner, -1);
            }
        }

        double invested = run.Run.PaidForGoods;
        _log.LogWarning($"delivery run ended by {reason}: {run.Run.Orders.Count} stores lost, " +
            $"€{invested:0.00} cargo, " +
            $"friendship {(shielded ? "kept (shield)" : "-1 per hub owner")}");
        // Stop drops the run and the companion's leg; the shells' saves follow
        // in the caller, like the breakdown path.
        Stop();
        return invested;
    }

    /// <summary>
    /// Drops the run and its companion line once it settled. The board page
    /// stays: the receipt's CONTINUE lands back on it with the cart emptied.
    /// </summary>
    public void Stop()
    {
        _job = null;
        _mileage.CancelJob();
        _host.ClearGuidance();
    }

    // Pins the current leg on the companion, or skips a leg whose content lost
    // its address so the run can never stall on an unreachable stop.
    private void PinStop()
    {
        while (_job?.Current is { } stop)
        {
            if (_host.PinStop(stop))
            {
                return;
            }

            _log.LogWarning(
                $"delivery stop skipped, no address: {stop.Name} ({stop.Key})");
            // Unity's settle still stocks a store whose door went missing; the
            // arrival is the only writer now, so the skipped leg delivers here.
            if (stop.Kind == DeliveryStopKind.Store)
            {
                _city.AddStock(stop.Key, stop.Units);
            }
            _job.Advance();
        }

        if (_job is { } run)
        {
            // The last stop is the finished job: bank the run's odometer delta
            // before the settle's save carries the driving record.
            _mileage.FinishJob();
            _host.Settle(run.Run);
        }
    }

    // The distinct hub owners a run would have befriended, resolved read-only
    // (a failed trip must not credit the ramp). Used by the breakdown hook.
    private List<string> SourceOwners(DeliveryRun run)
    {
        List<string> owners = [];
        foreach (DeliveryOrder order in run.Orders)
        {
            if (_board.Catalog.Find(order.StoreKey) is not { } store)
            {
                continue;
            }
            string owner = _career.OwnerFor(store.Category);
            if (owner.Length > 0 && !owners.Contains(owner))
            {
                owners.Add(owner);
            }
        }
        return owners;
    }
}
