using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the hunger reading under %AppData%\AceDrive. Never throws: a
/// missing or corrupt file means a freshly fed driver, and a failed write
/// must never break gameplay or shutdown.
/// </summary>
public sealed class JsonHungerStateStore : IHungerStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonHungerStateStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "hunger.json"), log)
    { }

    public JsonHungerStateStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public HungerState? Load()
    {
        try
        {
            if (!File.Exists(_file)) return null;
            HungerState? state = JsonSerializer.Deserialize<HungerState>(
                File.ReadAllText(_file), JsonOptions);
            if (state is null || state.Hunger < 0)
            {
                _log.LogWarning($"hunger.json invalid at {_file}; starting fed");
                return null;
            }

            _log.LogInfo($"hunger loaded: {state.Hunger} ({(state.ThresholdTriggered ? "pancake event fired" : "pancake event armed")})");
            return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Corrupt or locked save just means a fed driver: the meter
            // re-seeds at 0 instead of blocking startup.
            _log.LogWarning($"hunger.json unreadable at {_file} ({ex.GetType().Name}); starting fed");
            return null;
        }
    }

    public void Save(HungerState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // never break gameplay or shutdown for persistence
            _log.LogWarning($"hunger.json unwritable at {_file} ({ex.GetType().Name})");
        }
    }
}
