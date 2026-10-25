// Grandma's ToDo list data (PORTPLAN item 46, port of TodoSubjectData).
// Balance table; Default is the test-shell fallback, not the source of truth.
// The ledger seeds its four visible slots from this table and takes every
// other target from its requirement's own rule.
namespace AceDrive.Core;

/// <summary>Balance table for the starter ToDo list.</summary>
public sealed record TodoTable(IReadOnlyList<TodoEntry> Entries)
{
    // Unity TodoManager.Reset's four starting tips, in its order.
    public static readonly TodoTable Default = new(
    [
        new(TodoSubject.CrEarnings, 500),
        new(TodoSubject.XpEarnings, 2),
        new(TodoSubject.BankStandings, 1002),
        new(TodoSubject.GoToMaxGroceryStore, 1),
    ]);
}
