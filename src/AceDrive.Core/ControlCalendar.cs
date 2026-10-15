// Main control page calendar (PORTPLAN items 50/52/54, port of the Unity
// JobSelectionScreen day strip + CalendarManager week/year + GameClockManager
// day math). Pure data: weekdays are 1=Sunday..7=Saturday like the Unity
// source, events are stable keys. All sentences live in resx, resolved by
// the ViewModel, so this file needs no localization.
namespace AceDrive.Core;

public static class ControlCalendar
{
    // Stable event keys: display labels resolve through resx, never here.
    public const string CentoMiglia = "CentoMiglia";
    public const string Valuation = "Valuation";
    public const string RoadTest = "RoadTest";
    public const string Willy = "Willy";
    public const string Pancakes = "Pancakes";

    // Event days are open 8am-8pm (companion parity).
    public const int OpenFromHour = 8;
    public const int OpenUntilHour = 20;

    // Weekday numbers of the week boundary: the display week rolls when the
    // game crosses Sunday midnight into Monday (Unity parity), which reads
    // as the strip's last row handing over to its first.
    public const int Sunday = 1;
    public const int Monday = 2;

    // Strip order: Monday first, Sunday last (calendar convention the
    // overlay follows). Events stay tied to their weekday, so the Unity
    // day-to-event mapping is untouched by the display order.
    public static readonly IReadOnlyList<CalendarDay> Week =
    [
        new(2, Valuation),
        new(3, RoadTest),
        new(4, Willy),
        new(5, null),
        new(6, Pancakes),
        new(7, null),
        new(1, CentoMiglia),
    ];

    // Recognition fragments per event, normalized lowercase. Vosk's English
    // model mangles "Miglia" and "Willy" but still returns these pieces of
    // the name intact, so the day row answers to them as well as to its
    // display label. Voice-match metadata, never shown: display copy lives
    // in resx. Events the model hears cleanly get no entry.
    private static readonly Dictionary<string, string[]> EventKeywords =
        new()
        {
            // /ˈtʃɛnto ˈliːlja/ is not in Vosk's English vocabulary, so the
            // model re-splits it into English tokens whole ("chantal",
            // "gentlemen leah", "sent media"...). This list is seeded from
            // the actual "voice heard" log lines of 2026-10-13 - not from
            // guesses; grow it the same way if a new mishearing shows up.
            [CentoMiglia] =
            [
                "cento", "chantal", "chant", "gentle", "gentlemen", "sample",
                "gentle media", "sample media", "sent media", "chant amelia", "gentlemen leah",
            ],
            [Willy] = ["where is"],
        };

    /// <summary>Spoken fragments that reach an event besides its full
    /// label; empty when the label itself is heard reliably.</summary>
    public static IReadOnlyList<string> KeywordsFor(string? eventId) =>
        eventId is not null && EventKeywords.TryGetValue(eventId, out string[]? keys)
            ? keys
            : [];

    /// <summary>Maps a moment to 1=Sunday..7=Saturday.</summary>
    public static int ToDayOfWeek(DateTime moment) => (int)moment.DayOfWeek + 1;

    /// <summary>True inside the 8am-8pm event window. Day matching is the caller's job.</summary>
    public static bool IsEventWindow(int hour) => hour is >= OpenFromHour and < OpenUntilHour;
}
