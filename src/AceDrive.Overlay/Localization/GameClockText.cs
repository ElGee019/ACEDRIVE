using System.Globalization;

namespace AceDrive.Overlay.Localization;

/// <summary>
/// The design's clock copy, shared by the menu and the calendar page: 24-hour
/// game time over the abbreviated weekday and display week. The two pages
/// render the same header, so the format and day keys live in one place.
/// </summary>
internal static class GameClockText
{
    // Unity day numbering, 1 = Sunday, matching ControlCalendar. The short
    // resx forms are the design's header copy, not a truncation in code.
    private static readonly string[] DayKeys =
    [
        "DayShortSun", "DayShortMon", "DayShortTue", "DayShortWed",
        "DayShortThu", "DayShortFri", "DayShortSat",
    ];

    /// <summary>Design clock line; the locale never reshapes "21:45".</summary>
    public static string Time(DateTime now) =>
        now.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Weekday + week line under the clock ("THU / WEEK 1").</summary>
    public static string DayWeek(int today, int week) =>
        Strings.Format("ClockDate", Strings.Get(DayKeys[today - 1]), week);
}
