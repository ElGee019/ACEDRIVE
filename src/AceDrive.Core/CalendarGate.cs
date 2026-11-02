// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The calendar's availability rule: whether the day's event can still be
// played. The strip paints a used-up row as done and the menu's calendar tile
// rests, so both read this one place instead of each joining the weekday table
// to the ledgers that stamp their own event.
namespace AceDrive.Core;

/// <summary>
/// What the calendar can still open: the weekday/week/window rule of
/// <see cref="ControlCalendar"/> joined to the once-a-week stamps the event
/// ledgers keep. Each event keeps its own bookkeeping — the weekly ledger for
/// the road test, Cento Miglia and Touristenfahrten, the missing-person run's
/// completion stamp, the valuation week's entry — and this class is the one
/// reader that maps an event id to the ledger that answers for it.
/// </summary>
public sealed class CalendarGate
{
    private readonly WeeklyEventLedger _weekly;
    private readonly WhereIsWillyLedger _willy;
    private readonly ValuationLedger _valuation;

    public CalendarGate(
        WeeklyEventLedger weekly, WhereIsWillyLedger willy, ValuationLedger valuation)
    {
        _weekly = weekly;
        _willy = willy;
        _valuation = valuation;
    }

    /// <summary>
    /// The event the moment can open now, or null when there is nothing left
    /// to play: an empty weekday, a week the row has not appeared in yet, an
    /// hour outside 8am-8pm, or a slot already used up.
    /// </summary>
    public string? OpenEventNow(DateTime gameNow, int week)
    {
        string? eventId = ControlCalendar.EventOfDay(ControlCalendar.ToDayOfWeek(gameNow));
        return eventId is not null
            && ControlCalendar.IsPlayableNow(gameNow, week)
            && !Taken(eventId, gameNow)
            ? eventId
            : null;
    }

    /// <summary>
    /// True once the event's slot is used up, each event reading the ledger its
    /// own flow refuses on: the same answer the tap would get, so the strip and
    /// the menu never promise an entry the event would turn down.
    /// </summary>
    public bool Taken(string? eventId, DateTime gameNow)
    {
        if (eventId == ControlCalendar.Willy)
        {
            return _willy.CompletedOn(gameNow);
        }

        if (eventId == ControlCalendar.Valuation)
        {
            // The valuation counts entries by its own absolute week key, not
            // the calendar's display week (see ValuationRules.WeekKey).
            return _valuation.HasEntry(ValuationRules.WeekKey(gameNow));
        }

        return eventId is not null && _weekly.TakenOn(eventId, gameNow);
    }
}
