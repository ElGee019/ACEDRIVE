// Grandma's ToDo list (PORTPLAN item 46, port of TodoSubjectData).
// Pure data: stable subject keys plus targets. Targets load from
// Data/todos.json at startup (JsonTodoStore); Default is the test-shell
// fallback, not the source of truth. Progress (Current) arrives with the
// persistent save (PORTPLAN item 51); until then everything reads 0.
namespace AceDrive.Core;

/// <summary>One tutorial quest: stable subject key plus its target.</summary>
public sealed record TodoEntry(string SubjectId, int Target);

/// <summary>Balance table for the starter ToDo list.</summary>
public sealed record TodoTable(IReadOnlyList<TodoEntry> Entries)
{
    public static readonly TodoTable Default = new(
    [
        new(GrandmaTodo.MaxGrocery, 1),
        new(GrandmaTodo.TaxiJobs, 3),
        new(GrandmaTodo.EarnCr, 500),
        new(GrandmaTodo.VisitBank, 1),
    ]);
}

public static class GrandmaTodo
{
    public const string MaxGrocery = "MaxGrocery";
    public const string TaxiJobs = "TaxiJobs";
    public const string EarnCr = "EarnCr";
    public const string VisitBank = "VisitBank";

    public static bool IsKnown(string? id) =>
        id is MaxGrocery or TaxiJobs or EarnCr or VisitBank;
}
