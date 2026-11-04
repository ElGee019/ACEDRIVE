using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The fuel receipt session (PORTPLAN item 37): EVO's own pump moves the tank,
/// so this collaborator prices the fill it notices — the daily market price,
/// the Free Tank Refill card, the station attendant's familiarity, the debt
/// Unity's <c>cash -= cost</c> allows, and the page that reports it.
///
/// WHY it lives beside the job flows: a fill is not a job (it cannot be refused
/// or replayed) and it is not a shop interior (EVO has no such screen), so the
/// poll feeds it one reading per tick and it settles at most one fill.
/// </summary>
public sealed class FuelFlow
{
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

    // WHY late: the host is the coordinator, built after this flow. Only a
    // settled fill reaches it, and the coordinator attaches in its constructor.
    private IFuelHost _host = null!;

    public FuelFlow(
        FuelMarket market, FuelFillTracker fills, FuelRules rules, GameClock game,
        Garage garage, IOnboardingStore onboarding, IFriendshipStore friendships,
        DriverWallet wallet, Glovebox glovebox, Trunk trunk, GameStateSaver saver,
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
        _saver = saver;
        _rivals = rivals;
        _prices = prices;
        _beat = beat;
        _log = log;
    }

    /// <summary>Attaches the coordinator that reveals the receipt.</summary>
    public void Attach(IFuelHost host) => _host = host;

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

        _prices.Save(_market.Snapshot());
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
        if (sample.InPit || drivenKm < (_rules.MinDriveBeforeFillMetres / 1000.0))
        {
            _fills.Reset();
            return;
        }

        if (_fills.Observe(litres, now, newSession) is { } fill)
        {
            Settle(fill);
        }
    }

    // A fill is paid in full even when the cash cannot cover it: the fuel is
    // already in the tank, so Unity's cash -= cost applies and the balance may
    // go into debt (DriverWallet.ChargeInFull). The card, the friendship and
    // the receipt follow Unity's FuelStationScreen order.
    private void Settle(FuelFill fill)
    {
        bool free = _glovebox.TryConsume(RewardCardEffect.FreeTankRefill, _trunk);
        FriendshipResult? friendship = AwardFriendship();
        FuelReceipt receipt = FuelReceiptBuilder.Build(
            fill.Litres, _market.PricePerLitre, free, friendship);

        _wallet.ChargeInFull(receipt.Total);
        // Bank first, then report: a kill between the receipt and the next
        // throttle would otherwise forget the charge and the familiarity.
        _saver.SaveDriver(_rivals.LastResult);

        _log.LogInfo(
            $"[Fuel] {receipt.Litres:0.0} L at CR {receipt.PricePerLitre:0.00}/L " +
            $"= CR {receipt.Total:0.00}{(free ? " (free card)" : "")}, " +
            $"cash {_wallet.Cash:0.00}");

        _host.ShowFuelReceipt(new FuelReceiptScreenViewModel(
            receipt, _wallet, _beat, new RelayCommand(_host.ResumeAfterFuelReceipt)));
    }

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
