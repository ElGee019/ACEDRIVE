using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The painted readouts: the status, speed, gear and detail lines the poll
/// reads off the physics tick, and the job, fare and shift lines the job state
/// drives (Unity's HUD readouts).
///
/// A formatter, not a screen: it owns the last-painted caches so the 10 Hz poll
/// re-formats only the values whose shown (quantised) form changed, and hands
/// back what it painted as a <see cref="ReadoutPaint"/>. The coordinator keeps
/// the bindable properties and copies the changes across; nothing here knows
/// about the panel or the shell.
/// </summary>
public sealed class TelemetryReadouts
{
    // Sentinels force the first paint of a session or after a lost sample.
    private const int Unpainted = -1;
    private int _lastPacketId = Unpainted;
    private int _lastSpeedKmh = Unpainted;
    private int _lastGear = int.MinValue;
    private int _lastRpm = Unpainted;
    private int _lastThrottlePct = Unpainted;
    private int _lastBrakePct = Unpainted;

    // Which "no physics yet" line is painted, so the waiting state also costs
    // one lookup on its first tick instead of one per tick.
    private string? _lastWaitingKey;

    /// <summary>
    /// One poll's paint: the physics readouts when a sample arrived, the
    /// waiting state when it did not. <paramref name="gameRunning"/> picks
    /// which waiting line is right (EVO up but no physics yet, or no game).
    /// </summary>
    public ReadoutPaint Paint(TelemetrySample? sample, bool gameRunning) =>
        sample is null ? PaintWaiting(gameRunning) : PaintLive(sample);

    /// <summary>
    /// The "no physics yet" state: it paints once per entering it, then stays
    /// put, because the string never changes while EVO is absent (the opening
    /// paint of a session takes this path too).
    /// </summary>
    public ReadoutPaint PaintWaiting(bool gameRunning)
    {
        _lastPacketId = Unpainted;
        _lastSpeedKmh = Unpainted;
        _lastGear = int.MinValue;
        _lastRpm = Unpainted;
        _lastThrottlePct = Unpainted;
        _lastBrakePct = Unpainted;
        string key = gameRunning ? "EvoNoShm" : "WaitingForEvo";
        if (key == _lastWaitingKey)
        {
            return new ReadoutPaint(Live: false);
        }

        _lastWaitingKey = key;
        return new ReadoutPaint(
            Live: false,
            Status: Strings.Get(key),
            Speed: Strings.Format("SpeedKmh", 0));
    }

    /// <summary>
    /// The job, fare and shift lines. Driven by state changes and the
    /// second-guarded countdown rather than by the tick, so these format on
    /// demand instead of caching a value that did not change — static because
    /// nothing about them is memoised.
    /// </summary>
    public static ReadoutPaint PaintJob(TaxiJobState job, TaxiShift shift, DateTime now)
    {
        string line = job.Phase switch
        {
            JobPhase.None => Strings.Get("NoJob"),
            JobPhase.Proposed => Strings.Format("JobProposed", job.PickupName, job.DropoffName),
            JobPhase.ToPickup => Strings.Format("JobToPickup", job.PickupName, job.DropoffName),
            JobPhase.EnRoute => Strings.Format("JobEnRoute", job.PickupName, job.DropoffName, job.DistanceKm),
            JobPhase.Complete => Strings.Format("JobComplete", job.PickupName, job.DropoffName, job.Tip),
            _ => string.Empty,
        };
        string shiftLine = shift.IsStarted
            ? Strings.Format(
                "ShiftSummaryActive",
                shift.JobsDone, shift.Earned, FormatRemaining(shift.Remaining(now)))
            : Strings.Format("ShiftSummary", shift.JobsDone, shift.Earned);
        return new ReadoutPaint(
            Job: line,
            Fare: Strings.Format("FareTotal", job.Total),
            Shift: shiftLine);
    }

    // Live readouts: one format per changed value, never one per tick. Each
    // guard quantises to what the resx actually prints, so the cache can never
    // hold a stale-looking value.
    private ReadoutPaint PaintLive(TelemetrySample sample)
    {
        _lastWaitingKey = null;

        string? status = null;
        if (sample.PacketId != _lastPacketId)
        {
            _lastPacketId = sample.PacketId;
            status = Strings.Format("EvoLive", sample.PacketId);
        }

        string? speed = null;
        int speedKmh = (int)Math.Round(sample.SpeedKmh);
        if (speedKmh != _lastSpeedKmh)
        {
            _lastSpeedKmh = speedKmh;
            speed = Strings.Format("SpeedKmh", sample.SpeedKmh);
        }

        string? gear = null;
        if (sample.Gear != _lastGear)
        {
            _lastGear = sample.Gear;
            gear = TelemetryConventions.GearDisplay(sample.Gear);
        }

        string? detail = null;
        int rpm = sample.Rpm;
        int throttle = (int)Math.Round(sample.Gas * 100.0f);
        int brake = (int)Math.Round(sample.Brake * 100.0f);
        if (rpm != _lastRpm || throttle != _lastThrottlePct || brake != _lastBrakePct)
        {
            _lastRpm = rpm;
            _lastThrottlePct = throttle;
            _lastBrakePct = brake;
            detail = Strings.Format("EvoDetail", sample.Rpm, sample.Gas, sample.Brake);
        }

        return new ReadoutPaint(Live: true, Status: status, Speed: speed, Gear: gear, Detail: detail);
    }

    // Invariant like every other clock readout: the shift line is a duration,
    // not a localised number.
    private static string FormatRemaining(TimeSpan remaining) =>
        remaining.ToString(@"m\:ss", CultureInfo.InvariantCulture);
}
