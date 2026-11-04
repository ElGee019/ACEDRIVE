// Employee job persistence boundary (PORTPLAN item 23). Lives in Core beside the
// other store seams so EmployeeAssignments stays dependency-free; the JSON file
// sink is Overlay/Services/JsonEmployeeJobStore. Its own file rather than
// employees.json: the roster owns that one, and one writer per file is what keeps
// a hire from clobbering a running job.
namespace AceDrive.Core;

/// <summary>
/// Loads and banks which employees are on a job. Implementations must never
/// throw: a missing or corrupt file means nobody is working, and the employees
/// page then paints every row as NOT ON JOB.
/// </summary>
public interface IEmployeeJobStore
{
    /// <summary>The persisted jobs; empty when no job has been banked yet.</summary>
    EmployeeJobState Load();

    /// <summary>Replaces the persisted jobs with the given set.</summary>
    void Save(EmployeeJobState state);
}
