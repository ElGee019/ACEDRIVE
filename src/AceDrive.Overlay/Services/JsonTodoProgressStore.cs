// ToDo progress on disk under %AppData%\AceDrive (PORTPLAN item 46). Never
// throws: a missing or corrupt file means a fresh list (the ledger validates
// every id), and a failed write keeps the session's progress in memory.
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>todos_state.json: the four slots, the queue and the completion flags.</summary>
public sealed class JsonTodoProgressStore : ITodoProgressStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonTodoProgressStore(ILog log)
        : this(DataPaths.UserSave("todos_state.json"), log)
    { }

    public JsonTodoProgressStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public TodoLedgerState? Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out TodoLedgerState? state, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return null;
        }

        if (state is null)
        {
            _log.LogWarning(
                $"todos_state.json {JsonFile.Describe(error)} at {_file}; starting a fresh list");
            return null;
        }

        return state;
    }

    public void Save(TodoLedgerState state)
    {
        if (!JsonFile.TryWrite(_file, state, out string? error))
        {
            _log.LogWarning(
                $"todos_state.json unwritable at {_file} ({error}); progress kept in memory");
        }
    }
}
