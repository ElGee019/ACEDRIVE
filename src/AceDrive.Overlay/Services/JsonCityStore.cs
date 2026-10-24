using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the town under %AppData%\AceDrive. Never throws: a missing or
/// corrupt file means a freshly seeded city, and a failed write must never
/// break gameplay or shutdown.
/// </summary>
public sealed class JsonCityStore : ICityStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonCityStore(ILog log)
        : this(DataPaths.UserSave("city.json"), log)
    { }

    public JsonCityStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<StoreState>? Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out List<StoreState>? cells, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return null;
        }

        // Corrupt or locked town file just means a fresh town: the city
        // re-seeds from the rules instead of blocking startup.
        if (cells is null)
        {
            _log.LogWarning(
                $"city.json {JsonFile.Describe(error)} at {_file}; starting a fresh town");
            return null;
        }

        // A hand-edited row without a key cannot be matched to a store;
        // CityEconomy clamps the numbers themselves.
        List<StoreState> usable = cells.FindAll(c => !string.IsNullOrEmpty(c.Key));
        if (usable.Count == 0)
        {
            _log.LogWarning($"city.json has no usable rows at {_file}; starting a fresh town");
            return [];
        }

        _log.LogInfo($"city loaded: {usable.Count} stores");
        return usable;
    }

    public void Save(IReadOnlyList<StoreState> states)
    {
        // never break gameplay or shutdown for persistence
        if (!JsonFile.TryWrite(_file, states, out string? error))
        {
            _log.LogWarning($"city.json unwritable at {_file} ({error})");
        }
    }
}
