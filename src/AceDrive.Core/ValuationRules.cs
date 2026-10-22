// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 49 and
// LATESTDRIVEBS2 ValuationEntriesService's pure helpers.
namespace AceDrive.Core;

/// <summary>
/// Weekly vehicle-valuation showcase timing: the list empties on the
/// Sunday-to-Monday midnight game tick, entries expire after 48 real hours,
/// and the permanent house entry always drives a freshly drawn car.
/// </summary>
public static class ValuationRules
{
    public const string HouseEntryName = "John and Jane Doe";
    public const string HouseEntryDocId = "house-entry";
    public const int EntryExpiryHours = 48;

    // Unity dayOfWeek: 1 = Sunday, so 2 = Monday.
    private const int MondayDayOfWeek = 2;

    /// <summary>True exactly on the Sunday-to-Monday midnight game tick.</summary>
    public static bool IsWipeDue(int dayOfWeek, int hour, int minute) =>
        dayOfWeek == MondayDayOfWeek && hour == 0 && minute == 0;

    /// <summary>
    /// True when a Monday midnight boundary is newer than the last reset —
    /// the app was closed over the tick, or the wipe never completed. The
    /// most recent Monday at or before today makes a stale record self-heal.
    /// </summary>
    public static bool IsResetOverdue(
        int lastResetGameDay, int currentGameDay, int currentDayOfWeek)
    {
        if (currentGameDay <= 0)
        {
            return false;
        }
        int daysSinceMonday = ((currentDayOfWeek - MondayDayOfWeek) % 7 + 7) % 7;
        int mostRecentMonday = currentGameDay - daysSinceMonday;
        return mostRecentMonday > 0 && lastResetGameDay < mostRecentMonday;
    }

    /// <summary>Picks the house entry's car; null when the pool is empty.</summary>
    public static string? PickRandomCarId(IReadOnlyList<string> carIds, Func<int, int> nextInt)
    {
        if (carIds is null || carIds.Count == 0 || nextInt is null)
        {
            return null;
        }
        int index = Math.Clamp(nextInt(carIds.Count), 0, carIds.Count - 1);
        return carIds[index];
    }

    /// <summary>The TTL stamp written with a new entry (entry time + 48 h).</summary>
    public static DateTime ComputeExpiryUtc(DateTime enteredUtc)
    {
        DateTime utc = enteredUtc.Kind == DateTimeKind.Utc
            ? enteredUtc
            : enteredUtc.ToUniversalTime();
        return utc.AddHours(EntryExpiryHours);
    }
}
