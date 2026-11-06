using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The fuel session (PORTPLAN items 37/38): EVO's own pump moves the tank, so
/// this collaborator prices the fills it notices — the daily market price, the
/// Free Tank Refill card, the station attendant's familiarity, the debt Unity's
/// <c>cash -= cost</c> allows — and notices a dry tank, which opens Unity's
/// repair page (the full tank at the pump's price plus the mechanic's service
/// fee) and fails the interrupted ride or delivery.
///
/// WHY it lives beside the job flows: a fill is not a job (it cannot be refused
/// or replayed) and it is not a shop interior (EVO has no such screen), so the
/// poll feeds it one reading per tick and it settles at most one fill.
///
/// EVO's shared memory is read-only, so the repair cannot put fuel back in the
/// tank. It prepays the next fill instead: the paid litres ride fuel.json and
/// are taken off the next pump bill for that car (PORTPLAN 38). A load or pit
/// top-up the game performs itself realises the credit and clears it.
/// </summary>
public sealed class FuelFlow
{
    // A tank within half a litre of its capacity is "full" for the purpose of
    // realising a prepaid fill: EVO's own top-ups land at the cap, while a
    // station purchase is consumed by the fill detector instead.
    private const double FullTankToleranceLitres = 0.5;

    private readonly FuelMarket _market;
    private readonly FuelFillTracker _fills;
    private readonly FuelRules _rules;
    private readonly GameClock _game;
    private readonly Garage _garage;
    private readonly IOnboardingStore _onboarding;
    private readonly IFriendshipStore _friendships;
    private readonly DriverWallet _wallet;
    private readonly Glovebox _glovebox;
    private readonly Trunk _trunk;
    private readonly VisitLedger _visits;
    private readonly GameStateSaver _saver;
    private readonly IRivalsService _rivals;
    private readonly IFuelPriceStore _prices;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    // What the tracker compares against to tell a real fill from a session
    // change: the car the garage says is driven, the track the game reports and
    // the physics packet counter (it rewinds when EVO reloads the session).
    private string? _lastCarKey;
    private string? _lastTrack;
    private int _lastPacketId = -1;

    // Where the driven car's odometer stood when this session was first seen:
    // a fill is priced only after the player has driven away from the spawn, so
    // the tank EVO tops up on load/restart is never billed.
    private double _sessionStartOdometerKm;
    private bool _sessionPrimed;

    // The tank reading of the previous tick: a drop to zero is the live run-dry
    // edge Unity's DrivingManager halts on. A session that opens on zero has no
    // edge (it only gets the road-service button, like Unity's map).
    private double _lastLitres = -1.0;
    private bool _outOfFuelLatched;

    // The roadside refuel's paid-for tank (PORTPLAN 38): the litres the repair
    // bought and the car they belong to. A missing car key or a non-positive
    // amount is "no credit" and never fuels another car for free.
    private double _prepaidLitres;
    private string? _prepaidCarKey;

    // The quote the open repair page was built from; null once it was paid (a
    // second tap must never charge twice).
    private FuelOutage? _outage;

    // WHY late: the host is the coordinator, built after this flow. Only a
    // settled fill or a run-dry edge reaches it, and the coordinator attaches
    // in its constructor.
    private IFuelHost _host = null!;

    public FuelFlow(
        FuelMarket market, FuelFillTracker fills, FuelRules rules, GameClock game,
        Garage garage, IOnboardingStore onboarding, IFriendshipStore friendships,
        DriverWallet wallet, Glovebox glovebox, Trunk trunk, VisitLedger visits,
        GameStateSaver saver,
        IRivalsService rivals, IFuelPriceStore prices, VoiceBeat beat, ILog log)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(fills);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(garage);
        ArgumentNullException.ThrowIfNull(onboarding);
        ArgumentNullException.ThrowIfNull(friendships);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(glovebox);
        ArgumentNullException.ThrowIfNull(trunk);
        ArgumentNullException.ThrowIfNull(visits);
        ArgumentNullException.ThrowIfNull(saver);
        ArgumentNullException.ThrowIfNull(rivals);
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(log);
        _market = market;
        _fills = fills;
        _rules = rules;
        _game = game;
        _garage = garage;
        _onboarding = onboarding;
        _friendships = friendships;
        _wallet = wallet;
        _glovebox = glovebox;
        _trunk = trunk;
        _visits = visits;
        _saver = saver;
        _rivals = rivals;
        _prices = prices;
        _beat = beat;
        _log = log;

        // The store is already warm from the market's own boot read, so this is
        // a cache hit, not a second disk read. A credit without a car key is a
        // corrupt/legacy row: drop it rather than ever fuelling a random tank.
        FuelPriceState? saved = _prices.Load();
        _prepaidLitres = saved is { PrepaidLitres: > 0 } && double.IsFinite(saved.PrepaidLitres)
            ? saved.PrepaidLitres
            : 0.0;
        _prepaidCarKey = _prepaidLitres > 0.0 ? saved?.PrepaidCarKey : null;
        if (_prepaidCarKey is null)
        {
            _prepaidLitres = 0.0;
        }
    }

    /// <summary>Attaches the coordinator that reveals the receipt and the
    /// run-dry repair page.</summary>
    public void Attach(IFuelHost host) => _host = host;

    /// <summary>The CR the road-service shortcut takes (Unity's flat 300).</summary>
    public double RoadServiceFee => _rules.RoadServiceFee;

    /// <summary>
    /// True while the driven car sits on an empty tank with no prepaid fill:
    /// Unity's map shows CALL ROAD SERVICE in exactly this state. The live
    /// run-dry page is the other branch of the same rule; a repair clears this
    /// flag because the tank is already paid for.
    /// </summary>
    public bool IsStranded { get; private set; }

    /// <summary>Whether the wallet can take the road-service charge (Unity's
    /// <c>cash &lt; 300</c> refusal; unlike the forced repair this is a
    /// voluntary purchase and can never create debt).</summary>
    public bool CanAffordRoadService => _wallet.Cash >= _rules.RoadServiceFee;

    /// <summary>
    /// The daily drift (Unity's midnight roll), ticked with the other game-clock
    /// work so it also advances while EVO is closed. A roll is one uniform
    /// change and one file write, never a per-poll cost.
    /// </summary>
    public void Tick()
    {
        if (!_market.RollTo(_game.GameDay))
        {
            return;
        }

        SaveFuelState();
        _log.LogInfo(
            $"[Fuel] price CR {_market.PricePerLitre:0.00}/L for game day {_game.GameDay}");
    }

    /// <summary>
    /// Feeds one live sample. Only a fill the player drove to is charged and
    /// reported: EVO tops the tank up on load, on restart and at the pit box,
    /// and the temporary freeroam map can spawn the car with a fresh tank, so
    /// those refuels are re-primed instead of billed. The introduction drives
    /// are free too, the same gate the accident and breakdown rolls use.
    /// </summary>
    public void Observe(TelemetrySample sample, DateTime now)
    {
        if (!_onboarding.Load().IsComplete)
        {
            IsStranded = false;
            _lastLitres = -1.0;
            return;
        }

        if (sample.FuelLitres is not { } litres || _garage.Current is not { } car)
        {
            // No official fuel field (graphics block missing/torn) or no car to
            // bill: nothing can be priced. The tracker keeps its state so a
            // one-frame graphic hiccup does not split a real fill in two.
            return;
        }

        bool newSession = IsNewSession(sample);
        if (newSession || !_sessionPrimed)
        {
            _sessionPrimed = true;
            _sessionStartOdometerKm = car.Odometer;
        }

        double tank = _garage.SpecOf(car)?.TankLitres ?? 0.0;
        // A load/restart/pit service tops the tank up for free. If a roadside
        // repair had prepaid that fill, the game just delivered it: clear the
        // credit so it cannot pay for a second tank later. A station fill is
        // left to the fill tracker, which splits it properly.
        if ((newSession || sample.InPit) && IsFull(litres, tank))
        {
            ClearPrepaid(car.Key);
        }

        // A tank that was topped up at the pit box (load/restart/pit service)
        // is not a purchase, and neither is a jump in the first metres of a
        // session: the player has to have driven away from the spawn before a
        // fill counts. Re-priming here drops any accumulation started under
        // the pit flag, so a stop that begins in the box is never billed even
        // when the car leaves before the level settles.
        double drivenKm = car.Odometer - _sessionStartOdometerKm;
        if (drivenKm < 0.0)
        {
            // An engine swap can reset the odometer; start the count again.
            _sessionStartOdometerKm = car.Odometer;
            drivenKm = 0.0;
        }
        bool fillGate = sample.InPit
            || drivenKm < (_rules.MinDriveBeforeFillMetres / 1000.0);

        UpdateOutOfFuel(litres, car, tank, newSession, sample.InPit);
        _lastLitres = litres;

        if (fillGate)
        {
            _fills.Reset();
            return;
        }

        if (_fills.Observe(litres, now, newSession) is { } fill)
        {
            Settle(fill, car);
        }
    }

    /// <summary>
    /// Unity's RepairScreen confirm: the mechanic's bill is taken in full (the
    /// balance may go into debt, like <c>cash -= cost</c>), the interrupted
    /// ride or delivery is failed by the host, and the paid-for litres wait for
    /// the next fill. A second tap after the quote cleared is a no-op.
    /// </summary>
    public void RepairOutOfFuel()
    {
        if (_outage is not { } quote)
        {
            return;
        }
        _outage = null;

        // Unity RepairScreen's free branch: a Free Car Repair card is consumed
        // on the confirm and zeroes the bill (the fuel is still delivered).
        bool free = _glovebox.TryConsume(RewardCardEffect.FreeCarRepair, _trunk);
        double charged = free ? 0.0 : quote.RepairCost;
        _wallet.ChargeInFull(charged);
        _prepaidLitres = quote.LitresNeeded;
        _prepaidCarKey = _garage.CurrentKey;
        if (_prepaidCarKey is null)
        {
            _prepaidLitres = 0.0;
        }
        _outOfFuelLatched = true;
        IsStranded = false;
        SaveFuelState();

        // Unity charges first, then fails the job (MapScreen.OnReturn), so a
        // Delivery Insurance refund lands after the repair bill.
        _host.FailJobForFuelOut();
        _saver.SaveDriver(_rivals.LastResult);
        _log.LogWarning(
            $"[FuelOut] roadside refuel CR {charged:0.00}" +
            $"{(free ? " (free repair card)" : "")} " +
            $"({quote.LitresNeeded:0.0} L at CR {quote.PricePerLitre:0.00}/L " +
            $"+ CR {quote.ServiceFee:0.00} service), cash {_wallet.Cash:0.00}, " +
            $"prepaid {_prepaidLitres:0.0} L for {_prepaidCarKey}");

        _host.ResumeAfterFuelOut();
    }

    /// <summary>
    /// Unity's CALL ROAD SERVICE: a flat 300 CR, refused when the wallet cannot
    /// cover it. The tank is prepaid and the map lets the player go; the taxi
    /// job (Unity's only job at that point) is failed by the caller.
    /// </summary>
    public bool CallRoadService()
    {
        if (!IsStranded || _garage.Current is not { } car
            || !_wallet.TrySpend(_rules.RoadServiceFee))
        {
            return false;
        }

        double tank = _garage.SpecOf(car)?.TankLitres ?? 0.0;
        _prepaidLitres = Math.Max(0.0, tank);
        _prepaidCarKey = car.Key;
        _outOfFuelLatched = true;
        IsStranded = false;
        SaveFuelState();
        _log.LogWarning(
            $"[FuelOut] road service CR {_rules.RoadServiceFee:0.00}, " +
            $"prepaid {_prepaidLitres:0.0} L for {car.Key}, cash {_wallet.Cash:0.00}");
        return true;
    }

    // A fill is paid in full even when the cash cannot cover it: the fuel is
    // already in the tank, so Unity's cash -= cost applies and the balance may
    // go into debt (DriverWallet.ChargeInFull). The card, the friendship and
    // the receipt follow Unity's FuelStationScreen order; the roadside
    // repair's prepaid litres come off the bill first.
    private void Settle(FuelFill fill, OwnedCar car)
    {
        FuelPrepayResult prepay = FuelPrepayment.Apply(
            fill.Litres, _prepaidLitres, _prepaidCarKey, car.Key);
        if (prepay.PrepaidLitres > 0.0)
        {
            _prepaidLitres = Math.Max(0.0, _prepaidLitres - prepay.PrepaidLitres);
            if (_prepaidLitres <= 0.0)
            {
                _prepaidCarKey = null;
            }
        }

        // The Free Tank Refill card only pays a bill that is still there: a
        // fill the roadside repair already covered must not burn the card.
        bool free = prepay.BillableLitres > 0.0
            && _glovebox.TryConsume(RewardCardEffect.FreeTankRefill, _trunk);
        FriendshipResult? friendship = AwardFriendship();
        FuelReceipt receipt = FuelReceiptBuilder.Build(
            fill.Litres, _market.PricePerLitre, free, friendship, prepay.PrepaidLitres);

        // WHY the visit is stamped here: the ToDo list's fuel-station tip
        // (PORTPLAN 46) has no forecourt door to enter, so the fill the player
        // drove to is the visit. It is set before the save so a kill between
        // the receipt and the next throttle never forgets the tip.
        _visits.MarkFuelStation();
        _wallet.ChargeInFull(receipt.Total);
        // Bank first, then report: a kill between the receipt and the next
        // throttle would otherwise forget the charge, the prepaid split and the
        // familiarity.
        SaveFuelState();
        _saver.SaveDriver(_rivals.LastResult);

        _log.LogInfo(
            $"[Fuel] {receipt.Litres:0.0} L at CR {receipt.PricePerLitre:0.00}/L " +
            $"= CR {receipt.Total:0.00}" +
            $"{(receipt.HasPrepaid ? $" ({receipt.PrepaidLitres:0.0} L prepaid)" : "")}" +
            $"{(free ? " (free card)" : "")}, cash {_wallet.Cash:0.00}");

        _host.ShowFuelReceipt(new FuelReceiptScreenViewModel(
            receipt, _wallet, _beat, new RelayCommand(_host.ResumeAfterFuelReceipt)));
    }

    // The live run-dry edge (Unity DrivingManager's onFuelOut): only a reading
    // that fell from above zero opens the repair page. A session that starts
    // dry has no edge, exactly like Unity's map on a reload, and leaves the
    // road-service button to the control page.
    private void UpdateOutOfFuel(
        double litres, OwnedCar car, double tank, bool newSession, bool inPit)
    {
        bool prepaid = IsPrepaidFor(car.Key);
        IsStranded = litres <= 0.0 && !prepaid;
        if (litres > 0.0)
        {
            _outOfFuelLatched = false;
            return;
        }

        if (!IsStranded)
        {
            // A prepaid empty tank is already paid for; no page, no road
            // service. The next fill realises the credit.
            _outOfFuelLatched = true;
            return;
        }

        if (!_outOfFuelLatched && !newSession && !inPit && _lastLitres > 0.0)
        {
            _outOfFuelLatched = true;
            ShowFuelOut(litres, car, tank);
            return;
        }

        // The first reading of a session (or a stop at a pump) has no run-dry
        // edge: latch it so a later tick cannot mistake the same empty tank for
        // a fresh one. The control page offers the road service instead.
        _outOfFuelLatched = true;
    }

    private void ShowFuelOut(double litres, OwnedCar car, double tank)
    {
        CarSpec? spec = _garage.SpecOf(car);
        FuelOutage quote = FuelOutage.Quote(
            tank, litres, _market.PricePerLitre, _rules.OutOfFuelServiceFee);
        _outage = quote;

        _log.LogWarning(
            $"[FuelOut] dry tank at {car.Odometer:0.0} km: {quote.LitresNeeded:0.0} L " +
            $"to full at CR {quote.PricePerLitre:0.00}/L + CR {quote.ServiceFee:0.00} " +
            $"service = CR {quote.RepairCost:0.00}");

        _host.ShowFuelOut(new FuelOutScreenViewModel(
            quote, CarTitles.Of(car.Key, spec), _wallet, _beat,
            new RelayCommand(RepairOutOfFuel),
            freeRepair: _glovebox.Carries(RewardCardEffect.FreeCarRepair, _trunk)));
    }

    private bool IsPrepaidFor(string? carKey) =>
        _prepaidLitres > 0.0 && carKey is not null
        && string.Equals(_prepaidCarKey, carKey, StringComparison.Ordinal);

    private void ClearPrepaid(string carKey)
    {
        if (!IsPrepaidFor(carKey))
        {
            return;
        }

        _prepaidLitres = 0.0;
        _prepaidCarKey = null;
        SaveFuelState();
        _log.LogInfo("[Fuel] prepaid tank realised by the game's own top-up");
    }

    private static bool IsFull(double litres, double tank) =>
        tank > 0.0 && litres >= tank - FullTankToleranceLitres;

    private void SaveFuelState() =>
        _prices.Save(_market.Snapshot() with
        {
            PrepaidLitres = _prepaidLitres,
            PrepaidCarKey = _prepaidCarKey,
        });

    // Unity's FuelStationScreen increments the POI's friendship level on the
    // fill; the store's one "+1 like a completed ride" verb is that same step,
    // so the receipt prints the level and the band the player reached.
    private FriendshipResult? AwardFriendship()
    {
        string owner = _rules.OwnerName.Trim();
        if (owner.Length == 0)
        {
            return null;
        }

        FriendshipOutcome outcome = _friendships.RegisterRide(owner);
        return new FriendshipResult(outcome.OwnerName, outcome.Level, outcome.TierIncreased);
    }

    // Three ways a reading can belong to a different session than the previous
    // one; all of them re-prime the tracker instead of pricing the jump.
    private bool IsNewSession(TelemetrySample sample)
    {
        string? track = sample.Track?.Name;
        bool reset = _lastTrack is not null
            && !string.Equals(track, _lastTrack, StringComparison.Ordinal);
        if (track is { Length: > 0 })
        {
            _lastTrack = track;
        }

        if (_lastPacketId >= 0 && sample.PacketId < _lastPacketId)
        {
            reset = true;
        }
        _lastPacketId = sample.PacketId;

        string? car = _garage.Current?.Key;
        if (!string.Equals(car, _lastCarKey, StringComparison.Ordinal))
        {
            _lastCarKey = car;
            reset = true;
        }

        return reset;
    }
}
