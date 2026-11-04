using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The fuel market in %AppData%\AceDrive\fuel.json. Never throws: a missing
/// file is a first run (Unity's 1.65 seed), and a failed write must not break
/// gameplay or shutdown. Load caches what Save refreshes, so the one read is
/// paid at boot — the market owns the live price afterwards.
/// </summary>
public sealed class JsonFuelPriceStore : IFuelPriceStore
{
    private readonly string _file;
    private readonly ILog _log;
    private readonly object _gate = new();
    private FuelPriceState? _current;
    private bool _loaded;

    public JsonFuelPriceStore(ILog log)
        : this(DataPaths.UserSave("fuel.json"), log)
    { }

    public JsonFuelPriceStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public FuelPriceState? Load()
    {
        lock (_gate)
        {
            if (_loaded) return _current;
            _loaded = true;
            _current = Read();
            return _current;
        }
    }

    public void Save(FuelPriceState state)
    {
        // A failed bank must never break gameplay or shutdown; the market keeps
        // the price in memory and the next day flip retries the write.
        if (!JsonFile.TryWrite(_file, state, out string? error))
        {
            _log.LogWarning(
                $"fuel.json unwritable at {_file} ({error}); price kept in memory");
        }

        lock (_gate)
        {
            _current = state;
            _loaded = true;
        }
    }

    private FuelPriceState? Read()
    {
        JsonReadStatus status =
            JsonFile.TryRead(_file, out FuelPriceState? state, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return null;
        }

        // A corrupt or hand-edited row degrades to the shipped seed price, never
        // to an exception at the pump (AGENTS.md: Try* at every external edge).
        if (state is null || !IsPlausible(state))
        {
            _log.LogWarning(
                $"fuel.json {JsonFile.Describe(error)} at {_file}; " +
                "starting from the shipped price");
            return null;
        }

        _log.LogInfo(
            $"fuel price resumed: CR {state.PricePerLitre:0.00}/L (day {state.LastRollDay})");
        return state;
    }

    private static bool IsPlausible(FuelPriceState state) =>
        double.IsFinite(state.PricePerLitre)
        && state.PricePerLitre > 0.0
        && state.LastRollDay >= 0;
}
