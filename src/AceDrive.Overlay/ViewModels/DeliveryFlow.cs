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
/// Owns the live run, the meter edge that separates the pickup drive from the
/// charged store legs, and the board page the run is staged on. The panel, the
/// companion and the settle arrive through <see cref="IDeliveryHost"/>. The
/// board itself (which shops exist) stays outside: it grows while the overlay
/// runs, so it has a holder of its own.
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
    private readonly DrivenDistanceMeter _meter;
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
    // True once the run reached its first store leg: the meter is reset at that
    // edge so the hub pickup distance is not charged as delivery distance
    // (Unity stamps jobStartOdometer when the store route starts).
    private bool _storeLegStarted;
    // Built once and reused: the pickup cart survives leaving the page, like
    // Unity's player.pickupCart, and the receipt's CONTINUE comes back to it.
    private DeliveryJobScreenViewModel? _boardPage;

    public DeliveryFlow(
        DeliveryBoard board, DeliveryCareer career, CityEconomy city,
        IFriendshipStore friends, DriverWallet wallet, Glovebox glovebox, Trunk trunk,
        DrivenDistanceMeter meter, JobMileageMeter mileage, TripBreakdowns breakdowns,
        IClock clock, VoiceBeat beat, ILog log)
    {
        _board = board;
        _career = career;
        _city = city;
        _friends = friends;
        _wallet = wallet;
        _glovebox = glovebox;
        _trunk = trunk;
        _meter = meter;
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
            _board.Catalog, _career, _city, _log,
            new RelayCommand(() => _host.ShowPage(_host.ControlPage())),
            new ParameterizedCommand<DeliveryRun>(Start),
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
    /// DELIVER: rolls the trip start, plans the stops in cart order, resets the
    /// shared meter and pins the first leg. A triggered breakdown loses the
    /// cargo (no settle, no credit) and charges every producing hub's owner 1
    /// familiarity before its page opens (Unity's BeginTripSession).
    /// </summary>
    public void Start(DeliveryRun? run)
    {
        if (run is null || run.StoreKeys.Count == 0 || InProgress)
        {
            return;
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
                $"delivery cut by breakdown: {run.StoreKeys.Count} stores lost, " +
                $"insurance {(insured ? "refunded" : "none")}, friendship -1 per hub owner");
            _host.RevealPage(_breakdowns.Interrupt(
                breakdown, new RelayCommand(() => _host.ShowPage(_host.ControlPage())), notes));
            return;
        }

        IReadOnlyList<DeliveryStop> stops =
            DeliveryJobPlanner.Plan(_board.Catalog, _career, run.StoreKeys);
        _job = new DeliveryJob(stops, run);
        _storeLegStarted = false;
        _meter.Reset();
        // The run's start odometer: the last store's settle banks the drive
        // (PORTPLAN 44), not the shared leg meter.
        _mileage.StartJob();
        _log.LogInfo($"delivery started: {stops.Count} stops, " +
            $"{run.StoreKeys.Count} stores, goods €{run.PaidForGoods:0.00}");
        _host.HidePanel();
        PinStop();
    }

    /// <summary>
    /// The store legs' distance: the meter is shared with the touristen corner
    /// legs and those runs cannot overlap, so the poll feeds whichever is live.
    /// </summary>
    public void Advance(TelemetrySample sample, double dt) =>
        _meter.Add(sample.SpeedKmh, dt);

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
            bool moved = _city.Restock([stop.Key]);
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
    public double? FailForAccident(bool shielded = false)
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
        _log.LogWarning($"delivery run ended by accident: {run.Run.StoreKeys.Count} stores lost, " +
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
        _storeLegStarted = false;
        _mileage.CancelJob();
        _host.ClearGuidance();
    }

    // Pins the current leg on the companion, or skips a leg whose content lost
    // its address so the run can never stall on an unreachable stop. The first
    // store leg resets the meter: the pickup drive is not part of the delivery
    // distance charge.
    private void PinStop()
    {
        while (_job?.Current is { } stop)
        {
            if (!_storeLegStarted && stop.Kind == DeliveryStopKind.Store)
            {
                _meter.Reset();
                _storeLegStarted = true;
            }

            if (_host.PinStop(stop))
            {
                return;
            }

            _log.LogWarning(
                $"delivery stop skipped, no address: {stop.Name} ({stop.Key})");
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
        foreach (string key in run.StoreKeys)
        {
            if (_board.Catalog.Find(key) is not { } store)
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
