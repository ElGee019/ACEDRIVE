// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 49 and LATESTDRIVEBS2 RoadTestScreen's
// AccelCountdownRoutine + TimerRoutine.
namespace AceDrive.Core;

/// <summary>
/// The accel leg's clock: the start counter, the reaction hold and then the
/// stopwatch the result is read from. Pure state — the caller hands it the
/// moment it polls, so a unit test drives a whole countdown without a real
/// clock and the 10 Hz poll never owns a timer of its own.
/// </summary>
public sealed class RoadTestClock
{
    private readonly DateTime _startedAt;
    private readonly DateTime _greenAt;

    public RoadTestClock(DateTime startedAt, double holdSeconds)
    {
        _startedAt = startedAt;
        HoldSeconds = RoadTestRules.ClampHold(holdSeconds);
        _greenAt = startedAt.AddSeconds(RoadTestRules.CountdownSteps + HoldSeconds);
    }

    /// <summary>The hold this run drew, clamped into Unity's band.</summary>
    public double HoldSeconds { get; }

    /// <summary>True once the counter ran out: the stopwatch is the readout.</summary>
    public bool IsGreen(DateTime now) => now >= _greenAt;

    /// <summary>
    /// The digit to paint, 0 at green. The counter counts down one digit per
    /// second and keeps the "1" up through the hold, so the player never learns
    /// the exact start from the counter alone; the timer appearing is the green
    /// light.
    /// </summary>
    public int Countdown(DateTime now)
    {
        if (now >= _greenAt)
        {
            return 0;
        }

        double seconds = (now - _startedAt).TotalSeconds;
        return Math.Max(1, RoadTestRules.CountdownSteps - (int)seconds);
    }

    /// <summary>Time since the green light; zero while the counter and hold run.</summary>
    public TimeSpan Elapsed(DateTime now) =>
        now > _greenAt ? now - _greenAt : TimeSpan.Zero;
}
