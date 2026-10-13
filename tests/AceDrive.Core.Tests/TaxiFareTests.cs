using AceDrive.Core;

namespace AceDrive.Core.Tests;

public sealed class TaxiFareTests
{
    private static readonly FareTable Table = FareTable.Default;

    [Fact]
    public void ZeroDistance_IsBaseFare() =>
        Assert.Equal(3.50, TaxiFare.ComputeFare(0, Table));

    [Fact]
    public void TenKm_Fare() =>
        Assert.Equal(21.50, TaxiFare.ComputeFare(10, Table));

    [Fact]
    public void NegativeDistance_ClampedToBase() =>
        Assert.Equal(3.50, TaxiFare.ComputeFare(-5, Table));

    [Fact]
    public void CleanDrive_TipIsTenPercent() =>
        Assert.Equal(2.15, TaxiFare.ComputeTip(21.50, cleanDrive: true, Table));

    [Fact]
    public void DirtyDrive_NoTip() =>
        Assert.Equal(0.0, TaxiFare.ComputeTip(21.50, cleanDrive: false, Table));

    [Fact]
    public void CustomTable_DrivesMath()
    {
        var table = new FareTable(BaseFare: 5.00, PerKmRate: 2.00, TipRate: 0.20);
        Assert.Equal(9.00, TaxiFare.ComputeFare(2, table));
        Assert.Equal(1.80, TaxiFare.ComputeTip(9.00, cleanDrive: true, table));
    }

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
