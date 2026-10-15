namespace AceDrive.Overlay.ViewModels;

/// <summary>One calendar row, preformatted: the view stays dumb.</summary>
public sealed record CalendarRow(
    string DayLabel, string EventLabel, int DayOfWeek, bool IsToday, bool IsOpen, bool Flash = false);
