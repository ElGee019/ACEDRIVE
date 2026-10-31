using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The real-world Nürburgring calendar headless: a listed single day and every
/// day inside a listed range name their event, the days around them do not, and
/// the bonus factor follows the same lookup — so the briefing title and the
/// doubled goods can never disagree about whether it is a race weekend.
/// </summary>
public sealed class TouristenRaceCalendarTests
{
    private static TouristenRaceCalendar Calendar() => new(
        [
            new TouristenRaceWeekend(
                new DateOnly(2027, 3, 20), new DateOnly(2027, 3, 20), "NLS Round 1"),
            new TouristenRaceWeekend(
                new DateOnly(2027, 5, 27), new DateOnly(2027, 5, 30),
                "55th ADAC RAVENOL 24h Nürburgring"),
        ],
        2.0);

    [Fact]
    public void Find_MatchesASingleDayAndEveryDayOfARange()
    {
        TouristenRaceCalendar calendar = Calendar();

        Assert.Equal("NLS Round 1",
            calendar.Find(new DateOnly(2027, 3, 20))?.Name);
        Assert.Equal(
            "55th ADAC RAVENOL 24h Nürburgring",
            calendar.Find(new DateOnly(2027, 5, 27))?.Name);
        Assert.Equal(
            "55th ADAC RAVENOL 24h Nürburgring",
            calendar.Find(new DateOnly(2027, 5, 29))?.Name);
        Assert.Equal(
            "55th ADAC RAVENOL 24h Nürburgring",
            calendar.Find(new DateOnly(2027, 5, 30))?.Name);
    }

    [Fact]
    public void Find_LeavesTheDaysAroundAnEventAlone()
    {
        TouristenRaceCalendar calendar = Calendar();

        Assert.Null(calendar.Find(new DateOnly(2027, 3, 19)));
        Assert.Null(calendar.Find(new DateOnly(2027, 3, 21)));
        Assert.Null(calendar.Find(new DateOnly(2027, 5, 26)));
        Assert.Null(calendar.Find(new DateOnly(2027, 5, 31)));
    }

    [Fact]
    public void Multiplier_IsTheBonusInsideAnEventAndOneOutside()
    {
        TouristenRaceCalendar calendar = Calendar();

        Assert.Equal(2.0, calendar.Multiplier(new DateOnly(2027, 5, 28)));
        Assert.Equal(2.0, calendar.Multiplier(new DateOnly(2027, 3, 20)));
        Assert.Equal(1.0, calendar.Multiplier(new DateOnly(2027, 3, 21)));
    }

    [Fact]
    public void None_PaysPlainPricesEveryDay()
    {
        Assert.Null(TouristenRaceCalendar.None.Find(new DateOnly(2027, 3, 20)));
        Assert.Equal(1.0, TouristenRaceCalendar.None.Multiplier(new DateOnly(2027, 3, 20)));
    }

    [Fact]
    public void Multiplier_FallsBackToOneWhenTheFactorIsNotUsable()
    {
        // A hand-edited file must never make a delivery cheaper, nor crash it.
        var weekend = new TouristenRaceWeekend(
            new DateOnly(2027, 3, 20), new DateOnly(2027, 3, 20), "NLS Round 1");
        DateOnly day = new DateOnly(2027, 3, 20);

        Assert.Equal(1.0, new TouristenRaceCalendar([weekend], 0.5).Multiplier(day));
        Assert.Equal(1.0, new TouristenRaceCalendar([weekend], double.NaN).Multiplier(day));
    }

    [Fact]
    public void IsUsable_RejectsBrokenRowsAndAcceptsAWholeTable()
    {
        Assert.True(Calendar().IsUsable);
        Assert.False(TouristenRaceCalendar.None.IsUsable);
        Assert.False(new TouristenRaceCalendar(
            [new TouristenRaceWeekend(
                new DateOnly(2027, 5, 30), new DateOnly(2027, 5, 27), "Backwards")],
            2.0).IsUsable);
        Assert.False(new TouristenRaceCalendar(
            [new TouristenRaceWeekend(
                new DateOnly(2027, 3, 20), new DateOnly(2027, 3, 20), "  ")],
            2.0).IsUsable);
        Assert.False(new TouristenRaceCalendar(
            [new TouristenRaceWeekend(
                new DateOnly(2027, 3, 20), new DateOnly(2027, 3, 20), "NLS")],
            0.5).IsUsable);
    }
}
