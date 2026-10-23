using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The live delivery run's stop cursor (PORTPLAN 19).</summary>
public sealed class DeliveryJobTests
{
    private static readonly DeliveryRun Run = new(["store"], 10.0, 20.0);

    private static DeliveryJob Job() => new(
        [
            new DeliveryStop(DeliveryStopKind.Hub, "hub", "Grocers and Sons"),
            new DeliveryStop(DeliveryStopKind.Store, "store", "Adenau Post"),
        ],
        Run);

    [Fact]
    public void WalksTheStopsAndAnswersCompleteAfterTheLast()
    {
        var job = Job();

        Assert.Equal(DeliveryStopKind.Hub, job.Current!.Kind);
        Assert.Equal(0, job.CompletedStops);
        Assert.Equal(2, job.StopCount);
        Assert.Same(Run, job.Run);

        DeliveryStop? next = job.Advance();
        Assert.Equal(DeliveryStopKind.Store, next!.Kind);
        Assert.Equal("store", next.Key);
        Assert.Equal(1, job.CompletedStops);

        Assert.Null(job.Advance());
        Assert.True(job.IsComplete);
        Assert.Equal(2, job.CompletedStops);
    }

    [Fact]
    public void EmptyJob_IsCompleteWithoutStops()
    {
        var job = new DeliveryJob([], Run);

        Assert.True(job.IsComplete);
        Assert.Null(job.Advance());
        Assert.Equal(0, job.StopCount);
    }

    [Fact]
    public void Constructor_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() => new DeliveryJob(null!, Run));
        Assert.Throws<ArgumentNullException>(() => new DeliveryJob([], null!));
    }
}
