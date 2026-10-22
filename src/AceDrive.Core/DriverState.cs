// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11/43/47.
namespace AceDrive.Core;

/// <summary>What a restart must reopen: the cash wallet, the XP total, the
/// world-standings snapshot (rank/total 0 = never saved, Unity's
/// lastSavedRivals* pair), the login-streak stamps (Unity
/// PlayerData.daysPlayedInARow / lastLoginDate / lastDailyBonusDate; the
/// dates ride Unity's yyyy-MM-dd format, see DailyStreakService), the
/// operating-zone counters (Unity expansionJobsCompleted /
/// expansionCount / discoveredStreets / currentOperatingZone) and the
/// savings bank (Unity PlayerData.bankAmount + hasEnteredBank; the
/// 1000.0 default IS the Unity seed — a pre-bank save without the
/// property deserializes straight to the saldo, and a legal balance can
/// never reach 0, so Unity's map-boot 0→1000 migration is implicit). The
/// insurance stamp (PORTPLAN 34, Unity lastInsurancePremiumDay) is the last
/// game date a premium was charged, so the nightly charge fires once per date.
/// The pending insurance cash (Unity pendingInsuranceCashIn) is the escrow a
/// repair pays into and the broker collects.
/// The nullable dates default to null for the migration-free loads of old
/// saves: a fresh streak answers 1, exactly like Unity's zero counter.
/// The trunk (PORTPLAN 26/51, Unity PlayerData.trunkItems) rides here so a
/// restart reopens bought goods with the cash that paid for them.
/// The best single shift (PORTPLAN 12, Unity highestShiftEarnings +
/// highestShiftEarningsFirstEarnedAt) with its lifetime counters
/// (totalShiftsCompleted + longestShiftJobCount) and its last world rank
/// snapshot ride here too, so the online board resumes without re-pushing on
/// every launch.</summary>
public sealed record DriverState(
    double Cash,
    double Xp,
    int RivalsRank = 0,
    int RivalsTotal = 0,
    int DaysInARow = 0,
    string? LastLoginDate = null,
    string? LastDailyBonusDate = null,
    int ExpansionJobs = 0,
    int ExpansionCount = 0,
    IReadOnlyList<string>? StreetIds = null,
    double ZoneKm = 0.0,
    double Bank = 1000.0,
    bool HasEnteredBank = false,
    string? LastInsuranceChargeDate = null,
    double PendingInsurance = 0.0,
    IReadOnlyList<TrunkItem>? Trunk = null,
    double BestShiftEarnings = 0.0,
    long BestShiftFirstEarnedTicks = 0,
    int ShiftRank = 0,
    int ShiftTotal = 0,
    int ShiftsCompleted = 0,
    int LongestShiftJobs = 0)
{
    public static readonly DriverState Fresh = new(0.0, 0.0);
}
