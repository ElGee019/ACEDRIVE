// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11-13.
namespace AceDrive.Core;

/// <summary>Taxi job lifecycle phases (PORTPLAN item 11).</summary>
public enum JobPhase
{
    None,
    Proposed,   // offered, waiting for accept
    EnRoute,    // passenger aboard, meter running
    Complete,   // dropped off, receipt shown
}

/// <summary>Plain data for one taxi job. All money in EUR.</summary>
public sealed class TaxiJobState
{
    public JobPhase Phase { get; set; } = JobPhase.None;
    public string PickupName { get; set; } = "";
    public string DropoffName { get; set; } = "";
    public double DistanceKm { get; set; }
    public double Fare { get; set; }
    public double Tip { get; set; }
    public double Total => Fare + Tip;
}

/// <summary>
/// Fare balance table. Loaded from Data/fares.json at startup; this Default
/// is the test-shell fallback (and the deterministic fixture for tests),
/// not the source of truth.
/// </summary>
public sealed record FareTable(double BaseFare, double PerKmRate, double TipRate)
{
    public static readonly FareTable Default = new(3.50, 1.80, 0.10);
}

/// <summary>Fare math. Deterministic, no I/O. Rates come from the table.</summary>
public static class TaxiFare
{
    public static double ComputeFare(double distanceKm, FareTable table) =>
        Math.Round(table.BaseFare + table.PerKmRate * Math.Max(0.0, distanceKm), 2);

    public static double ComputeTip(double fare, bool cleanDrive, FareTable table) =>
        cleanDrive ? Math.Round(fare * table.TipRate, 2) : 0.0;
}

/// <summary>Minimal shift accumulator (PORTPLAN item 12).
/// A shift opens on the first taxi job and idles out 30 minutes after the
/// last dropoff (Unity TaxiShiftService rule): the button reads START while
/// closed and CONTINUE while open, and the header counts down to the end.
/// Time is always passed in (IClock at the call site) so tests stay
/// deterministic; the parameterless RecordCompleted keeps the legacy
/// accumulate-without-stamping path.</summary>
public sealed class TaxiShift
{
    /// <summary>Idle minutes after the last dropoff before a shift ends.</summary>
    public const double TimeoutMinutes = 30.0;

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
}
