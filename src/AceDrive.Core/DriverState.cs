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
/// every launch.
/// The driver-rating skills (PORTPLAN 06, Unity PlayerData.driverCornering /
/// driverConcentration / driverOffTheLine) default to Unity's new-game
/// overall, so a save written before the rating existed opens on a legal
/// fresh-driver profile instead of three zeros.
/// The lifetime settled-job total (Unity PlayerData.jobsCompleted) gates the
/// dealer and mechanic POIs; it is separate from the zone's expansion counter,
/// which resets on every expansion.
/// The ToDo career readings (PORTPLAN 46) ride here too: the lifetime CR
/// earned, the per-kind settled job totals, the manual destination pins and
/// the one-time business visits. Unity keeps them on PlayerData; the driver
/// file is this port's PlayerData.
/// The Gentlemen Drivers Club reputation (PORTPLAN 49, Unity
/// PlayerData.clubReputation) starts at Unity's raw 0 — the Cento Miglia page
/// prints it with Unity's 1.0 floor — and only a finished Cento Miglia moves
/// it, so the race port owns the writes while the page already reads it.
/// The Star of the Show win total (Unity PlayerData.starOfTheShowWins) rides
/// here too: the race prize scales on it (each win pays more than the last).
/// The weekly market valuation (PORTPLAN 49, Unity PlayerData.valuation*)
/// keeps its venue, in-flight drive, week's entry, settled uid set and last
/// reset week here, so a restart reopens the same showcase week. The entry's
/// real-world stamp rides along because the two-day away-earning cap is
/// measured in real hours, not game weeks.
/// The weekly missing-person run (ToWhereIsWilly.md §2.9, Unity
/// PlayerData.lastWillyGameDay + lastWillyYear) keeps only its completion
/// date here: the search itself is session state and never resumes.
/// The delivery career (PORTPLAN 18/51, Unity PlayerData.credP +
/// ownedWholesaleHubs + hubDeliveryStats) keeps its balance, bought hubs and
/// per-hub ramp counts here too, so a restart reopens the career the player
/// paid for instead of re-granting the intro gift.</summary>
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
    int LongestShiftJobs = 0,
    double Cornering = DriverRating.NewGameOverall,
    double Concentration = DriverRating.NewGameOverall,
    double OffTheLine = DriverRating.NewGameOverall,
    int JobsCompleted = 0,
    double CreditEarned = 0.0,
    int TaxiJobsCompleted = 0,
    int DeliveryJobsCompleted = 0,
    int ManuallySetDestinations = 0,
    bool VisitedCarWash = false,
    bool VisitedCarDealer = false,
    bool VisitedInsurance = false,
    double ClubReputation = 0.0,
    int StarOfTheShowWins = 0,
    string? ValuationVenueStreetId = null,
    int ValuationVenueHouseNumber = 0,
    bool ValuationDriving = false,
    int ValuationEventsCompleted = 0,
    int ValuationEntryWeek = -1,
    string? ValuationEntryCarKey = null,
    double ValuationOwnScore = 0.0,
    IReadOnlyList<string>? ValuationSeenUids = null,
    int LastValuationResetWeek = -1,
    string? LastWillyDate = null,
    DateTime? ValuationEnteredAtUtc = null,
    DeliveryCareerState? Career = null)
{
    public static readonly DriverState Fresh = new(0.0, 0.0);
}
