using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the road-test pick under %AppData%\AceDrive. Never throws: a
/// missing or corrupt file means a fresh random draw, and a failed write must
/// never break gameplay or shutdown.
/// </summary>
public sealed class JsonRoadTestStore : IRoadTestStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonRoadTestStore(ILog log)
        : this(DataPaths.UserSave("roadtest.json"), log)
    { }

    public JsonRoadTestStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public RoadTestSnapshot? Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out RoadTestSnapshot? state, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return null;
        }

        // Corrupt or locked pick file just means a fresh draw: the road test
        // re-picks from the pool instead of blocking startup.
        if (state is null || state.TestedIds is null)
        {
            _log.LogWarning(
                $"roadtest.json {JsonFile.Describe(error)} at {_file}; drawing a fresh pick");
            return null;
        }

        _log.LogInfo($"road test resumed: {state.CarId ?? "none"} " +
            $"({state.TestedIds.Count} banked)");
        return state;
    }

    public void Save(RoadTestSnapshot snapshot)
    {
        // never break gameplay or shutdown for persistence
        if (!JsonFile.TryWrite(_file, snapshot, out string? error))
        {
            _log.LogWarning($"roadtest.json unwritable at {_file} ({error})");
        }
    }
}
