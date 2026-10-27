// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The weekly missing-person run's persisted half (ToWhereIsWilly.md §2.9/§3.1,
// Unity PlayerData.lastWillyGameDay + lastWillyYear): only the completion stamp
// survives a restart; the search itself is session state.
namespace AceDrive.Core;

using System.Globalization;

/// <summary>
/// The one fact a restart must reopen for the Wednesday run: the game date its
/// receipt last stamped, so the calendar row closes for the rest of that
/// Wednesday and reopens the next week. WHY a game date instead of Unity's
/// <c>gameDay + year</c> pair: one string carries both halves, survives a year
/// boundary and a hand-edited save, and the gate is "the same Wednesday", not a
/// day counter.
/// </summary>
public sealed class WhereIsWillyLedger
{
    /// <summary>The invariant date shape the stamp is stored in.</summary>
    private const string DateFormat = "yyyy-MM-dd";

    public WhereIsWillyLedger(DriverState? saved = null)
    {
        LastCompletedDate = Normalize(saved?.LastWillyDate);
        _completedDate = Parse(LastCompletedDate);
    }

    /// <summary>The game date of the last completed run (<c>yyyy-MM-dd</c>), or null.</summary>
    public string? LastCompletedDate { get; private set; }

    // WHY the parsed twin: the calendar page asks CompletedOn on every 10 Hz
    // poll, and formatting today's key there would allocate per tick. The date
    // compare is allocation-free; the string stays the persisted shape.
    private DateTime? _completedDate;

    /// <summary>True when a run on this game date already paid and stamped.</summary>
    public bool CompletedOn(DateTime gameNow) =>
        _completedDate is { } completed && completed.Date == gameNow.Date;

    /// <summary>
    /// Stamps the run the receipt just paid. Written before the driver file is
    /// banked, so a kill between the two can never let the same day pay twice.
    /// </summary>
    public void MarkCompleted(DateTime gameNow)
    {
        _completedDate = gameNow.Date;
        LastCompletedDate = KeyOf(gameNow);
    }

    private static string KeyOf(DateTime moment) =>
        moment.ToString(DateFormat, CultureInfo.InvariantCulture);

    // A saved stamp that is not a real date is dropped: it can never match a
    // game date anyway, and keeping garbage would hide a corrupt file.
    private static string? Normalize(string? saved)
    {
        if (string.IsNullOrWhiteSpace(saved))
        {
            return null;
        }

        string trimmed = saved.Trim();
        return Parse(trimmed) is not null ? trimmed : null;
    }

    private static DateTime? Parse(string? date) =>
        date is { Length: > 0 }
            && DateTime.TryParseExact(
                date, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed)
            ? parsed.Date
            : null;
}
