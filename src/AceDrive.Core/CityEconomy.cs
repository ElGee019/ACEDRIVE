// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 25 (store
// inventory + citizen demand), port of Unity StoreManager.OnMinuteTick and
// StoreEconomyConfigSO.
namespace AceDrive.Core;

/// <summary>
/// The living town: every store's citizen count drifts with how full its
/// shelves are, and citizen demand empties those shelves once the delivery
/// career exists (Unity gates the pulls on an owned wholesale hub).
///
/// Unity drove this from a per-minute clock event; here elapsed game minutes
/// arrive from <see cref="GameClock"/>'s moment — real time through IClock,
/// never poll counts — and only whole ticks are paid out, the remainder
/// carried, so a lagged or a fast clock keeps the exact cadence. All work
/// happens on tick boundaries, never in the 10 Hz poll.
/// </summary>
public sealed class CityEconomy
{
    private const int MinutesPerHour = 60;

    private readonly CityRules _rules;
    private readonly List<DeliveryStore> _board = [];
    private readonly List<StoreState> _cells = [];
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    // Sentinel start, like the idle and wake stamps in MainViewModel: the
    // town begins living at the first game moment it is shown, so a new
    // session never re-pays the hours an earlier one already banked.
    private DateTime _since = DateTime.MinValue;
    private int _citizenCarry;
    private int _demandCarry;

    public CityEconomy(
        IReadOnlyList<DeliveryStore> board,
        CityRules rules,
        IReadOnlyList<StoreState>? saved = null)
    {
        _rules = rules;
        foreach (DeliveryStore store in board)
        {
            _index[store.Key] = _cells.Count;
            _board.Add(store);
            _cells.Add(Seed(store, saved));
        }
    }

    /// <summary>
    /// Adds one store that opened after the town was built (a placed
    /// grocery/coffee shop). The cell starts on the row's own shelf fill,
    /// exactly like a store present at boot; an existing key changes nothing.
    /// Returns whether a cell was added.
    /// </summary>
    public bool AddStore(DeliveryStore store)
    {
        if (store is null
            || string.IsNullOrWhiteSpace(store.Key)
            || _index.ContainsKey(store.Key))
        {
            return false;
        }

        _index[store.Key] = _cells.Count;
        _board.Add(store);
        _cells.Add(Seed(store, null));
        return true;
    }

    /// <summary>
    /// Moves the town up to a game moment: citizens adjust every
    /// <see cref="CityRules.CitizenTickMinutes"/> game minutes, demand pulls
    /// every <see cref="CityRules.DepletionTickMinutes"/> once
    /// <paramref name="hasDeliveryCareer"/> is true. Returns whether anything
    /// moved, which is the transition worth logging — half citizens only
    /// bank, so most calls report nothing.
    /// </summary>
    public bool AdvanceTo(DateTime gameNow, bool hasDeliveryCareer)
    {
        if (_since == default)
        {
            _since = gameNow;
            return false;
        }

        int elapsed = (int)(gameNow - _since).TotalMinutes;
        if (elapsed < 1)
        {
            return false;
        }
        // Whole minutes only: the leftover is owed to the next call.
        _since = _since.AddMinutes(elapsed);

        int growthTicks = TakeTicks(ref _citizenCarry, elapsed, _rules.CitizenTickMinutes);
        // Demand never accrues while the career is missing, so buying the
        // first hub cannot dump every missed interval at once (Unity's gate).
        int demandTicks;
        if (hasDeliveryCareer)
        {
            demandTicks = TakeTicks(ref _demandCarry, elapsed, _rules.DepletionTickMinutes);
        }
        else
        {
            _demandCarry = 0;
            demandTicks = 0;
        }

        // Citizens first, then demand, like one Unity minute tick: the ladder
        // count reads the citizens this minute just produced.
        bool grew = growthTicks > 0 && ApplyGrowth(growthTicks);
        bool depleted = demandTicks > 0 && ApplyDemand(demandTicks);
        return grew || depleted;
    }

    /// <summary>Sum of the given stores' citizens (Unity GetTotalCitizens).
    /// The caller decides which stores count: the delivery pages pass the
    /// connected ones, so a career that is not opened yet is not on the map.</summary>
    public int TotalCitizens(IReadOnlyList<DeliveryStore> stores)
    {
        int total = 0;
        foreach (DeliveryStore store in stores)
        {
            int position = IndexOf(store.Key);
            if (position >= 0)
            {
                total += _cells[position].Citizens;
            }
        }
        return total;
    }

    /// <summary>Header growth of the given stores, citizens per game hour
    /// (Unity GetTotalGrowthDeltaPerHour).</summary>
    public int TotalGrowthPerHour(IReadOnlyList<DeliveryStore> stores)
    {
        int total = 0;
        foreach (DeliveryStore store in stores)
        {
            total += GrowthPerHour(store);
        }
        return total;
    }

    /// <summary>What the row and the header print: this store's band delta
    /// scaled to an hour. Derived from the tick delta rather than Unity's
    /// second table of display values, so the number shown and the citizens
    /// actually banked can never disagree.</summary>
    public int GrowthPerHour(DeliveryStore store)
    {
        int position = IndexOf(store.Key);
        if (position < 0)
        {
            return 0;
        }
        double delta = Delta(store.PercentOf(_cells[position].StockedUnits));
        return (int)Math.Round(
            delta * MinutesPerHour / _rules.CitizenTickMinutes, MidpointRounding.AwayFromZero);
    }

    /// <summary>The board as it now stands: catalog rows carrying the town's
    /// live shelf count, so need/percent/band/cost math reads real stock
    /// instead of the printed starting fill. Order is preserved.</summary>
    public IReadOnlyList<DeliveryStore> Live(IReadOnlyList<DeliveryStore> stores)
    {
        var live = new List<DeliveryStore>(stores.Count);
        foreach (DeliveryStore store in stores)
        {
            live.Add(Live(store));
        }
        return live;
    }

    /// <summary>One catalog row with this store's live shelf count.</summary>
    public DeliveryStore Live(DeliveryStore store)
    {
        int position = IndexOf(store.Key);
        return position < 0 ? store : store with { StockedUnits = _cells[position].StockedUnits };
    }

    /// <summary>What a drop-off leaves behind: the given stores' shelves fill
    /// to capacity (Unity's delivery restock). No separate growth call is
    /// needed — the band is read off the fill at every citizen tick, so the
    /// town follows the restock on its own. Returns whether a shelf actually
    /// moved: an already-full store (or a key the board never held) says
    /// nothing, which is what keeps transition-only logging honest.</summary>
    public bool Restock(IReadOnlyList<string> storeKeys)
    {
        bool moved = false;
        foreach (string key in storeKeys)
        {
            int position = IndexOf(key);
            if (position < 0)
            {
                continue;
            }
            StoreState cell = _cells[position];
            int capacity = _board[position].UnitCapacity;
            if (cell.StockedUnits != capacity)
            {
                _cells[position] = cell with { StockedUnits = capacity };
                moved = true;
            }
        }
        return moved;
    }

    /// <summary>What the next session reopens: one cell per store, copied so
    /// the saved view cannot mutate with a later tick. Exit-only, like Unity's
    /// storeStates, which are never written mid-session.</summary>
    public IReadOnlyList<StoreState> Snapshot() => [.. _cells];

    private static int TakeTicks(ref int carry, int elapsed, int interval)
    {
        carry += elapsed;
        int ticks = carry / interval;
        carry -= ticks * interval;
        return ticks;
    }

    private bool ApplyGrowth(int ticks)
    {
        bool moved = false;
        for (int i = 0; i < _cells.Count; i++)
        {
            StoreState cell = _cells[i];
            double delta = Delta(_board[i].PercentOf(cell.StockedUnits)) * ticks;
            if (delta == 0)
            {
                continue;
            }

            // Unity: bank the fraction, pay out whole citizens only, and never
            // let a declining store drop under the floor.
            double accumulator = cell.GrowthAccumulator + delta;
            int citizens = cell.Citizens;
            if (accumulator >= 1)
            {
                int gain = (int)accumulator;
                citizens += gain;
                accumulator -= gain;
            }
            else if (accumulator <= -1)
            {
                int loss = (int)(-accumulator);
                citizens = Math.Max(_rules.MinCitizens, citizens - loss);
                accumulator += loss;
            }

            moved |= citizens != cell.Citizens;
            _cells[i] = cell with { Citizens = citizens, GrowthAccumulator = accumulator };
        }
        return moved;
    }

    private bool ApplyDemand(int ticks)
    {
        bool moved = false;
        for (int i = 0; i < _cells.Count; i++)
        {
            StoreState cell = _cells[i];
            // Unity removes one item per demand unit, capped at what the
            // shelves hold; with the slots collapsed to unit counts that cap
            // is the whole rule, and citizens do not move during a pull.
            int units = Math.Min(DemandUnits(cell.Citizens) * ticks, cell.StockedUnits);
            if (units > 0)
            {
                _cells[i] = cell with { StockedUnits = cell.StockedUnits - units };
                moved = true;
            }
        }
        return moved;
    }

    // Unity's triangular demand ladder: each extra unit a store sells off
    // needs more customers than the previous one (0-199 citizens -> 1 unit,
    // 200-499 -> 2, 500-899 -> 3, 900-1399 -> 4, ...).
    private int DemandUnits(int citizens)
    {
        int units = 1;
        int step = _rules.DepletionFirstCitizens;
        int threshold = step;
        while (citizens >= threshold)
        {
            units++;
            step += _rules.DepletionStepCitizens;
            threshold += step;
        }
        return units;
    }

    // Unity's band chain, comparison for comparison: the upper edges are
    // inclusive, the lower two exclusive.
    private double Delta(double percent)
    {
        CityRules r = _rules;
        if (percent >= r.HighPercent)
        {
            return r.HighDelta;
        }
        if (percent >= r.MediumPercent)
        {
            return r.MediumDelta;
        }
        if (percent > r.LowPercent)
        {
            return r.NeutralDelta;
        }
        return percent > r.VeryLowPercent ? r.LowDelta : r.VeryLowDelta;
    }

    // A store with no saved cell joins at the rules' start value on its
    // printed shelf fill. Saved numbers are trusted but clamped: content may
    // shrink a capacity between sessions, and nothing may start under the
    // floor or on a negative shelf.
    private StoreState Seed(DeliveryStore store, IReadOnlyList<StoreState>? saved)
    {
        StoreState? cell = Find(saved, store.Key);
        if (cell is null)
        {
            return new StoreState(store.Key, _rules.DefaultCitizens, store.StockedUnits, 0);
        }

        return cell with
        {
            Citizens = Math.Max(_rules.MinCitizens, cell.Citizens),
            StockedUnits = Math.Clamp(cell.StockedUnits, 0, store.UnitCapacity),
        };
    }

    private static StoreState? Find(IReadOnlyList<StoreState>? saved, string key)
    {
        if (saved is null)
        {
            return null;
        }
        foreach (StoreState cell in saved)
        {
            if (cell.Key == key)
            {
                return cell;
            }
        }
        return null;
    }

    // Position of a store's cell, or -1 when the board never held that key:
    // callers that read a number treat the unknown store as no contribution.
    private int IndexOf(string key) =>
        _index.TryGetValue(key, out int position) ? position : -1;
}
