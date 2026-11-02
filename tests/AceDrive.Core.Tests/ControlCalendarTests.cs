using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Calendar day math is deterministic; the strip lists Monday
/// first with Sunday last, events tied to their weekday.</summary>
public sealed class ControlCalendarTests
{
    [Fact]
    public void Week_HasSevenRowsMondayFirstSundayLast()
    {
        Assert.Equal(7, ControlCalendar.Week.Count);
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(i + 2, ControlCalendar.Week[i].DayOfWeek);
        }

        Assert.Equal(ControlCalendar.Sunday, ControlCalendar.Week[6].DayOfWeek);
    }

    [Fact]
    public void Week_MapsEventsLikeUnity()
    {
        Assert.Equal(ControlCalendar.Valuation, ControlCalendar.Week[0].EventId);
        Assert.Equal(ControlCalendar.RoadTest, ControlCalendar.Week[1].EventId);
        Assert.Equal(ControlCalendar.Willy, ControlCalendar.Week[2].EventId);
        Assert.Null(ControlCalendar.Week[3].EventId);
        Assert.Equal(ControlCalendar.Pancakes, ControlCalendar.Week[4].EventId);
        Assert.Equal(ControlCalendar.CentoMiglia, ControlCalendar.Week[6].EventId);
    }

    [Fact]
    public void Week_SaturdayHostsTheTouristenfahrtenRun()
    {
        // The run is the port's own event: no Unity row, but a real flow behind
        // the Saturday button (TouristenfahrtenFlow).
        Assert.Equal(7, ControlCalendar.Week[5].DayOfWeek);
        Assert.Equal(ControlCalendar.Touristenfahrten, ControlCalendar.Week[5].EventId);
        Assert.Equal(2, ControlCalendar.Week[5].OpensFromWeek);
    }

    [Fact]
    public void IsOpenInWeek_TouristenfahrtenWaitsForItsSecondWeek()
    {
        // The run's row is not part of the first week's strip: the career's
        // first Saturday has to be behind the player. Every other row opens at
        // once, and a weekday with no row has nothing to gate.
        Assert.False(ControlCalendar.IsOpenInWeek(7, 1));
        Assert.True(ControlCalendar.IsOpenInWeek(7, 2));
        Assert.True(ControlCalendar.IsOpenInWeek(7, 9));

        foreach (CalendarDay day in ControlCalendar.Week)
        {
            if (day.DayOfWeek != 7)
            {
                Assert.True(ControlCalendar.IsOpenInWeek(day.DayOfWeek, 1));
            }
        }

        Assert.True(ControlCalendar.IsOpenInWeek(5, 1)); // Thursday: no row
    }

    [Fact]
    public void ToDayOfWeek_MondayIsTwo()
    {
        // 2026-10-12 is a Monday.
        Assert.Equal(2, ControlCalendar.ToDayOfWeek(new DateTime(2026, 10, 12, 12, 0, 0)));
        Assert.Equal(1, ControlCalendar.ToDayOfWeek(new DateTime(2026, 10, 11, 12, 0, 0)));
    }

    [Fact]
    public void IsEventWindow_EightToEight()
    {
        Assert.False(ControlCalendar.IsEventWindow(7));
        Assert.True(ControlCalendar.IsEventWindow(8));
        Assert.True(ControlCalendar.IsEventWindow(19));
        Assert.False(ControlCalendar.IsEventWindow(20));
    }

    [Fact]
    public void IsPlayableNow_OpensOnlyAnEventsOwnWeekdayInsideItsWindow()
    {
        // Monday 12:00 carries the valuation event: playable. The window's
        // edges close it, an empty weekday is never playable, and Saturday's
        // run waits for its second week.
        Assert.True(ControlCalendar.IsPlayableNow(new DateTime(2026, 10, 12, 12, 0, 0), 1));
        Assert.True(ControlCalendar.IsPlayableNow(new DateTime(2026, 10, 12, 8, 0, 0), 1));
        Assert.True(ControlCalendar.IsPlayableNow(new DateTime(2026, 10, 12, 19, 59, 0), 1));
        Assert.False(ControlCalendar.IsPlayableNow(new DateTime(2026, 10, 12, 7, 59, 0), 1));
        Assert.False(ControlCalendar.IsPlayableNow(new DateTime(2026, 10, 12, 20, 0, 0), 1));
        Assert.False(ControlCalendar.IsPlayableNow(new DateTime(2026, 10, 15, 12, 0, 0), 1)); // Thursday
        Assert.False(ControlCalendar.IsPlayableNow(new DateTime(2026, 10, 17, 12, 0, 0), 1));
        Assert.True(ControlCalendar.IsPlayableNow(new DateTime(2026, 10, 17, 12, 0, 0), 2));
    }

    [Fact]
    public void Keywords_GarbleProneEventsOnly()
    {
        // The fragments Vosk actually emits for the hard event names,
        // harvested from "voice heard" log lines.
        IReadOnlyList<string> cento = ControlCalendar.KeywordsFor(ControlCalendar.CentoMiglia);
        Assert.Contains("cento", cento);
        Assert.Contains("chantal", cento); // heard as a whole word
        Assert.Contains("gentlemen leah", cento); // heard as a two-word split
        Assert.Equal("where is", string.Join('|', ControlCalendar.KeywordsFor(ControlCalendar.Willy)));
        Assert.Empty(ControlCalendar.KeywordsFor(ControlCalendar.Valuation));
        Assert.Empty(ControlCalendar.KeywordsFor(ControlCalendar.RoadTest));
        Assert.Empty(ControlCalendar.KeywordsFor(ControlCalendar.Pancakes));
        Assert.Empty(ControlCalendar.KeywordsFor(null));
    }
}
