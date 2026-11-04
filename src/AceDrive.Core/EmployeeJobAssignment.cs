// The employees page's one-job record (PORTPLAN item 23). The assign page
// writes it; the employees page paints it and collects what it has earned.
// Car sharing pays the day's batch XP, a taxi ride pays its distance in CR and
// a delivery pays per unit its refill adds, so one job carries one currency:
// the family's flow fills whichever amount it pays and the other stays zero.
namespace AceDrive.Core;

/// <summary>
/// One employee's current job. <see cref="DueGameMinute"/> is the absolute
/// game minute the work ends, <see cref="Xp"/>/<see cref="Cr"/> are what
/// collecting pays — car sharing banks XP, the taxi and delivery runs bank CR —
/// and <see cref="StoreKey"/> names the store an employee delivery targets, so
/// its payout can be settled against the live shelves when it arrives. The
/// record is immutable: the page replaces it on assignment and takes it away
/// when the player collects.
/// </summary>
public sealed record EmployeeJobAssignment(
    EmployeeJobKind Kind,
    long? DueGameMinute = null,
    double Xp = 0.0,
    double Cr = 0.0,
    string? StoreKey = null)
{
    /// <summary>
    /// True once the game clock reached the due minute. A job without a finish
    /// model (no due minute) is never due: it keeps reading ON JOB until its
    /// flow can time it.
    /// </summary>
    public bool IsDue(long gameMinute) =>
        DueGameMinute is { } due && gameMinute >= due;
}
