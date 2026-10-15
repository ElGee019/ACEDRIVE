using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Game time runs at half the Unity clock speed and carries the
/// calendar days with it (PORTPLAN item 50).</summary>
public sealed class GameClockTests
{
    [Fact]
    public void HalfSpeed_TwoPointTwoSecondsPerGameMinute()
    {
        // Unity ticks one game minute per 1.1 real seconds; AceDrive halves
        // that pace, so 2.2 real seconds make one game minute.
        var clock = new FakeClock();
        var game = new GameClock(clock, clock.UtcNow);
        clock.Advance(TimeSpan.FromSeconds(2.2));
        Assert.Equal(new DateTime(2026, 10, 12, 12, 1, 0, DateTimeKind.Utc), game.GameNow);
    }

    [Fact]
    public void FullGameDay_TakesAboutFiftyThreeRealMinutes()
    {
        var clock = new FakeClock();
        var game = new GameClock(clock, clock.UtcNow);
        clock.Advance(TimeSpan.FromMinutes(52.8));
        Assert.Equal(new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc), game.GameNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Today_FollowsGameDaylight()
    {
        var clock = new FakeClock();
        var game = new GameClock(clock, clock.UtcNow);
        Assert.Equal(2, game.Today); // Monday
        clock.Advance(TimeSpan.FromMinutes(53));
        Assert.Equal(3, game.Today); // Tuesday 12:05
    }

    [Fact]
    public void FreshSave_MirrorsUnitySaturdayMorning()
    {
        // Unity PlayerDefaults open on Saturday 08:00, dayOfWeek 7: the
        // production game start must land on the same morning.
        var clock = new FakeClock();
        var game = new GameClock(clock, new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc), game.GameNow);
        Assert.Equal(7, game.Today);
    }
}
