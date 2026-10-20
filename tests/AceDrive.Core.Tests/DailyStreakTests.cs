using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The real-time login streak (item 47, Unity DailyStreakService): recount
/// from the dates at every boot, idempotent inside one day, +1 on a
/// consecutive return, reset on a gap. Time comes from FakeClock.
/// </summary>
public sealed class DailyStreakTests
{
    private static readonly DateTime Day0 = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);

    private static DailyStreak Streak(FakeClock clock, DriverState? saved = null) =>
        new(clock, saved);

    [Fact]
    public void FreshDriver_Update_GrantsDayOne()
    {
        var clock = new FakeClock { Now = Day0 };
        var streak = Streak(clock);
        Assert.Equal(1, streak.Update());
        Assert.Equal("2026-10-17", streak.LastLoginDate);
        Assert.Equal(1, streak.DaysInARow);
    }

    [Fact]
    public void SameDayRepeat_IsIdempotent()
    {
        var clock = new FakeClock { Now = Day0 };
        var streak = Streak(clock);
        streak.Update();
        clock.Advance(TimeSpan.FromHours(5));
        Assert.Equal(1, streak.Update()); // Unity: recomputed, never doubles
        Assert.Equal(1, streak.DaysInARow);
    }

    [Fact]
    public void NextDayReturn_Consecutive_AdvancesToTwo()
    {
        var clock = new FakeClock { Now = Day0 };
        var streak = Streak(clock, new DriverState(0, 0, DaysInARow: 1, LastLoginDate: "2026-10-17"));
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(2, streak.Update());
    }

    [Fact]
    public void GapDay_BreaksTheStreak()
    {
        var clock = new FakeClock { Now = Day0 };
        var streak = Streak(clock, new DriverState(0, 0, DaysInARow: 7, LastLoginDate: "2026-10-15"));
        // Last login was the 15th, today is the 17th — one day missed.
        Assert.Equal(1, streak.Update());
    }

    [Fact]
    public void StaleCounter_CannotOutliveItsDates()
    {
        // A hand-edited (or wrong-side-of-midnight) saved counter of 99 with
        // no date must start over: the dates are the source of truth.
        var clock = new FakeClock { Now = Day0 };
        var streak = Streak(clock, new DriverState(0, 0, DaysInARow: 99, LastLoginDate: "2026-10-15"));
        Assert.Equal(1, streak.Update());
    }

    [Fact]
    public void Bonus_ClaimsOncePerDay()
    {
        var clock = new FakeClock { Now = Day0 };
        var streak = Streak(clock);
        streak.Update();
        Assert.True(streak.BonusDue);
        streak.StampBonus();
        Assert.False(streak.BonusDue);
        clock.Advance(TimeSpan.FromDays(1));
        Assert.True(streak.BonusDue);
        Assert.Equal(2, streak.Update()); // consecutive return: day 2
    }

    [Fact]
    public void BonusAlreadyStampedToday_DoesNotDoubleCount_Login()
    {
        // Same-day boot after the grant: already counted today, Update keeps
        // the stored day number (Unity: Math.Max(1, stored)).
        var clock = new FakeClock { Now = Day0 };
        var streak = Streak(clock, new DriverState(0, 0, DaysInARow: 4, LastLoginDate: "2026-10-17",
            LastDailyBonusDate: "2026-10-17"));
        Assert.Equal(4, streak.Update());
        Assert.False(streak.BonusDue);
    }

    [Fact]
    public void MalformedSavedDate_DegradesToFreshStreak()
    {
        var clock = new FakeClock { Now = Day0 };
        var streak = Streak(clock, new DriverState(0, 0, DaysInARow: 9, LastLoginDate: "not-a-date"));
        Assert.Equal(1, streak.Update());
    }
}
