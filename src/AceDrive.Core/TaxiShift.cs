// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11-13.
namespace AceDrive.Core;

/// <summary>Minimal shift accumulator (PORTPLAN item 12).
/// A shift opens on the first taxi job and idles out 30 minutes after the
/// last dropoff (Unity TaxiShiftService rule): the button reads START while
/// closed and CONTINUE while open, and the header counts down to the end.
/// The stamp is real wall-clock time, so restoring a snapshot reopens the
/// same shift — or times it out — exactly as if the process had never
/// stopped. Time is always passed in (IClock at the call site) so tests stay
/// deterministic; the parameterless RecordCompleted keeps the legacy
/// accumulate-without-stamping path.</summary>
public sealed class TaxiShift
{
    /// <summary>Idle minutes after the last dropoff before a shift ends.</summary>
    public const double TimeoutMinutes = 30.0;

    public TaxiShift(TaxiShiftSnapshot? saved = null)
    {
        if (saved is null) return;
        IsStarted = saved.IsStarted;
        StartedAt = saved.StartedAt;
        LastJobEnd = saved.LastJobEnd;
        JobsDone = Math.Max(0, saved.JobsDone);
        Earned = Math.Max(0.0, saved.Earned);
    }

    public int JobsDone { get; private set; }
    public double Earned { get; private set; }
    public bool IsStarted { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime LastJobEnd { get; private set; }

    /// <summary>Opens a fresh shift: counters reset, idle clock starts now.</summary>
    public void Start(DateTime now)
    {
        IsStarted = true;
        StartedAt = now;
        LastJobEnd = now;
        JobsDone = 0;
        Earned = 0;
    }

    public void RecordCompleted(TaxiJobState job)
    {
        if (job.Phase != JobPhase.Complete) return;
        JobsDone++;
        Earned = Math.Round(Earned + job.Total, 2);
    }

    /// <summary>Records a dropoff and stamps the idle clock (when open).</summary>
    public void RecordCompleted(TaxiJobState job, DateTime now)
    {
        if (job.Phase != JobPhase.Complete) return;
        JobsDone++;
        Earned = Math.Round(Earned + job.Total, 2);
        if (IsStarted) LastJobEnd = now;
    }

    /// <summary>True when open and idle since the last dropoff hit the timeout.</summary>
    public bool IsTimedOut(DateTime now) =>
        IsStarted && (now - LastJobEnd).TotalMinutes >= TimeoutMinutes;

    /// <summary>Time left before the open shift idles out; zero when closed.</summary>
    public TimeSpan Remaining(DateTime now)
    {
        if (!IsStarted) return TimeSpan.Zero;
        TimeSpan remaining = TimeSpan.FromMinutes(TimeoutMinutes) - (now - LastJobEnd);
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    /// <summary>Ends an idle-timed-out shift, keeping counters for the receipt.
    /// Returns false (nothing mutated) when the shift is not timed out.</summary>
    public bool TryEndTimedOut(DateTime now, out TimeSpan idle)
    {
        idle = TimeSpan.Zero;
        if (!IsTimedOut(now)) return false;
        idle = now - LastJobEnd;
        IsStarted = false;
        return true;
    }

    /// <summary>The state the next session resumes from, stamps included.</summary>
    public TaxiShiftSnapshot Snapshot() =>
        new(IsStarted, StartedAt, LastJobEnd, JobsDone, Earned);
}
