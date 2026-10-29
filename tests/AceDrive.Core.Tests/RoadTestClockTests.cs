using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The accel clock's start counter: five digits over five real seconds, the
/// random hold, then the stopwatch. Time arrives as a value, so every step is
/// asserted without waiting.
/// </summary>
public sealed class RoadTestClockTests
{
    private static readonly DateTime Start = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Countdown_CountsFiveToOneOverOneSecondEach()
    {
        var clock = new RoadTestClock(Start, holdSeconds: 1.0);

        Assert.Equal(5, clock.Countdown(Start));
        Assert.Equal(4, clock.Countdown(Start.AddSeconds(1.0)));
        Assert.Equal(3, clock.Countdown(Start.AddSeconds(2.0)));
        Assert.Equal(2, clock.Countdown(Start.AddSeconds(3.0)));
        Assert.Equal(1, clock.Countdown(Start.AddSeconds(4.0)));
    }

    [Fact]
    public void Countdown_KeepsTheOneThroughTheHold()
    {
        var clock = new RoadTestClock(Start, holdSeconds: 2.0);

        // The counter's five seconds are gone; the "1" stays up until the green
        // light, which is what hides the exact start from the player.
        Assert.Equal(1, clock.Countdown(Start.AddSeconds(5.0)));
        Assert.Equal(1, clock.Countdown(Start.AddSeconds(6.9)));
        Assert.Equal(0, clock.Countdown(Start.AddSeconds(7.0)));
    }

    [Fact]
    public void Elapsed_StaysZeroUntilTheGreenLight()
    {
        var clock = new RoadTestClock(Start, holdSeconds: 1.0);

        Assert.Equal(TimeSpan.Zero, clock.Elapsed(Start));
        Assert.Equal(TimeSpan.Zero, clock.Elapsed(Start.AddSeconds(5.9)));
        // DateTime arithmetic is tick-exact but AddSeconds is not, so compare
        // the reading at sub-tick tolerance instead of the TimeSpan.
        Assert.InRange(clock.Elapsed(Start.AddSeconds(6.1)).TotalSeconds, 0.0999, 0.1001);
        Assert.True(clock.IsGreen(Start.AddSeconds(6.0)));
        Assert.False(clock.IsGreen(Start.AddSeconds(5.999)));
    }

    [Fact]
    public void Hold_IsClampedIntoUnitysBand()
    {
        Assert.Equal(RoadTestRules.HoldMinSeconds, new RoadTestClock(Start, -5.0).HoldSeconds);
        Assert.Equal(RoadTestRules.HoldMaxSeconds, new RoadTestClock(Start, 99.0).HoldSeconds);
        Assert.Equal(RoadTestRules.HoldMinSeconds, new RoadTestClock(Start, double.NaN).HoldSeconds);
    }
}
