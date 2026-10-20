// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN item 32, port of Unity
// Utils/EngineHealthService.cs — an engine ages with the odometer since its
// last reset; the broken percentage follows a quadratic curve that reaches
// 100% exactly at a cc-dependent red threshold. Weekly inspections are
// informational, the annual one can strand the car.
//
// Unity schedules on an absolute (gameDay, minuteOfDay) timeline because its
// clock is raw counters; ACEDRIVE's GameClock already exposes a real
// DateTime, so the schedule is stored as DateTime moments and compared
// directly. The observable cadence (7 days / 364-371 days) is identical.
namespace AceDrive.Core;

/// <summary>
/// One car's engine state (Unity OwnedCar.engineBroken / engineBaseOdometer /
/// engineNextCheck*): the odometer the wear counter resets from, the broken
/// flag, and the next weekly/annual inspection moments. Randomness (the
/// inspection minute and the annual roll) is a seeded instance — never the
/// shared global (AGENTS.md) — so the schedules are deterministic in tests.
/// </summary>
public sealed class EngineHealth
{
    private const int MinutesPerDay = 1440;

    private readonly MaintenanceRules _rules;
    private readonly Random _rng;

    public EngineHealth(MaintenanceRules rules, EngineConditionState? saved, double odometer, int seed)
    {
        _rules = rules;
        _rng = new Random(seed);
        // A fresh car (or a legacy row without engine state) counts the whole
        // standing odometer as engine usage (Unity: the starter car's base is
        // 0). Unity's backfill keeps a used car's odometer as its base instead;
        // ACEDRIVE cannot tell a starter from the catalogue here, so a
        // pre-engine save resumes with a fresh engine — the split only shows on
        // saves written before this slice.
        double fallbackBase = Math.Max(0.0, odometer);
        double baseOdometer = saved?.BaseOdometer ?? fallbackBase;
        BaseOdometer = double.IsFinite(baseOdometer) && baseOdometer >= 0.0 ? baseOdometer : fallbackBase;
        Broken = saved?.Broken ?? false;
        WeeklyNext = saved?.WeeklyNext;
        AnnualNext = saved?.AnnualNext;
    }

    /// <summary>Odometer the wear counter counts from (Unity engineBaseOdometer).</summary>
    public double BaseOdometer { get; private set; }

    /// <summary>Whether the engine failed and the car is stranded until the
    /// mechanic repairs or replaces it (Unity engineBroken).</summary>
    public bool Broken { get; private set; }

    /// <summary>Next weekly inspection moment, null until first scheduled.</summary>
    public DateTime? WeeklyNext { get; private set; }

    /// <summary>Next annual inspection moment, null until first scheduled.</summary>
    public DateTime? AnnualNext { get; private set; }

    /// <summary>Whether both inspection moments have been set (Unity's
    /// "initialized" marker: a new engine always reschedules to the future).</summary>
    public bool IsScheduled => WeeklyNext is not null && AnnualNext is not null;

    /// <summary>Unity IsWeeklyCheckDue: the inspection moment has passed.</summary>
    public bool IsWeeklyDue(DateTime gameNow) => WeeklyNext is { } due && gameNow >= due;

    /// <summary>Unity IsYearCheckDue: the annual inspection moment has passed.</summary>
    public bool IsAnnualDue(DateTime gameNow) => AnnualNext is { } due && gameNow >= due;

    /// <summary>Unity ComputeBrokenPercent: quadratic, 100% exactly at redKm.</summary>
    public double BrokenPercent(int engineCc, double odometer)
    {
        double redKm = _rules.RedKmFor(engineCc);
        if (redKm <= 0.0)
        {
            return 0.0;
        }
        double since = Math.Max(0.0, odometer - BaseOdometer);
        double t = since / redKm;
        return Math.Clamp(t * t, 0.0, 1.0);
    }

    /// <summary>Unity EnsureInitialized: sets the schedule once, for a car that
    /// has never been inspected. Called the first time the car is driven.</summary>
    public void EnsureInitialized(DateTime gameNow)
    {
        if (!IsScheduled)
        {
            ScheduleChecks(gameNow);
        }
    }

    /// <summary>Unity ScheduleChecks: fresh schedule after a repair, a new
    /// engine, or the initial backfill.</summary>
    public void ScheduleChecks(DateTime gameNow)
    {
        WeeklyNext = NextMoment(gameNow, _rules.WeeklyIntervalDays, jitterDays: 0);
        AnnualNext = NextMoment(gameNow, _rules.YearIntervalDays, jitterDays: _rules.YearJitterDays);
    }

    /// <summary>Unity CompleteWeeklyCheck: the schedule rolls forward exactly
    /// one week, guaranteeing at most one weekly inspection per week.</summary>
    public void CompleteWeekly(DateTime gameNow) =>
        WeeklyNext = NextMoment(gameNow, _rules.WeeklyIntervalDays, jitterDays: 0);

    /// <summary>Unity RunYearCheck: rolls random &lt; brokenPercent; on failure
    /// the engine is broken (stranded). Always rolls the schedule forward a
    /// year, so a failed engine does not re-roll on every drive.</summary>
    public bool RunAnnual(int engineCc, double odometer, DateTime gameNow)
    {
        double percent = BrokenPercent(engineCc, odometer);
        bool failed = _rng.NextDouble() < percent;
        if (failed)
        {
            Broken = true;
        }
        AnnualNext = NextMoment(gameNow, _rules.YearIntervalDays, jitterDays: _rules.YearJitterDays);
        return failed;
    }

    /// <summary>Unity RepairEngine: clears the broken state and resets the wear
    /// counter to the current odometer (the engine is fresh again, km kept).</summary>
    public void Repair(double odometer, DateTime gameNow)
    {
        Broken = false;
        BaseOdometer = Math.Max(0.0, odometer);
        ScheduleChecks(gameNow);
    }

    /// <summary>Unity ReplaceEngine: clears the broken state and resets the
    /// counter to 0. The caller zeroes the car's odometer and part baselines
    /// (Unity's replace resets both).</summary>
    public void ReplaceEngine(DateTime gameNow)
    {
        Broken = false;
        BaseOdometer = 0.0;
        ScheduleChecks(gameNow);
    }

    /// <summary>What a restart reopens (Unity EngineHealthService fields).</summary>
    public EngineConditionState Snapshot() => new(BaseOdometer, Broken, WeeklyNext, AnnualNext);

    private DateTime NextMoment(DateTime gameNow, int days, int jitterDays)
    {
        int extra = jitterDays > 0 ? _rng.Next(0, jitterDays) : 0;
        int minute = _rng.Next(0, MinutesPerDay);
        // Day-granular like Unity: midnight of the current game day plus the
        // interval plus a random minute-of-day.
        return gameNow.Date.AddDays(days + extra).AddMinutes(minute);
    }
}
