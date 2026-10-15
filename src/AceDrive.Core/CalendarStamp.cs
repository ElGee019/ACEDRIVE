// Main control page calendar data (PORTPLAN items 50/52/54, port of the
// Unity CalendarManager week/year state). Pure data.
namespace AceDrive.Core;

/// <summary>
/// Calendar position. Default mirrors the Unity fresh save; the test-shell
/// fallback until the persistent save arrives (PORTPLAN item 51).
/// </summary>
public sealed record CalendarStamp(int Year, int Week)
{
    public static readonly CalendarStamp Default = new(1924, 1);
}
