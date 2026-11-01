namespace AceDrive.Core.Tests;

/// <summary>
/// The calendar's once-per-week stamps: an event the strip opened on its day is
/// closed for the rest of that day, each event keeps its own slot, and a restart
/// inside the same week reopens the stamps instead of the event.
/// </summary>
public sealed class WeeklyEventLedgerTests
{
    // Tuesday 2026-10-13 noon: the road test's weekday, inside the event window.
    private static readonly DateTime TuesdayNoon =
        new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TakenOn_IsFalseUntilTheEventIsStamped()
    {
        var ledger = new WeeklyEventLedger();

        Assert.False(ledger.TakenOn(ControlCalendar.RoadTest, TuesdayNoon));

        ledger.MarkTaken(ControlCalendar.RoadTest, TuesdayNoon);

        Assert.True(ledger.TakenOn(ControlCalendar.RoadTest, TuesdayNoon));
        // The stamps are per event: one row taking its day leaves the others.
        Assert.False(ledger.TakenOn(ControlCalendar.CentoMiglia, TuesdayNoon));
    }

    [Fact]
    public void TakenOn_ReopensOnTheNextWeek()
    {
        var ledger = new WeeklyEventLedger();
        ledger.MarkTaken(ControlCalendar.RoadTest, TuesdayNoon);

        Assert.False(ledger.TakenOn(ControlCalendar.RoadTest, TuesdayNoon.AddDays(7)));
    }

    [Fact]
    public void Snapshot_ReopensTheStampsAfterARestart()
    {
        var ledger = new WeeklyEventLedger();
        ledger.MarkTaken(ControlCalendar.RoadTest, TuesdayNoon);
        ledger.MarkTaken(ControlCalendar.CentoMiglia, TuesdayNoon);

        // The saver writes the cached snapshot into the driver file; a restart
        // feeds that map back in, exactly like the root does.
        var saved = DriverState.Fresh with { WeeklyEventDates = ledger.Snapshot };
        var resumed = new WeeklyEventLedger(saved);

        Assert.True(resumed.TakenOn(ControlCalendar.RoadTest, TuesdayNoon));
        Assert.True(resumed.TakenOn(ControlCalendar.CentoMiglia, TuesdayNoon));
        Assert.Equal("2026-10-13", resumed.Snapshot[ControlCalendar.RoadTest]);
    }

    [Fact]
    public void AStampThatIsNotADate_IsDropped()
    {
        // A hand-edited or corrupt stamp can never match a game date, so it is
        // forgotten instead of hiding behind a row that looks taken.
        var saved = DriverState.Fresh with
        {
            WeeklyEventDates = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ControlCalendar.RoadTest] = "sometime",
            },
        };

        var ledger = new WeeklyEventLedger(saved);

        Assert.False(ledger.TakenOn(ControlCalendar.RoadTest, TuesdayNoon));
        Assert.Empty(ledger.Snapshot);
    }
}
