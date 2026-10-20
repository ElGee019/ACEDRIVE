using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Engine health (PORTPLAN item 32, port of Unity
/// EngineHealthService): the quadratic broken ramps to the cc-banded red km,
/// the weekly/annual schedule, the annual roll and repair/replace.</summary>
public sealed class EngineHealthTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 34, 0, DateTimeKind.Utc);

    // A 100 km red line keeps the arithmetic exact for the roll tests.
    private static readonly MaintenanceRules ShortRed = MaintenanceRules.Default with
    {
        EngineBands = [new EngineBand(int.MaxValue, 100.0)],
    };

    [Fact]
    public void BrokenPercent_IsQuadraticAndCapsAtTheRedLine()
    {
        var engine = new EngineHealth(ShortRed, null, odometer: 0.0, seed: 0);
        Assert.Equal(0.25, engine.BrokenPercent(1500, 50.0), 10);
        Assert.Equal(1.0, engine.BrokenPercent(1500, 100.0), 10);
        Assert.Equal(1.0, engine.BrokenPercent(1500, 250.0), 10); // clamped past the red line
    }

    [Fact]
    public void BrokenPercent_UsesTheEngineCcBand()
    {
        // Unity: 1460 cc -> the 2000 band -> 300k; 6000 cc -> the top band -> 500k.
        var engine = new EngineHealth(MaintenanceRules.Default, null, odometer: 0.0, seed: 0);
        Assert.Equal(1.0, engine.BrokenPercent(1460, 300000.0), 10);
        Assert.Equal(0.36, engine.BrokenPercent(6000, 300000.0), 10);
    }

    [Fact]
    public void Schedule_IsSevenDaysAndAToYearJitter()
    {
        var engine = new EngineHealth(MaintenanceRules.Default, null, odometer: 0.0, seed: 0);
        Assert.False(engine.IsScheduled);

        engine.EnsureInitialized(Now);

        Assert.True(engine.IsScheduled);
        Assert.Equal(Now.Date.AddDays(7), engine.WeeklyNext!.Value.Date);
        Assert.InRange(engine.WeeklyNext.Value.Minute, 0, 59);
        Assert.InRange(engine.AnnualNext!.Value.Date, Now.Date.AddDays(364), Now.Date.AddDays(371));
    }

    [Fact]
    public void EnsureInitialized_DoesNotReschedule()
    {
        var engine = new EngineHealth(MaintenanceRules.Default, null, odometer: 0.0, seed: 0);
        engine.EnsureInitialized(Now);
        DateTime first = engine.WeeklyNext!.Value;
        engine.EnsureInitialized(Now.AddDays(3));
        Assert.Equal(first, engine.WeeklyNext!.Value);
    }

    [Fact]
    public void IsWeeklyDue_FlipsAtTheMoment()
    {
        var engine = new EngineHealth(MaintenanceRules.Default, null, odometer: 0.0, seed: 0);
        engine.EnsureInitialized(Now);
        DateTime due = engine.WeeklyNext!.Value;
        Assert.False(engine.IsWeeklyDue(due.AddMinutes(-1)));
        Assert.True(engine.IsWeeklyDue(due));
        Assert.True(engine.IsWeeklyDue(due.AddDays(9)));
    }

    [Fact]
    public void CompleteWeekly_RollsForwardOneWeek()
    {
        var engine = new EngineHealth(MaintenanceRules.Default, null, odometer: 0.0, seed: 0);
        engine.EnsureInitialized(Now);
        engine.CompleteWeekly(Now);
        Assert.Equal(Now.Date.AddDays(7), engine.WeeklyNext!.Value.Date);
    }

    [Fact]
    public void RunAnnual_FailsAtFullWear_AndStrands()
    {
        var engine = new EngineHealth(ShortRed, new EngineConditionState(0.0), odometer: 100.0, seed: 0);

        bool failed = engine.RunAnnual(engineCc: 1500, odometer: 100.0, Now);

        Assert.True(failed);
        Assert.True(engine.Broken);
    }

    [Fact]
    public void RunAnnual_PassesOnAFreshEngine()
    {
        var engine = new EngineHealth(ShortRed, new EngineConditionState(0.0), odometer: 0.0, seed: 0);

        bool failed = engine.RunAnnual(engineCc: 1500, odometer: 0.0, Now);

        Assert.False(failed);
        Assert.False(engine.Broken);
    }

    [Fact]
    public void Repair_KeepsTheOdometer_AndResetsTheWearCounter()
    {
        var engine = new EngineHealth(ShortRed, new EngineConditionState(0.0), odometer: 100.0, seed: 0);
        engine.RunAnnual(engineCc: 1500, odometer: 100.0, Now);
        Assert.True(engine.Broken);

        engine.Repair(odometer: 100.0, Now);

        Assert.False(engine.Broken);
        Assert.Equal(100.0, engine.BaseOdometer);
        Assert.Equal(0.0, engine.BrokenPercent(1500, 100.0), 10);
    }

    [Fact]
    public void ReplaceEngine_ResetsTheWearCounterToZero()
    {
        var engine = new EngineHealth(ShortRed, new EngineConditionState(0.0), odometer: 100.0, seed: 0);
        engine.RunAnnual(engineCc: 1500, odometer: 100.0, Now);

        engine.ReplaceEngine(Now);

        Assert.False(engine.Broken);
        Assert.Equal(0.0, engine.BaseOdometer);
    }

    [Fact]
    public void Snapshot_RoundTripsBrokenAndSchedule()
    {
        var engine = new EngineHealth(ShortRed, new EngineConditionState(0.0), odometer: 100.0, seed: 0);
        engine.RunAnnual(engineCc: 1500, odometer: 100.0, Now);

        var resumed = new EngineHealth(ShortRed, engine.Snapshot(), odometer: 100.0, seed: 0);

        Assert.True(resumed.Broken);
        Assert.Equal(engine.BaseOdometer, resumed.BaseOdometer);
        Assert.Equal(engine.WeeklyNext, resumed.WeeklyNext);
        Assert.Equal(engine.AnnualNext, resumed.AnnualNext);
    }

    [Fact]
    public void LegacyRowWithoutEngine_StartsFreshAtTheOdometer()
    {
        // A garage.json written before this slice carries no engine block: the
        // whole standing odometer counts as usage, so a fresh base reads 0%.
        var engine = new EngineHealth(ShortRed, null, odometer: 4321.0, seed: 0);
        Assert.Equal(4321.0, engine.BaseOdometer);
        Assert.Equal(0.0, engine.BrokenPercent(1500, 4321.0), 10);
    }
}
