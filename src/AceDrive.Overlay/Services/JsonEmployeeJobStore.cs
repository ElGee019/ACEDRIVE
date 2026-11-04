using System.Text.Json;
using System.Text.Json.Serialization;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the employees' current jobs under
/// %AppData%\AceDrive\employee_jobs.json. Never throws: a missing or corrupt
/// file means nobody is working and the next page open paints every row idle,
/// and a failed write must never break an assign tap or shutdown.
/// </summary>
public sealed class JsonEmployeeJobStore : IEmployeeJobStore
{
    // WHY the enum as a name: the family is the whole point of the file, so a
    // save a player or a log reader opens says CarSharing instead of a number
    // that silently means another family once the enum is reordered. Both
    // directions use these options, and a numeric value still loads.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonEmployeeJobStore(ILog log)
        : this(DataPaths.UserSave("employee_jobs.json"), log)
    { }

    public JsonEmployeeJobStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public EmployeeJobState Load()
    {
        JsonReadStatus status = JsonFile.TryRead(
            _file, out EmployeeJobState? state, out string? error, Options);
        if (status == JsonReadStatus.Unreadable)
        {
            // A corrupt job file must not take the overlay down; the crew is
            // still hired and the page can assign them again.
            _log.LogWarning(
                $"employee_jobs.json unreadable at {_file} ({error}); starting with nobody on a job");
        }

        return state ?? new EmployeeJobState();
    }

    public void Save(EmployeeJobState state)
    {
        if (state is null)
        {
            return;
        }

        // A failed bank must never break the assign: the page keeps the job in
        // memory and the next change retries the write.
        if (!JsonFile.TryWrite(_file, state, out string? error, Options))
        {
            _log.LogWarning(
                $"employee_jobs.json unwritable at {_file} ({error}); jobs kept in memory");
        }
    }
}
