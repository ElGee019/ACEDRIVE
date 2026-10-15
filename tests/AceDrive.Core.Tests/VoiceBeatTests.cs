using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The shared voice beat defers actions on wall time (FakeClock),
/// never tick counts; latest tap wins and cancel drops cleanly.</summary>
public sealed class VoiceBeatTests
{
    [Fact]
    public void Defer_RunsAfterTheBeat_NotBefore()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool fired = false;
        beat.Defer(() => fired = true);

        beat.Poll();
        Assert.False(fired); // still inside the beat

        clock.Advance(VoiceBeat.Duration - TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.False(fired);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.True(fired);
    }

    [Fact]
    public void Defer_RunsOnce()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        int count = 0;
        beat.Defer(() => count++);

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        beat.Poll();
        Assert.Equal(1, count);
    }

    [Fact]
    public void Defer_LatestTapWins()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool first = false;
        bool second = false;
        beat.Defer(() => first = true);
        beat.Defer(() => second = true);

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.False(first);
        Assert.True(second);
    }

    [Fact]
    public void Cancel_DropsThePendingTap()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool fired = false;
        beat.Defer(() => fired = true);
        beat.Cancel();

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.False(fired);
    }

    [Fact]
    public void Deadline_And_IsDue_TrackTheSameBeat()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        DateTime deadline = beat.Deadline();
        Assert.Equal(clock.UtcNow + VoiceBeat.Duration, deadline);
        Assert.False(beat.IsDue(deadline));
        clock.Advance(VoiceBeat.Duration);
        Assert.True(beat.IsDue(deadline));
    }

    [Fact]
    public void Duration_IsHalfASecond()
    {
        // Pinned by the user: the acknowledgment glow and the navigation
        // wait share this one beat.
        Assert.Equal(TimeSpan.FromSeconds(0.5), VoiceBeat.Duration);
    }
}
