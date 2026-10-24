using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the Germany-zone name pools and passenger suffixes from
/// Data/name_pools.json next to the exe. Missing or invalid data degrades to
/// <see cref="NamePoolTable.Default"/> with a warning — the overlay must
/// never fail to start over a data file (AGENTS.md: Try* at every boundary).
/// </summary>
public sealed class JsonNamePoolStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonNamePoolStore(ILog log)
        : this(DataPaths.GameData("name_pools.json"), log)
    { }

    public JsonNamePoolStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public NamePoolTable Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "name_pools.json", "using built-in defaults",
                IsUsable, out NamePoolTable? table))
        {
            return NamePoolTable.Default;
        }

        _log.LogInfo($"name pools loaded: {table.Pools.Count} pools, {table.Suffixes.Count} suffixes");
        return table;
    }

    // Every pool must be drawable: a non-positive weight or an empty name
    // list would hand the generator a zero-total roulette or an index that
    // never resolves. One empty suffix table would do the same for draws.
    private static bool IsUsable(NamePoolTable table)
    {
        if (table.Pools.Count == 0 || table.Suffixes.Count == 0)
        {
            return false;
        }

        foreach (NamePool pool in table.Pools)
        {
            if (pool.Weight <= 0 || pool.FirstNames.Count == 0 || pool.Surnames.Count == 0)
            {
                return false;
            }
        }

        return true;
    }
}
