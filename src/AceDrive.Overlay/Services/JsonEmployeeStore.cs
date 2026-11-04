using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the hired crew and its weekly hire roster under
/// %AppData%\AceDrive\employees.json. Never throws: a missing or corrupt file
/// means nobody is hired and the next community-centre visit rolls a fresh
/// roster, and a failed write must never break gameplay or shutdown.
/// </summary>
public sealed class JsonEmployeeStore : IEmployeeStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonEmployeeStore(ILog log)
        : this(DataPaths.UserSave("employees.json"), log)
    { }

    public JsonEmployeeStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public EmployeeState Load()
    {
        JsonReadStatus status =
            JsonFile.TryRead(_file, out EmployeeState? state, out string? error);
        if (status == JsonReadStatus.Unreadable)
        {
            // A corrupt crew must not take the overlay down; the next visit
            // starts with an empty roster and rolls residents again.
            _log.LogWarning(
                $"employees.json unreadable at {_file} ({error}); starting with nobody hired");
        }

        return state ?? new EmployeeState();
    }

    public void Save(EmployeeState state)
    {
        if (state is null)
        {
            return;
        }

        // A failed bank must never break the hire: the roster keeps the crew
        // in memory and the next change retries the write.
        if (!JsonFile.TryWrite(_file, state, out string? error))
        {
            _log.LogWarning(
                $"employees.json unwritable at {_file} ({error}); crew kept in memory");
        }
    }
}
