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

/// <summary>Minimal shift accumulator (PORTPLAN item 12).</summary>
public sealed class TaxiShift
{
    public int JobsDone { get; private set; }
    public double Earned { get; private set; }

    public void RecordCompleted(TaxiJobState job)
    {
        if (job.Phase != JobPhase.Complete) return;
        JobsDone++;
        Earned = Math.Round(Earned + job.Total, 2);
    }
}
