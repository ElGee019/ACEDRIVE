using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads ToDo balance from Data/todos.json next to the exe. Missing or
/// invalid file degrades to <see cref="TodoTable.Default"/> with a warning â€”
/// the overlay must never fail to start over a data file.
/// </summary>
public sealed class JsonTodoStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonTodoStore(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "todos.json"), log)
    { }

    public JsonTodoStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public TodoTable Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"todos.json missing at {_file}; using built-in defaults");
                return TodoTable.Default;
            }
            var table = JsonSerializer.Deserialize<TodoTable>(
                File.ReadAllText(_file), JsonOptions);
            if (table?.Entries is not { Count: > 0 } || !IsValid(table))
            {
                _log.LogWarning($"todos.json invalid at {_file}; using built-in defaults");
                return TodoTable.Default;
            }
            _log.LogInfo($"todos loaded: {table.Entries.Count} entries");
            return table;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"todos.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return TodoTable.Default;
        }
    }

    // Every entry needs a subject the ViewModel can format plus a reachable target.
    private static bool IsValid(TodoTable table)
    {
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
