// The persisted employee crew (PORTPLAN item 54, Unity PlayerData.employees +
// hireCandidates + hireRosterDay). Pure data: the roster owns the rules, the
// store owns the file.
namespace AceDrive.Core;

/// <summary>
/// What the community centre's hiring page keeps between launches: the names
/// already hired, the week's candidate roster and the display week it was
/// rolled for. Null lists are a save written before hiring existed, which
/// resumes as an empty crew instead of a migration.
/// </summary>
public sealed record EmployeeState(
    IReadOnlyList<string>? Employees = null,
    IReadOnlyList<string>? Candidates = null,
    int RosterWeek = -1);
