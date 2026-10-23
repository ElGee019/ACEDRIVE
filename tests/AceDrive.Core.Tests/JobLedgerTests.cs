using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The lifetime settled-job total behind the job-gated POIs.</summary>
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

        Assert.Equal(1, ledger.Register());
        Assert.Equal(2, ledger.Register());
        Assert.Equal(2, ledger.Completed);
    }
}
