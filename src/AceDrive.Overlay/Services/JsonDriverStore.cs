using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the driver under %AppData%\AceDrive. Never throws: a missing
/// or corrupt file means a fresh driver, and a failed write must never
/// break gameplay or shutdown.
/// </summary>
public sealed class JsonDriverStore : IDriverStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonDriverStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "driver.json"), log)
    { }

    public JsonDriverStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public DriverState? Load()
    {
        try
        {
            if (!File.Exists(_file)) return null;
            DriverState? state = JsonSerializer.Deserialize<DriverState>(
                File.ReadAllText(_file), JsonOptions);
            if (state is null || state.Cash < 0 || state.Xp < 0 || state.Bank < 0)
            {
                _log.LogWarning($"driver.json invalid at {_file}; starting a fresh driver");
                return null;
            }
            _log.LogInfo($"driver loaded: cash={state.Cash:0.00} xp={state.Xp:0.00} bank={state.Bank:0.00}");
            return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt save must degrade to
            // a fresh driver, never take the overlay down (AGENTS.md: Try*
            // at every external boundary). Anything else stays a crash.
            _log.LogWarning($"driver.json unreadable at {_file} ({ex.GetType().Name}); starting a fresh driver");
            return null;
        }
    }

    public void Save(DriverState state)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_file);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_file, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed bank must never break gameplay or shutdown; the next
            // bank retries with fresher numbers.
            _log.LogWarning($"driver.json unwritable at {_file} ({ex.GetType().Name}); progress kept in memory");
        }
    }
}
