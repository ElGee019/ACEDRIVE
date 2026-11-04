// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Port of Unity's daily fuel-price drift (GameClockManager.UpdateFuelPrice).
namespace AceDrive.Core;

/// <summary>
/// The pump price (PORTPLAN item 37): one global CR/L that drifts by a uniform
/// window once per game day and is clamped to the authored band, exactly as
/// Unity's midnight roll does. The seeded stream and the last-roll day live
/// here, so a restart continues yesterday's price instead of re-seeding it and
/// a test can pin the sequence.
/// </summary>
public sealed class FuelMarket
{
    // WHY a cap: a save whose day stamp is years behind (a hand-edited file, a
    // clock moved back) must not pay thousands of rolls at boot. Unity would
    // have rolled at each midnight it lived through; a month is the band that
    // still reads as "it kept drifting while the overlay was closed".
    private const int MaxCatchUpDays = 30;

    private readonly FuelRules _rules;
    private readonly Random _rng;

    public FuelMarket(FuelRules rules, FuelPriceState? saved, int gameDay, int seed)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
        _rng = new Random(seed);

        // A first launch opens on Unity's PlayerDefaults 1.65 and only rolls at
        // the first midnight it sees. A resumed save keeps its price and its
        // day, clamped and never ahead of the clock (a rewind would otherwise
        // freeze the drift until the old stamp passed).
        bool resumed = saved is { } state && IsPlausible(state);
        PricePerLitre = resumed
            ? _rules.Clamp(saved!.PricePerLitre)
            : _rules.Clamp(_rules.StartPricePerLitre);
        LastRollDay = resumed ? Math.Min(saved!.LastRollDay, gameDay) : gameDay;
    }

    /// <summary>The CR/L charged right now, already inside the clamp band.</summary>
    public double PricePerLitre { get; private set; }

    /// <summary>The game day the price last moved on.</summary>
    public int LastRollDay { get; private set; }

    /// <summary>
    /// One roll per elapsed game day (Unity's midnight edge): a uniform change
    /// inside the authored window, clamped, then persisted by the caller.
    /// Returns true only when the price actually moved, so the file is written
    /// on the day flip and never per poll.
    /// </summary>
    public bool RollTo(int gameDay)
    {
        if (gameDay <= LastRollDay)
        {
            return false;
        }

        int days = Math.Min(gameDay - LastRollDay, MaxCatchUpDays);
        for (int i = 0; i < days; i++)
        {
            double change = _rules.DailyChangeMin
                + (_rng.NextDouble() * (_rules.DailyChangeMax - _rules.DailyChangeMin));
            PricePerLitre = _rules.Clamp(PricePerLitre + change);
        }

        LastRollDay = gameDay;
        return true;
    }

    /// <summary>The reading the price store banks.</summary>
    public FuelPriceState Snapshot() => new(PricePerLitre, LastRollDay);

    // A saved price outside the band is a hand-edited or foreign file: start
    // over rather than charge a fortune per litre.
    private static bool IsPlausible(FuelPriceState state) =>
        double.IsFinite(state.PricePerLitre)
        && state.PricePerLitre > 0.0
        && state.LastRollDay >= 0;
}
