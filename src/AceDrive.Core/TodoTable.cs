// Grandma's ToDo list data (PORTPLAN item 46, port of TodoSubjectData).
// Balance table; Default is the test-shell fallback, not the source of
// truth. Progress (Current) arrives with the persistent save (PORTPLAN 51).
namespace AceDrive.Core;

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
