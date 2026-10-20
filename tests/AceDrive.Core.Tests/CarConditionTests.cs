using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The per-part wear deck (PORTPLAN item 31, port of Unity
/// CarMaintenanceService): fractions, the average, the breakdown curve, the
/// service reset and the resume/backfill behaviour.</summary>
public sealed class CarConditionTests
{
    // A one-part table keeps the arithmetic exact: a single 1000 km interval
    // and Unity's 10% cap × 2.0 curve.
    private static readonly MaintenanceRules OnePart = MaintenanceRules.Default with
    {
        Parts = [new MaintenancePart("tires", 1000.0, 900.0)],
    };

    [Fact]
    public void FreshCar_ReadsZeroWear()
    {
        var condition = new CarCondition(OnePart, null, odometer: 0.0);
        Assert.Equal(0.0, condition.PartFraction("tires", 0.0));
        Assert.Equal(0.0, condition.AverageWear(0.0));
        Assert.Equal(0.0, condition.BreakdownChance(0.0));
    }

    [Fact]
    public void PartFraction_IsDistanceOverInterval_Clamped()
    {
        var condition = new CarCondition(OnePart, null, odometer: 0.0);
        Assert.Equal(0.5, condition.PartFraction("tires", 500.0));
        Assert.Equal(1.0, condition.PartFraction("tires", 1000.0));
        Assert.Equal(1.0, condition.PartFraction("tires", 5000.0)); // clamped past the interval
    }

    [Fact]
    public void BreakdownChance_FollowsUnitysCurve()
    {
        // Unity ComputeBreakdownChance: 0.10 × t^2. Half wear = a quarter of
        // the cap; full wear = the cap.
        var condition = new CarCondition(OnePart, null, odometer: 0.0);
        Assert.Equal(0.025, condition.BreakdownChance(500.0), 10);
        Assert.Equal(0.10, condition.BreakdownChance(1000.0), 10);
        Assert.Equal(0.10, condition.BreakdownChance(9000.0), 10);
    }

    [Fact]
    public void ServicePart_ResetsOnePartOnly()
    {
        MaintenanceRules twoParts = MaintenanceRules.Default with
        {
            Parts = [new MaintenancePart("oil_change", 1200.0, 250.0), new MaintenancePart("tires", 1000.0, 900.0)],
        };
        var condition = new CarCondition(twoParts, null, odometer: 0.0);
        condition.ServicePart("oil_change", odometer: 1000.0);

        Assert.Equal(0.0, condition.PartFraction("oil_change", 1000.0));
        Assert.Equal(1.0, condition.PartFraction("tires", 1000.0));
    }

    [Fact]
    public void UnknownPart_ReadsZeroAndServiceIsIgnored()
    {
        var condition = new CarCondition(OnePart, null, odometer: 0.0);
        Assert.Equal(0.0, condition.PartFraction("flux_capacitor", 500.0));
        condition.ServicePart("flux_capacitor", 500.0); // must not throw
        Assert.Equal(0.5, condition.AverageWear(500.0), 10);
    }

    [Fact]
    public void ResetAll_ZeroesEveryBaseline()
    {
        // Unity ReplaceEngine: a new engine zeroes the part baselines so the
        // displayed wear does not jump to 100% after the odometer reset.
        var condition = new CarCondition(OnePart, null, odometer: 0.0);
        condition.ResetAll();
        Assert.Equal(0.0, condition.PartFraction("tires", 0.0));
    }

    [Fact]
    public void Snapshot_RoundTripsTheBaseline()
    {
        var condition = new CarCondition(OnePart, null, odometer: 0.0);
        condition.ServicePart("tires", odometer: 400.0);
        var resumed = new CarCondition(OnePart, condition.Snapshot(), odometer: 400.0);
        Assert.Equal(0.0, resumed.PartFraction("tires", 400.0));
        Assert.Equal(0.5, resumed.PartFraction("tires", 900.0));
    }

    [Fact]
    public void Resume_BackfillsAMissingPartFresh()
    {
        // A part the saved row does not hold is freshly serviced at the current
        // odometer (Unity EnsureInitialized): a legacy save self-heals instead
        // of reading 100% worn.
        var resumed = new CarCondition(OnePart, saved: [], odometer: 1234.0);
        Assert.Equal(0.0, resumed.PartFraction("tires", 1234.0));
    }

    [Fact]
    public void Resume_IgnoresUnknownAndBrokenRows()
    {
        var resumed = new CarCondition(OnePart, [
            new MaintenancePartState("flux_capacitor", 0.0),   // not in the rules
            new MaintenancePartState("tires", -5.0),           // negative baseline
            new MaintenancePartState("tires", double.NaN),     // non-finite baseline
        ], odometer: 0.0);

        // Every row was rejected, so the backfill baseline stands: fully worn
        // only once the interval is consumed from 0.
        Assert.Equal(0.5, resumed.PartFraction("tires", 500.0));
    }
}
