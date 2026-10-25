// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity's passive customer asks).
namespace AceDrive.Core;

/// <summary>
/// What an accepted passive ask expects of the ride, in data rather than
/// words: a delivery deadline and/or live limits that void the tip once
/// broken. Every part is optional; an empty condition is a plain
/// ACCEPT/DECLINE ask, exactly like the lines that only promise a tip.
/// Thresholds live in <see cref="RideMonitorRules"/> so the content only names
/// which limits apply.
/// </summary>
public sealed record RideCondition(
    bool Deadline = false,
    bool BrakeBrusque = false,
    bool ThrottleBrusque = false,
    bool SteeringBrusque = false,
    int? MinRpm = null,
    int? MaxRpm = null,
    double? MaxSpeedKmh = null)
{
    /// <summary>The ask promises nothing measurable (accept or refuse only).</summary>
    public static readonly RideCondition None = new();

    /// <summary>True when the ride monitor has to watch at least one limit;
    /// a deadline-only ask is tracked by the clock, not the monitor.</summary>
    public bool HasLiveLimits =>
        BrakeBrusque || ThrottleBrusque || SteeringBrusque
        || MinRpm is not null || MaxRpm is not null || MaxSpeedKmh is not null;

    /// <summary>Content sanity: a decibel-free check the store can reject a
    /// hand-edited file with. A range that can never be met is invalid.</summary>
    public bool IsValid =>
        (MinRpm is not { } low || low is >= 0 and <= 20000)
        && (MaxRpm is not { } high || high is >= 0 and <= 20000)
        && (MinRpm is not { } min || MaxRpm is not { } max || min < max)
        && (MaxSpeedKmh is not { } speed
            || (double.IsFinite(speed) && speed is > 0.0 and <= 500.0));
}
