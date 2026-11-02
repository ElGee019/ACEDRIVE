namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One calendar row, preformatted: the view stays dumb. DayLabel is the full
/// day name for the gate warning's sentence; ShortLabel is the three-letter
/// form the strip shows (same copy as the clock header). Completed drives the
/// grey italic state the weekly missing-person row wears once its run is
/// stamped. Taken is the event's own used-up slot (the weekly stamp, the
/// missing-person completion, the valuation entry): the row says to come back
/// next week and its tap only acknowledges. Locked is behaviour only, never
/// styling: the row looks like every other one and its tap answers with the
/// coffee-career hint.
/// </summary>
public sealed record CalendarRow(
    string DayLabel, string ShortLabel, string EventLabel,
    int DayOfWeek, bool IsToday, bool IsOpen, bool Flash = false,
    bool Completed = false, bool Locked = false, bool Taken = false);
