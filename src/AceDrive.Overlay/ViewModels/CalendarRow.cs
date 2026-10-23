namespace AceDrive.Overlay.ViewModels;

/// <summary>One calendar row, preformatted: the view stays dumb. DayLabel is
/// the full day name for the gate warning's sentence; ShortLabel is the
/// three-letter form the strip shows (same copy as the clock header).</summary>
public sealed record CalendarRow(
    string DayLabel, string ShortLabel, string EventLabel,
    int DayOfWeek, bool IsToday, bool IsOpen, bool Flash = false);
