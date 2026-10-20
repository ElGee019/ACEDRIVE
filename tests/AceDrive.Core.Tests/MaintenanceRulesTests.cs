using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Maintenance balance table (PORTPLAN items 31/32/39, port of Unity
/// CarMaintenanceService + EngineHealthService + CarMechanicScreen +
/// StandardBreakdownStrategy): the shipped numbers, the class step and the
/// engine red-km bands. A drift from Unity shows up here.</summary>
public sealed class MaintenanceRulesTests
{
    [Fact]
    public void Default_KeepsUnitysPartTable()
    {
        MaintenanceRules rules = MaintenanceRules.Default;

        Assert.Equal(8, rules.Parts.Count);
        Assert.Equal(
            ["oil_change", "wipers", "air_filter", "brake_pads", "tires", "brake_fluid", "transmission_fluid", "car_battery"],
            rules.Parts.Select(p => p.Key));
        Assert.Equal((1200.0, 250.0), (rules.Parts[0].IntervalKm, rules.Parts[0].FullPrice));
        Assert.Equal((3600.0, 400.0), (rules.Parts[7].IntervalKm, rules.Parts[7].FullPrice));
    }

    [Fact]
    public void Default_KeepsUnitysScalarBalance()
    {
        MaintenanceRules rules = MaintenanceRules.Default;

        Assert.Equal(2.0, rules.CurveExponent);
        Assert.Equal(0.10, rules.MaxBreakdownChance);
        Assert.Equal(0.10, rules.BreakdownFractionMin);
        Assert.Equal(0.90, rules.BreakdownFractionMax);
        Assert.Equal(100.0, rules.PickupFee);
        Assert.Equal(0.10, rules.EngineRepairFraction);
        Assert.Equal(0.30, rules.NewEngineFraction);
        Assert.Equal(7, rules.WeeklyIntervalDays);
        Assert.Equal(364, rules.YearIntervalDays);
        Assert.Equal(8, rules.YearJitterDays);
    }

    [Theory]
    [InlineData(16, 1.0)]
    [InlineData(21, 1.0)]
    [InlineData(15, 0.8)]
    [InlineData(11, 0.8)]
    [InlineData(10, 0.6)]
    [InlineData(7, 0.6)]
    [InlineData(6, 0.4)]
    [InlineData(0, 0.4)]
    public void MultiplierFor_KeepsUnitysClassStep(int carClass, double expected) =>
        Assert.Equal(expected, MaintenanceRules.Default.MultiplierFor(carClass));

    [Theory]
    [InlineData(0, 250000.0)]
    [InlineData(999, 250000.0)]
    [InlineData(1000, 300000.0)]
    [InlineData(1460, 300000.0)]
    [InlineData(1999, 300000.0)]
    [InlineData(2000, 350000.0)]
    [InlineData(3000, 400000.0)]
    [InlineData(4000, 450000.0)]
    [InlineData(5000, 500000.0)]
    [InlineData(6000, 500000.0)]
    public void RedKmFor_KeepsUnitysEngineBands(int engineCc, double expected) =>
        Assert.Equal(expected, MaintenanceRules.Default.RedKmFor(engineCc));

    [Fact]
    public void MalformedClassTable_DegradesToTheCheapestStep()
    {
        // A table with no band covering class 0 must not throw mid-purchase.
        MaintenanceRules rules = MaintenanceRules.Default with
        {
            ClassBands = [new ClassPriceBand(10, 0.9)],
        };
        Assert.Equal(0.9, rules.MultiplierFor(0));
    }

    [Fact]
    public void EmptyEngineTable_ReadsZeroRedKm()
    {
        MaintenanceRules rules = MaintenanceRules.Default with { EngineBands = [] };
        Assert.Equal(0.0, rules.RedKmFor(1500));
    }
}
