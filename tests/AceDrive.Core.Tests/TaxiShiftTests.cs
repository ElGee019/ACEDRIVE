using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The shift snapshot is what carries the 30-minute idle rule across
/// a closed overlay: the wall-clock stamps, not process uptime, own the end.</summary>
public sealed class TaxiShiftTests
{
    private static readonly DateTime Start =
        new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Snapshot_RestoresTheSameOpenShift()
    {
        var shift = new TaxiShift();
        shift.Start(Start);
        shift.RecordCompleted(
            new TaxiJobState { Phase = JobPhase.Complete, Fare = 10, Tip = 1 },
            Start.AddMinutes(20));

        var resumed = new TaxiShift(shift.Snapshot());

        Assert.True(resumed.IsStarted);
        Assert.Equal(1, resumed.JobsDone);
        Assert.Equal(11.0, resumed.Earned);
        Assert.Equal(Start, resumed.StartedAt);
        Assert.Equal(Start.AddMinutes(20), resumed.LastJobEnd);
        // The timeout stays anchored to the last dropoff, not the restart.
        Assert.False(resumed.IsTimedOut(Start.AddMinutes(49.9)));
        Assert.True(resumed.IsTimedOut(Start.AddMinutes(50)));
    }

    [Fact]
    public void RestoredOpenShift_ExpiresWhileTheOverlayWasClosed()
    {
        var saved = new TaxiShiftSnapshot(
            IsStarted: true, Start, LastJobEnd: Start.AddMinutes(10),
            JobsDone: 2, Earned: 42.0);

        // The next launch is 31 minutes after the latest dropoff: the shift
        // ended while nothing was running and must not reopen.
        var resumed = new TaxiShift(saved);

        Assert.True(resumed.IsTimedOut(Start.AddMinutes(41)));
        Assert.True(resumed.TryEndTimedOut(Start.AddMinutes(41), out TimeSpan idle));
        Assert.False(resumed.IsStarted);
        Assert.Equal(2, resumed.JobsDone); // counters kept for the header/receipt
        Assert.True((idle - TimeSpan.FromMinutes(31)).Duration() < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Snapshot_RestoresAClosedShiftAsClosed()
    {
        var closed = new TaxiShiftSnapshot(
            IsStarted: false, Start, Start.AddMinutes(5), JobsDone: 3, Earned: 99.0);

        var resumed = new TaxiShift(closed);

        Assert.False(resumed.IsStarted);
        Assert.False(resumed.IsTimedOut(Start.AddDays(1)));
        Assert.Equal(3, resumed.JobsDone);
        Assert.Equal(99.0, resumed.Earned);
        Assert.Equal(TimeSpan.Zero, resumed.Remaining(Start.AddDays(1)));
    }
}
