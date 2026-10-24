using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the taxi shift under %AppData%\AceDrive. Never throws: a
/// missing or corrupt file means a closed shift, and a failed write must never
/// break gameplay or shutdown. The wall-clock stamps inside are what make the
/// idle timeout survive a restart.
/// </summary>
public sealed class JsonShiftStore : IShiftStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonShiftStore(ILog log)
        : this(DataPaths.UserSave("shift.json"), log)
    { }

    public JsonShiftStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public TaxiShiftSnapshot? Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out TaxiShiftSnapshot? state, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return null;
        }

        // Corrupt or locked shift file just means a closed shift: the next
        // START opens a fresh one instead of blocking startup.
        if (state is null || !IsValid(state))
        {
            _log.LogWarning(
                $"shift.json {JsonFile.Describe(error)} at {_file}; starting with a closed shift");
            return null;
        }

        _log.LogInfo($"shift loaded: {(state.IsStarted ? "open" : "closed")}, " +
            $"{state.JobsDone} jobs / {state.Earned:0.00}");
        return state;
    }

    public void Save(TaxiShiftSnapshot snapshot)
    {
        // never break gameplay or shutdown for persistence
        if (!JsonFile.TryWrite(_file, snapshot, out string? error))
        {
            _log.LogWarning($"shift.json unwritable at {_file} ({error})");
        }
    }

    // An open shift without stamps can never resolve its timeout, so it is a
    // corrupt save rather than a resumable one.
    private static bool IsValid(TaxiShiftSnapshot state) =>
        state.JobsDone >= 0
        && state.Earned >= 0.0
        && double.IsFinite(state.Earned)
        && (!state.IsStarted || (state.StartedAt != default && state.LastJobEnd != default));
}
