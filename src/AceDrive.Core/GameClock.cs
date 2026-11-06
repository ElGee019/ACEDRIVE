namespace AceDrive.Core;

/// <summary>
/// Accelerated game time for the week system (PORTPLAN item 50). An
/// independent clock: its own saved moment plus real elapsed time at a fixed
/// ~27x, never EVO's time of day. The wall clock flows in through
/// <see cref="IClock"/>, so FakeClock tests stay deterministic. Stateless
/// apart from its two anchors: no tick counting, no events yet (day-change
/// logging lives with the caller, which owns the ILog).
/// </summary>
public sealed class GameClock
{
    // WHY ~27x: a full game day then lasts ~53 real minutes, so the calendar
    // strip turns over at a readable pace. The rate is AceDrive's own (2.2
    // real seconds per game minute), not Unity's faster tick and not EVO's
    // clock: a pace read off the game would jump whenever the player pauses
    // or loads a session.
    public const double RealSecondsPerGameMinute = 2.2;

    private const int MinutesPerDay = 24 * 60;

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

    /// <summary>
    /// Whole game minutes since the calendar's zero point. Unity's
    /// <c>CurrentGameMinute()</c> is nonzero from the first minute, so the
    /// job legs stamp this at each end and price the difference: a game-time
    /// drive is paid in game minutes rather than real seconds, and a pause
    /// cannot inflate a haul.
    /// </summary>
    public long GameMinute => (long)(GameNow - DateTime.MinValue).TotalMinutes;

    /// <summary>
    /// Whole game days since the calendar's zero point. The daily schedules key
    /// on this (the car-sharing batch is one per day), so two moments on the
    /// same day share one plan and a rollover starts the next.
    /// </summary>
    public int GameDay => (int)(GameMinute / MinutesPerDay);

    /// <summary>
    /// Minute of the current game day (00:00 = 0, 23:59 = 1439). A daily
    /// schedule is authored in these minutes, so a page converting one to an
    /// absolute <see cref="GameMinute"/> needs this reading.
    /// </summary>
    public int GameMinuteOfDay => (int)(GameNow.TimeOfDay.TotalMinutes);
}
