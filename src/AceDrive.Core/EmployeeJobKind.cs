// The three employee work families the employees page reports on (PORTPLAN
// item 23). Pure data: the page paints them, and the assignment's due minute
// decides when a job has finished.
namespace AceDrive.Core;

/// <summary>
/// One family of work an employee can take. The page has one button per kind,
/// and its text turns teal while that family has a finished job waiting.
/// </summary>
public enum EmployeeJobKind
{
    /// <summary>A car-sharing booking the employee drove.</summary>
    CarSharing,

    /// <summary>A taxi job the employee drove.</summary>
    TaxiJobs,

    /// <summary>A delivery job the employee drove.</summary>
    DeliveryJobs,
}
