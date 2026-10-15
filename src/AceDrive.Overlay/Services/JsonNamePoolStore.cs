using System.IO;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonNamePoolStore(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "name_pools.json"), log)
    { }

    public JsonNamePoolStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public NamePoolTable Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"name_pools.json missing at {_file}; using built-in defaults");
                return NamePoolTable.Default;
            }
            NamePoolTable? table = JsonSerializer.Deserialize<NamePoolTable>(
                File.ReadAllText(_file), JsonOptions);
            if (!IsUsable(table))
            {
                _log.LogWarning($"name_pools.json invalid at {_file}; using built-in defaults");
                return NamePoolTable.Default;
            }
            _log.LogInfo($"name pools loaded: {table!.Pools.Count} pools, {table.Suffixes.Count} suffixes");
            return table;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down. Anything else stays a crash.
            _log.LogWarning($"name_pools.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return NamePoolTable.Default;
        }
    }

    // Every pool must be drawable: a non-positive weight or an empty name
    // list would hand the generator a zero-total roulette or an index that
    // never resolves. One empty suffix table would do the same for draws.
    private static bool IsUsable(NamePoolTable? table)
    {
        if (table is null || table.Pools.Count == 0 || table.Suffixes.Count == 0)
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
