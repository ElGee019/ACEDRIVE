using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The calendar's availability rule on its own: the strip's taken rows and the
/// menu's calendar tile read the same answer, each event closing on the ledger
/// its own flow refuses on.
/// </summary>
public sealed class CalendarGateTests
{
    // 2026-10-12 is a Monday; the Sunday before it carries the Cento Miglia.
    private static readonly DateTime MondayNoon = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SundayNoon = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WednesdayNoon = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ThursdayNoon = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SaturdayNoon = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OpenEventNow_OpensTheWeekdaysOwnEvent()
    {
        CalendarGate gate = NewGate();

        Assert.Equal(ControlCalendar.Valuation, gate.OpenEventNow(MondayNoon, 1));
        Assert.Equal(ControlCalendar.CentoMiglia, gate.OpenEventNow(SundayNoon, 1));
        Assert.Null(gate.OpenEventNow(ThursdayNoon, 1));
        Assert.Null(gate.OpenEventNow(MondayNoon.AddHours(9), 1)); // 9pm, past the gate
        Assert.Null(gate.OpenEventNow(SaturdayNoon, 1)); // the run's first week
        Assert.Equal(
            ControlCalendar.Touristenfahrten, gate.OpenEventNow(SaturdayNoon, 2));
    }

    [Fact]
    public void OpenEventNow_ClosesEachEventOnItsOwnStamp()
    {
        var weekly = new WeeklyEventLedger();
        var willy = new WhereIsWillyLedger();
        var valuation = new ValuationLedger();
        var gate = new CalendarGate(weekly, willy, valuation);

        weekly.MarkTaken(ControlCalendar.CentoMiglia, SundayNoon);
        Assert.Null(gate.OpenEventNow(SundayNoon, 1));
        // The next Sunday is a new date, so the slot is the new week's again.
        Assert.Equal(
            ControlCalendar.CentoMiglia, gate.OpenEventNow(SundayNoon.AddDays(7), 2));

        willy.MarkCompleted(WednesdayNoon);
        Assert.Null(gate.OpenEventNow(WednesdayNoon, 1));

        valuation.MarkEntry("ks_mazda_mx5_na", 100.0, ValuationRules.WeekKey(MondayNoon), MondayNoon);
        Assert.Null(gate.OpenEventNow(MondayNoon, 1));
    }

    [Fact]
    public void Taken_AnswersOnlyTheEventsOwnEvent()
    {
        var weekly = new WeeklyEventLedger();
        var gate = NewGate(weekly);

        weekly.MarkTaken(ControlCalendar.CentoMiglia, SundayNoon);

        Assert.True(gate.Taken(ControlCalendar.CentoMiglia, SundayNoon));
        Assert.False(gate.Taken(ControlCalendar.RoadTest, SundayNoon));
        Assert.False(gate.Taken(null, SundayNoon));
    }

    private static CalendarGate NewGate(
        WeeklyEventLedger? weekly = null,
        WhereIsWillyLedger? willy = null,
        ValuationLedger? valuation = null) =>
        new(weekly ?? new WeeklyEventLedger(), willy ?? new WhereIsWillyLedger(),
            valuation ?? new ValuationLedger());
}
