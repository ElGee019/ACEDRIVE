// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 47, exact port
// of Unity Drive.Utils.DailyStreakService. The counter and its dates live on
// DriverState (Unity: PlayerData.daysPlayedInARow / lastLoginDate /
// lastDailyBonusDate); this class owns them between save points.
namespace AceDrive.Core;

/// <summary>
/// The real-world consecutive-days LOGIN streak (not game days). The
/// streak-day number is always recomputed from lastLoginDate against the
/// current date — never trusted from the stored counter alone — so
/// <see cref="Update"/> is idempotent per day and the counter can never go
/// stale within a long-lived session. The daily bonus is claimable once
/// per day; the onboarding gate lives with the caller (Unity gates it on
/// firstSceneComplete at the grant site, not here).
/// </summary>
public sealed class DailyStreak
{
    // WHY UtcNow and not local midnight: AGENTS.md routes all time through
    // IClock (DateTime.UtcNow). Unity used DateTime.Now, so the streak day
    // flips at UTC midnight instead of local midnight — a conscious 1-hour
    // offset in the worst case, never a wrong consecutive count.
    private const string DateFormat = "yyyy-MM-dd";
    private readonly IClock _clock;

    public DailyStreak(IClock clock, DriverState? saved)
    {
        _clock = clock;
        DaysInARow = Math.Max(0, saved?.DaysInARow ?? 0);
        _lastLogin = saved?.LastLoginDate;
        _lastBonus = saved?.LastDailyBonusDate;
    }

    public int DaysInARow { get; private set; }
    public string? LastLoginDate => _lastLogin;
    public string? LastDailyBonusDate => _lastBonus;
    private string? _lastLogin;
    private string? _lastBonus;

    /// <summary>Today's date stamp in Unity's save format.</summary>
    public string Today =>
        _clock.UtcNow.ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Advances the streak to today (no-op when already counted) and
    /// stamps lastLoginDate. Returns today's streak-day number.
    /// </summary>
    public int Update()
    {
        DaysInARow = CountStreakDay(_lastLogin, Today, DaysInARow);
        _lastLogin = Today;
        return DaysInARow;
    }

    /// <summary>True while today's login bonus has not been claimed yet
    /// (Unity's IsDailyBonusDue).</summary>
    public bool BonusDue => !string.Equals(_lastBonus, Today, StringComparison.Ordinal);

    /// <summary>Stamps today as claimed so the bonus is granted once per
    /// day (Unity's GrantDailyBonusIfDue tail).</summary>
    public void StampBonus() => _lastBonus = Today;

    /// <summary>
    /// Today's streak-day number given the stored lastLoginDate: 1 on a
    /// first-ever or broken streak, previous + 1 on a consecutive return,
    /// previous (unchanged, min 1) when already counted today.
    /// </summary>
    public static int CountStreakDay(string? lastLoginDate, string today, int storedDays)
    {
        if (string.IsNullOrEmpty(lastLoginDate)) return 1;
        if (lastLoginDate == today) return Math.Max(1, storedDays);
        string yesterday;
        try
        {
            yesterday = System.DateTime.ParseExact(today, DateFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None).AddDays(-1).ToString(DateFormat,
                System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (System.FormatException)
        {
            // WHY the guard stays anyway: the date comes from our own clock
            // formatter today, but the stamp doubles as a save-file format —
            // a malformed hand-edited stamp degrades to a fresh streak
            // instead of throwing out of the startup path.
            return 1;
        }
        return lastLoginDate == yesterday ? storedDays + 1 : 1;
    }
}
