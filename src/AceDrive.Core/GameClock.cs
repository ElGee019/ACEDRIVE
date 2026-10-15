namespace AceDrive.Core;

/// <summary>
/// Accelerated game time for the week system (PORTPLAN item 50). The wall
/// clock flows in through <see cref="IClock"/>; game minutes accumulate from
/// real elapsed time so FakeClock tests stay deterministic. Stateless apart
/// from its two anchors: no tick counting, no events yet (day-change logging
/// lives with the caller, which owns the ILog).
/// </summary>
public sealed class GameClock
{
    // WHY half of Unity's GameTimeConfigSO.DefaultTickInterval (1.1 real
    // seconds per game minute): a full game day then lasts ~53 real minutes,
    // so the calendar strip turns over at a readable pace on the overlay.
    public const double RealSecondsPerGameMinute = 2.2;

    private readonly IClock _clock;
    private readonly DateTime _realStart;
    private readonly DateTime _gameStart;

    public GameClock(IClock clock, DateTime gameStart)
    {
        _clock = clock;
        _realStart = clock.UtcNow;
        _gameStart = gameStart;
    }

    /// <summary>Current game moment. Computed, never counted.</summary>
    public DateTime GameNow =>
        _gameStart + TimeSpan.FromMinutes((_clock.UtcNow - _realStart).TotalSeconds / RealSecondsPerGameMinute);

    /// <summary>Calendar day, 1=Sunday..7=Saturday (Unity day numbering).</summary>
    public int Today => ControlCalendar.ToDayOfWeek(GameNow);
}
