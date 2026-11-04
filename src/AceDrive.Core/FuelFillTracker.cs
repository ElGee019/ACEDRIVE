// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Turns EVO's live fuel level into one settled fill (PORTPLAN item 37).
namespace AceDrive.Core;

/// <summary>
/// Watches the fuel level EVO reports and reports one fill when it ends. The
/// game's own pump does the fuelling, so the overlay never asks for an amount:
/// a run of rising samples accumulates, and the fill settles once the level has
/// stood still for the authored window (or dropped because the player drove
/// off). A session change (new track, a reload, another car) re-primes the
/// baseline instead of billing the jump — a fresh session can start with any
/// tank and that is not a purchase.
/// </summary>
public sealed class FuelFillTracker
{
    // WHY a tolerance: the physics block reports a float that jitters in the
    // last digits; 0.02 L is far below a real pump tick and far above a torn
    // frame, so neither starts nor splits a fill.
    private const double LevelEpsilon = 0.02;

    private readonly double _minLitres;
    private readonly double _settleSeconds;

    private double _level;
    private double _pending;
    private bool _primed;
    private bool _filling;
    private DateTime _lastRise;

    public FuelFillTracker(FuelRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _minLitres = rules.MinFillLitres;
        _settleSeconds = rules.SettleSeconds;
    }

    /// <summary>
    /// Feeds one live reading. Returns a fill only on the sample that settles
    /// it; every other sample answers null. <paramref name="sessionReset"/>
    /// makes the reading the new baseline without pricing it.
    /// </summary>
    public FuelFill? Observe(double litres, DateTime now, bool sessionReset)
    {
        if (sessionReset)
        {
            Reset();
        }

        // A NaN or a negative reading is a torn/foreign frame: keep the baseline
        // and wait for the next 100 ms sample rather than invent a fill.
        if (!double.IsFinite(litres) || litres < 0.0)
        {
            return null;
        }

        if (!_primed)
        {
            _level = litres;
            _primed = true;
            return null;
        }

        double delta = litres - _level;
        _level = litres;

        if (delta > LevelEpsilon)
        {
            _pending += delta;
            _lastRise = now;
            _filling = true;
            return null;
        }

        if (!_filling)
        {
            return null;
        }

        // The pump stopped: either the level held still for the settle window
        // (a fill can stall at a nearly full tank) or it fell because the car
        // drove off. Both end the fill on this sample, so a fill is never lost
        // to the first metre driven after it.
        if (delta < -LevelEpsilon || now - _lastRise >= TimeSpan.FromSeconds(_settleSeconds))
        {
            return Take();
        }

        return null;
    }

    /// <summary>Forgets the baseline: the next reading primes the tracker
    /// without being priced as a fill.</summary>
    public void Reset()
    {
        _primed = false;
        _filling = false;
        _pending = 0.0;
    }

    private FuelFill? Take()
    {
        double litres = Math.Round(_pending, 2, MidpointRounding.AwayFromZero);
        _pending = 0.0;
        _filling = false;
        // A trickle under the authored minimum is a leak/torn read, not a visit
        // to the pump: no receipt, no charge.
        return litres >= _minLitres ? new FuelFill(litres) : null;
    }
}
