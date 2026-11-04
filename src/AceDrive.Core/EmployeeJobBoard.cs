// The employees page's finished-job flags (PORTPLAN item 23). Session state on
// purpose: the car-sharing finish is painted per row from EmployeeAssignments,
// so no flow marks these flags yet. A later family page calls MarkFinished when
// it has work to flag, and the button turns teal until ClearFinished.
// WHY no store beside EmployeeAssignments' employee_jobs.json: a flag that
// nothing ever sets cannot be lost by a restart. The flow that first marks one
// owes it a banked state of its own, so this file never gets two writers.
namespace AceDrive.Core;

/// <summary>
/// Which employee work families have a finished job waiting. One set, no
/// counts: the page only needs "did something finish here?" to paint the
/// button teal. Cold path only (the page reads it on open/refresh), so a plain
/// set is enough — no store, no polling.
/// </summary>
public sealed class EmployeeJobBoard
{
    private readonly HashSet<EmployeeJobKind> _finished = [];

    /// <summary>True while this family has a finished job the page should flag.</summary>
    public bool HasFinished(EmployeeJobKind kind) => _finished.Contains(kind);

    /// <summary>
    /// Records a finished employee job. A future settle flow calls this once
    /// per finished job; the page then paints that family's button teal.
    /// </summary>
    public void MarkFinished(EmployeeJobKind kind) => _finished.Add(kind);

    /// <summary>
    /// Clears the flag once the player has seen the family's page, so the teal
    /// mark means "something new" instead of "ever finished". The future page
    /// behind each button calls this on open.
    /// </summary>
    public void ClearFinished(EmployeeJobKind kind) => _finished.Remove(kind);
}
