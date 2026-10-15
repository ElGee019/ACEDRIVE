// Grandma's ToDo list data (PORTPLAN item 46, port of TodoSubjectData).
// Pure data: stable subject keys plus targets; targets load from
// Data/todos.json at startup (JsonTodoStore).
namespace AceDrive.Core;

/// <summary>One tutorial quest: stable subject key plus its target.</summary>
public sealed record TodoEntry(string SubjectId, int Target);
