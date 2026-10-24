using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The readout formatter on its own: which lines a tick or a job change
/// actually repaints, and the waiting state that paints once and then stays
/// put. Nothing here touches a panel — the coordinator applies the returned
/// paint.
/// </summary>
public sealed class TelemetryReadoutsTests
{
    private static readonly TelemetrySample Sample = new(
        PacketId: 7, Gas: 0.25f, Brake: 0f, Gear: 4, Rpm: 3200, SteerRad: 0f, SpeedKmh: 90.4f);

    public TelemetryReadoutsTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    [Fact]
    public void Paint_FirstLiveSample_PaintsEveryLine()
    {
        var readouts = new TelemetryReadouts();

        ReadoutPaint paint = readouts.Paint(Sample, gameRunning: true);

        Assert.True(paint.Live);
        Assert.Equal(Strings.Format("EvoLive", 7), paint.Status);
        Assert.Equal(Strings.Format("SpeedKmh", 90.4f), paint.Speed);
        Assert.Equal(TelemetryConventions.GearDisplay(4), paint.Gear);
        Assert.Equal(Strings.Format("EvoDetail", 3200, 0.25f, 0f), paint.Detail);
    }

    [Fact]
    public void Paint_UnchangedSample_LeavesEveryLineAlone()
    {
        var readouts = new TelemetryReadouts();
        readouts.Paint(Sample, gameRunning: true);

        ReadoutPaint paint = readouts.Paint(Sample, gameRunning: true);

        // Still live, but nothing to format: the poll copies no string and
        // raises no notification for it.
        Assert.True(paint.Live);
        Assert.Null(paint.Status);
        Assert.Null(paint.Speed);
        Assert.Null(paint.Gear);
        Assert.Null(paint.Detail);
    }

    [Fact]
    public void Paint_ChangedQuantisedValue_RepaintsOnlyThatLine()
    {
        var readouts = new TelemetryReadouts();
        readouts.Paint(Sample, gameRunning: true);

        ReadoutPaint paint = readouts.Paint(Sample with { SpeedKmh = 94.1f }, gameRunning: true);

        Assert.Equal(Strings.Format("SpeedKmh", 94.1f), paint.Speed);
        Assert.Null(paint.Gear);
        Assert.Null(paint.Detail);
    }

    [Fact]
    public void Paint_WaitingWithoutTheGame_SaysWaitingOnce()
    {
        var readouts = new TelemetryReadouts();

        ReadoutPaint first = readouts.Paint(null, gameRunning: false);
        ReadoutPaint second = readouts.Paint(null, gameRunning: false);

        Assert.False(first.Live);
        Assert.Equal(Strings.Get("WaitingForEvo"), first.Status);
        Assert.Equal(Strings.Format("SpeedKmh", 0), first.Speed);
        Assert.Null(second.Status);
        Assert.Null(second.Speed);
    }

    [Fact]
    public void Paint_GameRunningWithoutPhysics_PaintsTheMissingSharedMemoryLine()
    {
        var readouts = new TelemetryReadouts();
        readouts.Paint(null, gameRunning: false);

        ReadoutPaint paint = readouts.Paint(null, gameRunning: true);

        Assert.Equal(Strings.Get("EvoNoShm"), paint.Status);
    }

    [Fact]
    public void PaintJob_PaintsThePhaseTheFareAndTheShift()
    {
        DateTime now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        var job = new TaxiJobState
        {
            Phase = JobPhase.ToPickup,
            PickupName = "Street 1",
            DropoffName = "Street 2",
            Fare = 12.5,
        };
        var shift = new TaxiShift();

        ReadoutPaint paint = TelemetryReadouts.PaintJob(job, shift, now);

        Assert.Equal(Strings.Format("JobToPickup", "Street 1", "Street 2"), paint.Job);
        Assert.Equal(Strings.Format("FareTotal", 12.5), paint.Fare);
        Assert.Equal(Strings.Format("ShiftSummary", 0, 0.0), paint.Shift);
        Assert.Null(paint.Live);
    }

    [Fact]
    public void PaintJob_OpenShift_CarriesTheCountdown()
    {
        DateTime now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        var shift = new TaxiShift();
        shift.Start(now);

        ReadoutPaint paint = TelemetryReadouts.PaintJob(new TaxiJobState(), shift, now);

        Assert.Equal(
            Strings.Format("ShiftSummaryActive", 0, 0.0, "30:00"),
            paint.Shift);
        Assert.Equal(Strings.Get("NoJob"), paint.Job);
    }
}
