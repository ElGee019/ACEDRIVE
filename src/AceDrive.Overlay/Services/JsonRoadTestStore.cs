using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the road-test pick under %AppData%\AceDrive. Never throws: a
/// missing or corrupt file means a fresh random draw, and a failed write must
/// never break gameplay or shutdown.
/// </summary>
public sealed class JsonRoadTestStore : IRoadTestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonRoadTestStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "roadtest.json"), log)
    { }

    public JsonRoadTestStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public RoadTestSnapshot? Load()
    {
        try
        {
            if (!File.Exists(_file)) return null;
            RoadTestSnapshot? state = JsonSerializer.Deserialize<RoadTestSnapshot>(
                File.ReadAllText(_file), JsonOptions);
            if (state is null || state.TestedIds is null)
            {
                _log.LogWarning($"roadtest.json invalid at {_file}; drawing a fresh pick");
                return null;
            }
            _log.LogInfo($"road test resumed: {state.CarId ?? "none"} " +
                $"({state.TestedIds.Count} banked)");
            return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Corrupt or locked pick file just means a fresh draw: the road
            // test re-picks from the pool instead of blocking startup.
            _log.LogWarning($"roadtest.json unreadable at {_file} ({ex.GetType().Name}); drawing a fresh pick");
            return null;
        }
    }

    public void Save(RoadTestSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // never break gameplay or shutdown for persistence
            _log.LogWarning($"roadtest.json unwritable at {_file} ({ex.GetType().Name})");
        }
    }
}
