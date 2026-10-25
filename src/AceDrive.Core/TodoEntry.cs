// Grandma's ToDo list data (PORTPLAN item 46, port of TodoSubjectData).
// Pure data: stable subject keys plus targets; targets load from
// Data/todos.json at startup (JsonTodoStore). The ledger seeds its four
// visible slots from this table; every later target comes from the subject's
// own requirement curve.
namespace AceDrive.Core;

/// <summary>One tutorial quest: stable subject key plus its starting target.</summary>
public sealed record TodoEntry(string SubjectId, int Target);
