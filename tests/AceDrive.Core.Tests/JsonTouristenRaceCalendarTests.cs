using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// The shipped real-world Nürburgring calendar parses into the record shape the
/// briefing title and the payout bonus read: the given 2027 dates, each with its
/// event name, and a doubled payout while an event runs. A missing file degrades
/// to no events and no bonus with one warning.
/// </summary>
public sealed class JsonTouristenRaceCalendarTests
{
    [Fact]
    public void ShippedTable_LoadsEveryGivenEvent()
    {
        string file = Path.Combine(
            AppContext.BaseDirectory, "Data", "touristenweekends.json");
        TouristenRaceCalendar calendar =
            new JsonTouristenRaceCalendar(file, new TestLog()).Load();

        Assert.True(calendar.IsUsable);
        Assert.Equal(15, calendar.Weekends.Count);
        Assert.Equal(2.0, calendar.BonusMultiplier);
    }

    [Fact]
    public void ShippedTable_NamesTheEventOnItsOwnDays()
    {
        string file = Path.Combine(
            AppContext.BaseDirectory, "Data", "touristenweekends.json");
        TouristenRaceCalendar calendar =
            new JsonTouristenRaceCalendar(file, new TestLog()).Load();

        // The March opener, the 24h week itself and the second test day of the
        // April meeting all name their own event; the day after pays normally.
        Assert.Equal(
            "NLS Round 1", calendar.Find(new DateOnly(2027, 3, 20))?.Name);
        Assert.Equal(
            "55th ADAC RAVENOL 24h Nürburgring",
            calendar.Find(new DateOnly(2027, 5, 29))?.Name);
        Assert.Equal(
            "ADAC 24h Test Days", calendar.Find(new DateOnly(2027, 4, 4))?.Name);
        Assert.Null(calendar.Find(new DateOnly(2027, 3, 21)));
        Assert.Equal(2.0, calendar.Multiplier(new DateOnly(2027, 3, 20)));
        Assert.Equal(1.0, calendar.Multiplier(new DateOnly(2027, 3, 21)));
    }

    [Fact]
    public void Load_MissingFile_FallsBackWithWarning()
    {
        var log = new TestLog();
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

        TouristenRaceCalendar calendar = new JsonTouristenRaceCalendar(file, log).Load();

        Assert.Empty(calendar.Weekends);
        Assert.Equal(1.0, calendar.Multiplier(new DateOnly(2027, 3, 20)));
        Assert.Single(log.Warnings);
    }
}
