// Main control page calendar data (PORTPLAN items 50/52/54, port of the
// Unity JobSelectionScreen day strip + CalendarManager). Pure data.
namespace AceDrive.Core;

/// <summary>One calendar row: the weekday plus its event key, if any.</summary>
/// <param name="OpensFromWeek">First display week the event's button appears
/// in: 1 for every event the career opens with, 2 for the Saturday run, which
/// waits for the career's first Saturday to be over. The rule lives on the row
/// so the strip and the dispatcher read one number.</param>
public sealed record CalendarDay(int DayOfWeek, string? EventId, int OpensFromWeek = 1);
