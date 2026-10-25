// Lifetime settled jobs (PORTPLAN item 24, Unity PlayerData.jobsCompleted).
// Kept apart from OperatingZone: the zone's counter resets on every expansion,
// while the business generators gate on the career-long total (the dealer at
// 10 jobs, the mechanic at 36). The ToDo subjects read the per-kind totals
// (PORTPLAN 46, Unity taxiJobsCompleted / deliveryJobsCompleted), so a job
// registers its kind beside the combined total.
namespace AceDrive.Core;

/// <summary>The settled-job totals behind the job-gated POIs and the ToDo tips.</summary>
public sealed class JobLedger
{
    public JobLedger(DriverState? saved = null)
    {
        Completed = Math.Max(0, saved?.JobsCompleted ?? 0);
        // A save written before the split carries only the combined total:
        // the per-kind counters then start at 0 instead of inventing a
        // breakdown the save never recorded.
        TaxiCompleted = Math.Max(0, saved?.TaxiJobsCompleted ?? 0);
        DeliveryCompleted = Math.Max(0, saved?.DeliveryJobsCompleted ?? 0);
    }

    /// <summary>Every settled taxi or delivery job, across sessions.</summary>
    public int Completed { get; private set; }

    /// <summary>Every settled taxi ride, across sessions (Unity taxiJobsCompleted).</summary>
    public int TaxiCompleted { get; private set; }

    /// <summary>Every settled delivery run, across sessions (Unity deliveryJobsCompleted).</summary>
    public int DeliveryCompleted { get; private set; }

    /// <summary>One settled taxi ride; returns the new lifetime total.</summary>
    public int RegisterTaxi()
    {
        Completed++;
        TaxiCompleted++;
        return Completed;
    }

    /// <summary>One settled delivery run; returns the new lifetime total.</summary>
    public int RegisterDelivery()
    {
        Completed++;
        DeliveryCompleted++;
        return Completed;
    }
}
