// The persisted employee jobs (PORTPLAN item 23). Pure data: EmployeeAssignments
// owns the rules and the store owns the file, exactly as EmployeeState splits the
// crew's hiring from employees.json.
namespace AceDrive.Core;

/// <summary>
/// What the employees page's on-job marks keep between launches: every employee
/// currently working, keyed by hire name, with the work family, the absolute game
/// minute the job ends (null while a family has no finish model) and the XP the
/// collect pays. Null is a save written before jobs were banked, which resumes as
/// nobody on a job instead of a migration.
/// </summary>
public sealed record EmployeeJobState(
    IReadOnlyDictionary<string, EmployeeJobAssignment>? Jobs = null);
