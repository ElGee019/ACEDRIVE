// Main control page calendar data (PORTPLAN items 50/52/54, port of the
// Unity JobSelectionScreen day strip + CalendarManager). Pure data.
namespace AceDrive.Core;

/// <summary>One calendar row: the weekday plus its event key, if any.</summary>
public sealed record CalendarDay(int DayOfWeek, string? EventId);
