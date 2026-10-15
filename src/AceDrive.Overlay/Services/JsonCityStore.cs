using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the town under %AppData%\AceDrive. Never throws: a missing or
/// corrupt file means a freshly seeded city, and a failed write must never
/// break gameplay or shutdown.
/// </summary>
public sealed class JsonCityStore : ICityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonCityStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "city.json"), log)
    { }

    public JsonCityStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<StoreState>? Load()
    {
        try
        {
            if (!File.Exists(_file)) return null;
            List<StoreState>? cells = JsonSerializer.Deserialize<List<StoreState>>(
                File.ReadAllText(_file), JsonOptions);
            if (cells is null)
            {
                _log.LogWarning($"city.json invalid at {_file}; starting a fresh town");
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Corrupt or locked town file just means a fresh town: the city
            // re-seeds from the rules instead of blocking startup.
            _log.LogWarning($"city.json unreadable at {_file} ({ex.GetType().Name}); starting a fresh town");
            return null;
        }
    }

    public void Save(IReadOnlyList<StoreState> states)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(states, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // never break gameplay or shutdown for persistence
            _log.LogWarning($"city.json unwritable at {_file} ({ex.GetType().Name})");
        }
    }
}
