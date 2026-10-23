// Lifetime settled jobs (PORTPLAN item 24, Unity PlayerData.jobsCompleted).
// Kept apart from OperatingZone: the zone's counter resets on every expansion,
// while the business generators gate on the career-long total (the dealer at
// 10 jobs, the mechanic at 36).
namespace AceDrive.Core;

/// <summary>The settled-job total behind the job-gated business POIs.</summary>
public sealed class JobLedger
{
    public JobLedger(DriverState? saved = null) =>
        Completed = Math.Max(0, saved?.JobsCompleted ?? 0);

    /// <summary>Every settled taxi or delivery job, across sessions.</summary>
    public int Completed { get; private set; }

    /// <summary>One settled job; returns the new lifetime total.</summary>
    public int Register()
    {
        Completed++;
        return Completed;
    }
}
