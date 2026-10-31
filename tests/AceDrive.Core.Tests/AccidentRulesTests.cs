using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The accident repair economy (Unity RepairCostCalculator's
/// successor, but scaled by impact speed instead of trip distance): a top
/// speed impact writes off the whole car, a half-top-speed impact half of it,
/// and the fraction clamps at both ends.</summary>
public sealed class AccidentRulesTests
{
    [Fact]
    public void TopSpeedImpact_IsAWriteOff()
    {
        Assert.Equal(1.0, AccidentRules.RepairFraction(240.0, 240.0), 6);
        Assert.Equal(40000.0, AccidentRules.RepairCost(40000.0, 240.0, 240.0), 6);
    }

    [Fact]
    public void HalfTopSpeed_IsHalfTheValue()
    {
        Assert.Equal(0.5, AccidentRules.RepairFraction(120.0, 240.0), 6);
        Assert.Equal(20000.0, AccidentRules.RepairCost(40000.0, 120.0, 240.0), 6);
    }

    [Fact]
    public void TheScaleIsLinear()
    {
        Assert.Equal(0.25, AccidentRules.RepairFraction(60.0, 240.0), 6);
        Assert.Equal(0.75, AccidentRules.RepairFraction(180.0, 240.0), 6);
    }

    [Fact]
    public void ImpactAboveTheRatedTopSpeed_StillClampsToAWriteOff()
    {
        Assert.Equal(1.0, AccidentRules.RepairFraction(300.0, 240.0), 6);
        Assert.Equal(40000.0, AccidentRules.RepairCost(40000.0, 300.0, 240.0), 6);
    }

    [Fact]
    public void AParkedCar_CostsNothing()
    {
        Assert.Equal(0.0, AccidentRules.RepairFraction(0.0, 240.0), 6);
        Assert.Equal(0.0, AccidentRules.RepairCost(40000.0, 0.0, 240.0), 6);
    }

    [Fact]
    public void AnUnknownTopSpeed_ReadsZero_InsteadOfInventingADenominator()
    {
        Assert.Equal(0.0, AccidentRules.RepairFraction(120.0, 0.0), 6);
        Assert.Equal(0.0, AccidentRules.RepairCost(40000.0, 120.0, -1.0), 6);
    }

    [Fact]
    public void NonFiniteOrUnpricedCars_CostNothing()
    {
        Assert.Equal(0.0, AccidentRules.RepairFraction(double.NaN, 240.0), 6);
        Assert.Equal(0.0, AccidentRules.RepairFraction(120.0, double.PositiveInfinity), 6);
        Assert.Equal(0.0, AccidentRules.RepairCost(0.0, 120.0, 240.0), 6);
        Assert.Equal(0.0, AccidentRules.RepairCost(double.NaN, 120.0, 240.0), 6);
    }
}
