namespace AceDrive.Core;

/// <summary>
/// Voice acknowledgment beat: every voice tap flashes its button teal for
/// <see cref="Duration"/>, then acts. Page changes wait out the beat so the
/// glow stays visible before the screen flips; mouse clicks bypass it and
/// act at once. Single pending slot, latest tap wins; the 10 Hz poll drains
/// it via <see cref="Poll"/>. Time flows through <see cref="IClock"/> (never
/// tick counts) so FakeClock tests it deterministically.
/// </summary>
public sealed class VoiceBeat(IClock clock)
{
    /// <summary>Acknowledgment glow and navigation wait, one shared beat app-wide.</summary>
    public static TimeSpan Duration { get; } = TimeSpan.FromSeconds(0.5);

    private Action? _pending;
    private DateTime _due;

    /// <summary>Arms the tap for the end of the beat, replacing any older pending tap.</summary>
    public void Defer(Action action)
    {
        _pending = action;
        _due = clock.UtcNow + Duration;
    }

    /// <summary>Drops the pending tap: newer input (click, key, wake word) or
    /// a real screen change already superseded it.</summary>
    public void Cancel() => _pending = null;

    /// <summary>Runs the due tap, if any. One null check per tick when idle.</summary>
    public void Poll()
    {
        if (_pending is null || clock.UtcNow < _due) return;
        Action act = _pending;
        _pending = null;
        act();
    }

    /// <summary>Flash deadline for a tap lit right now.</summary>
    public DateTime Deadline() => clock.UtcNow + Duration;

    /// <summary>True once the beat after the deadline has passed.</summary>
    public bool IsDue(DateTime deadline) => clock.UtcNow >= deadline;
}
