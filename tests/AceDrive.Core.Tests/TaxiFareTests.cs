using AceDrive.Core;

namespace AceDrive.Core.Tests;

public sealed class TaxiFareTests
{
    private static readonly FareTable Table = FareTable.Default;

    [Fact]
    public void ZeroDistance_IsForfaitOnly() =>
        Assert.Equal(10.0, TaxiFare.ComputeBase(0, Table));

    [Fact]
    public void OneKm_FirstTierOnly() =>
        Assert.Equal(16.67, TaxiFare.ComputeBase(1.0, Table));

    [Fact]
    public void TwoKm_FirstTierBoundary() =>
        Assert.Equal(23.34, TaxiFare.ComputeBase(2.0, Table));

    [Fact]
    public void EightPointTwoKm_ThreeTiers() =>
        Assert.Equal(53.15, TaxiFare.ComputeBase(8.2, Table));

    [Fact]
    public void SixtyKm_ReachesOpenTier() =>
        Assert.Equal(150.35, TaxiFare.ComputeBase(60.0, Table));

    [Fact]
    public void NegativeDistance_ClampedToForfait() =>
        Assert.Equal(10.0, TaxiFare.ComputeBase(-5, Table));

    [Fact]
    public void EligibleTip_IsTwentyPercent() =>
        Assert.Equal(10.63, TaxiFare.ComputeTip(53.15, tipEligible: true, Table));

    [Fact]
    public void IneligibleTip_IsZero() =>
        Assert.Equal(0.0, TaxiFare.ComputeTip(53.15, tipEligible: false, Table));

    [Fact]
    public void Fines_CostFiftyEach() =>
        Assert.Equal(100.0, TaxiFare.ComputeFineDeduction(2, Table));

    [Fact]
    public void NegativeFines_ClampedToZero() =>
        Assert.Equal(0.0, TaxiFare.ComputeFineDeduction(-1, Table));

    [Fact]
    public void Shift_AccumulatesCompletedJobsOnly()
    {
        var shift = new TaxiShift();
        var incomplete = new TaxiJobState { Phase = JobPhase.EnRoute, Fare = 10, Tip = 1 };
        shift.RecordCompleted(incomplete);
        Assert.Equal(0, shift.JobsDone);

        var done = new TaxiJobState { Phase = JobPhase.Complete, Fare = 10, Tip = 1 };
        shift.RecordCompleted(done);
        Assert.Equal(1, shift.JobsDone);
        Assert.Equal(11.0, shift.Earned);
    }

    [Fact]
    public void Shift_JobTotalDeductsFine()
    {
        var shift = new TaxiShift();
        var done = new TaxiJobState { Phase = JobPhase.Complete, Fare = 53.15, Tip = 10.63, Fine = 50.0 };
        shift.RecordCompleted(done, new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(1, shift.JobsDone);
        Assert.Equal(13.78, shift.Earned);
    }

    [Fact]
    public void Shift_StartsClosed_TimesOutAfterThirtyIdleMinutes()
    {
        var shift = new TaxiShift();
        var start = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(shift.IsStarted);
        Assert.False(shift.IsTimedOut(start));

        shift.Start(start);
        Assert.True(shift.IsStarted);
        Assert.False(shift.IsTimedOut(start.AddMinutes(29.9)));
        Assert.True(shift.Remaining(start) - TimeSpan.FromMinutes(30) < TimeSpan.FromSeconds(1));

        var done = new TaxiJobState { Phase = JobPhase.Complete, Fare = 10, Tip = 1 };
        shift.RecordCompleted(done, start.AddMinutes(5));
        Assert.Equal(1, shift.JobsDone);
        // Idle clock restamps on dropoff: 30 min after the job, not the start.
        Assert.False(shift.IsTimedOut(start.AddMinutes(34.9)));
        Assert.True(shift.IsTimedOut(start.AddMinutes(35)));

        Assert.True(shift.TryEndTimedOut(start.AddMinutes(35), out TimeSpan idle));
        Assert.False(shift.IsStarted);
        Assert.Equal(1, shift.JobsDone); // counters kept for the receipt
        Assert.True((idle - TimeSpan.FromMinutes(30)).Duration() < TimeSpan.FromSeconds(1));
        Assert.False(shift.TryEndTimedOut(start.AddMinutes(36), out _));
    }
}
