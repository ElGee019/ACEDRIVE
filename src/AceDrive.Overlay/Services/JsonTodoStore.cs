using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads ToDo balance from Data/todos.json next to the exe. Missing or
/// invalid file degrades to <see cref="TodoTable.Default"/> with a warning -
/// the overlay must never fail to start over a data file.
/// </summary>
public sealed class JsonTodoStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonTodoStore(ILog log)
        : this(DataPaths.GameData("todos.json"), log)
    { }

    public JsonTodoStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public TodoTable Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "todos.json", "using built-in defaults",
                IsValid, out TodoTable? table))
        {
            return TodoTable.Default;
        }

        _log.LogInfo($"todos loaded: {table.Entries.Count} entries");
        return table;
    }

    // Every entry needs a subject the ViewModel can format plus a reachable target.
    private static bool IsValid(TodoTable table)
    {
        if (table.Entries is not { Count: > 0 })
        {
            return false;
        }

        foreach (TodoEntry entry in table.Entries)
        {
            if (!GrandmaTodo.IsKnown(entry.SubjectId) || entry.Target < 1)
            {
                return false;
            }
        }
        return true;
    }
}
