using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The lifetime settled-job total behind the job-gated POIs and the
/// per-kind totals the ToDo tips read (PORTPLAN 46).</summary>
public sealed class JobLedgerTests
{
    [Fact]
    public void Fresh_StartsAtZero()
    {
        Assert.Equal(0, new JobLedger().Completed);
    }

    [Fact]
    public void Saved_RestoresTheLifetimeTotal()
    {
        Assert.Equal(36, new JobLedger(new DriverState(0.0, 0.0, JobsCompleted: 36)).Completed);
    }

    [Fact]
    public void Saved_NegativeTotal_ClampsToZero()
    {
        // A hand-edited save must not subtract from the first real job.
        Assert.Equal(0, new JobLedger(new DriverState(0.0, 0.0, JobsCompleted: -3)).Completed);
    }

    [Fact]
    public void Register_CountsEverySettledJob()
    {
        var ledger = new JobLedger();

        Assert.Equal(1, ledger.RegisterTaxi());
        Assert.Equal(2, ledger.RegisterDelivery());
        Assert.Equal(2, ledger.Completed);
        Assert.Equal(1, ledger.TaxiCompleted);
        Assert.Equal(1, ledger.DeliveryCompleted);
    }

    [Fact]
    public void Saved_KeepsThePerKindTotals()
    {
        var ledger = new JobLedger(new DriverState(
            0.0, 0.0, JobsCompleted: 9, TaxiJobsCompleted: 6, DeliveryJobsCompleted: 3));

        Assert.Equal(9, ledger.Completed);
        Assert.Equal(6, ledger.TaxiCompleted);
        Assert.Equal(3, ledger.DeliveryCompleted);
    }

    [Fact]
    public void Saved_PreSplitTotal_LeavesThePerKindCountersEmpty()
    {
        // A save from before the split only has the combined total; the tips
        // start counting from now instead of inventing a breakdown.
        var ledger = new JobLedger(new DriverState(0.0, 0.0, JobsCompleted: 12));

        Assert.Equal(12, ledger.Completed);
        Assert.Equal(0, ledger.TaxiCompleted);
        Assert.Equal(0, ledger.DeliveryCompleted);
    }
}
