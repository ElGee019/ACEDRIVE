using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the hunger reading under %AppData%\AceDrive. Never throws: a
/// missing or corrupt file means a freshly fed driver, and a failed write
/// must never break gameplay or shutdown.
/// </summary>
public sealed class JsonHungerStateStore : IHungerStateStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonHungerStateStore(ILog log)
        : this(DataPaths.UserSave("hunger.json"), log)
    { }

    public JsonHungerStateStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public HungerState? Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out HungerState? state, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return null;
        }

        // Corrupt or locked save just means a fed driver: the meter re-seeds
        // at 0 instead of blocking startup.
        if (state is null || state.Hunger < 0)
        {
            _log.LogWarning($"hunger.json {JsonFile.Describe(error)} at {_file}; starting fed");
            return null;
        }

        _log.LogInfo($"hunger loaded: {state.Hunger} ({(state.ThresholdTriggered ? "pancake event fired" : "pancake event armed")})");
        return state;
    }

    public void Save(HungerState state)
    {
        // never break gameplay or shutdown for persistence
        if (!JsonFile.TryWrite(_file, state, out string? error))
        {
            _log.LogWarning($"hunger.json unwritable at {_file} ({error})");
        }
    }
}
