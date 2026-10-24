using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the game moment under %AppData%\AceDrive. Never throws:
/// a missing or corrupt file means a fresh Saturday morning, and a failed
/// write must never break gameplay or shutdown.
/// </summary>
public sealed class JsonGameClockStore : IGameClockStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonGameClockStore(ILog log)
        : this(DataPaths.UserSave("gameclock.json"), log)
    { }

    public JsonGameClockStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public GameClockState? Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out GameClockState? state, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return null;
        }

        // Corrupt or locked clock file just means a fresh week: the game
        // moment rebuilds from the default start instead of blocking startup.
        if (state is null || state.Week < 1 || state.GameNow == default)
        {
            _log.LogWarning(
                $"gameclock.json {JsonFile.Describe(error)} at {_file}; starting a fresh week");
            return null;
        }

        _log.LogInfo($"clock loaded: {state.GameNow:yyyy-MM-dd HH:mm} week {state.Week}");
        return state;
    }

    public void Save(GameClockState state)
    {
        // never break gameplay or shutdown for persistence
        if (!JsonFile.TryWrite(_file, state, out string? error))
        {
            _log.LogWarning($"gameclock.json unwritable at {_file} ({error})");
        }
    }
}
