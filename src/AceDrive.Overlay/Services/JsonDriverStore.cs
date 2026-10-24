using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the driver under %AppData%\AceDrive. Never throws: a missing
/// or corrupt file means a fresh driver, and a failed write must never
/// break gameplay or shutdown.
/// </summary>
public sealed class JsonDriverStore : IDriverStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonDriverStore(ILog log)
        : this(DataPaths.UserSave("driver.json"), log)
    { }

    public JsonDriverStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public DriverState? Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out DriverState? state, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return null;
        }

        // A corrupt save degrades to a fresh driver, never takes the overlay
        // down (AGENTS.md: Try* at every external boundary).
        if (state is null || state.Cash < 0 || state.Xp < 0 || state.Bank < 0)
        {
            _log.LogWarning(
                $"driver.json {JsonFile.Describe(error)} at {_file}; starting a fresh driver");
            return null;
        }

        _log.LogInfo($"driver loaded: cash={state.Cash:0.00} xp={state.Xp:0.00} bank={state.Bank:0.00}");
        return state;
    }

    public void Save(DriverState state)
    {
        // A failed bank must never break gameplay or shutdown; the next bank
        // retries with fresher numbers.
        if (!JsonFile.TryWrite(_file, state, out string? error))
        {
            _log.LogWarning(
                $"driver.json unwritable at {_file} ({error}); progress kept in memory");
        }
    }
}
