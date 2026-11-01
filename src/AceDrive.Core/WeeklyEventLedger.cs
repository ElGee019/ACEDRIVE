// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The calendar's once-per-week stamps: the game date each event was last taken,
// so a week that already opened an event cannot open it a second time from the
// strip (Unity's per-event "one entry a week" bookkeeping, e.g.
// lastValuationGameDay and the Mille Miglia history entry).
namespace AceDrive.Core;

using System.Globalization;

/// <summary>
/// One stamp per calendar event: the game date it was last taken on. WHY a game
/// date and not a week number: the strip already opens an event only on its own
/// weekday, so that day is the week's one slot — a date needs no week
/// arithmetic, survives a hand-edited save and reads like
/// <see cref="WhereIsWillyLedger"/>'s completion stamp.
/// </summary>
public sealed class WeeklyEventLedger
{
    /// <summary>The invariant date shape the stamps are stored in.</summary>
    private const string DateFormat = "yyyy-MM-dd";

    private readonly Dictionary<string, DateTime> _dates = new(StringComparer.Ordinal);

    public WeeklyEventLedger(DriverState? saved = null)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        if (saved?.WeeklyEventDates is { } savedDates)
        {
            foreach ((string eventId, string date) in savedDates)
            {
                // A stamp that is not a real date is dropped: it can never match
                // a game date anyway, and keeping garbage would hide a corrupt
                // file behind a row that looks taken.
                if (Parse(date) is not { } parsed)
                {
                    continue;
                }

                _dates[eventId] = parsed;
                snapshot[eventId] = date;
            }
        }

        Snapshot = snapshot;
    }

    /// <summary>
    /// The stamps a save must reopen, event id to game date. Cached: the saver
    /// reads it on every bank and this class is its only writer.
    /// </summary>
    public IReadOnlyDictionary<string, string> Snapshot { get; private set; }

    /// <summary>True when the event was already taken on this game date.</summary>
    public bool TakenOn(string eventId, DateTime gameNow) =>
        _dates.TryGetValue(eventId, out DateTime taken) && taken.Date == gameNow.Date;

    /// <summary>
    /// Stamps the event the calendar just opened. Written before the driver file
    /// is banked, so a kill between the two can never reopen the same week.
    /// </summary>
    public void MarkTaken(string eventId, DateTime gameNow)
    {
        _dates[eventId] = gameNow.Date;
        var snapshot = new Dictionary<string, string>(Snapshot, StringComparer.Ordinal)
        {
            [eventId] = KeyOf(gameNow),
        };
        Snapshot = snapshot;
    }

    private static string KeyOf(DateTime moment) =>
        moment.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static DateTime? Parse(string? date) =>
        date is { Length: > 0 }
            && DateTime.TryParseExact(
                date, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed)
            ? parsed.Date
            : null;
}
