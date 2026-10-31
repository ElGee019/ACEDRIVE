using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The accident repair economy (Unity RepairCostCalculator's
/// successor, but scaled by impact speed instead of trip distance): a top
/// speed impact costs a tenth of the car, half the top speed half of that, the
/// fraction clamps at both ends, and the bill never passes the car's
/// price.</summary>
public sealed class AccidentRulesTests
{
    [Fact]
    public void TopSpeedImpact_CostsATenthOfTheCar()
    {
        double fraction = AccidentRules.RepairFraction(240.0, 240.0);

        Assert.Equal(0.1, fraction, 6);
        Assert.Equal(4000.0, AccidentRules.RepairCost(40000.0, fraction), 6);
    }

    [Fact]
    public void HalfTopSpeed_CostsHalfOfThat()
    {
        double fraction = AccidentRules.RepairFraction(120.0, 240.0);

        Assert.Equal(0.05, fraction, 6);
        Assert.Equal(2000.0, AccidentRules.RepairCost(40000.0, fraction), 6);
    }

    [Fact]
    public void TheScaleIsLinear()
    {
        Assert.Equal(0.025, AccidentRules.RepairFraction(60.0, 240.0), 6);
        Assert.Equal(0.075, AccidentRules.RepairFraction(180.0, 240.0), 6);
    }

    [Fact]
    public void ImpactAboveTheRatedTopSpeed_ClampsToTheSameTenth()
    {
        double fraction = AccidentRules.RepairFraction(300.0, 240.0);

        Assert.Equal(0.1, fraction, 6);
        Assert.Equal(4000.0, AccidentRules.RepairCost(40000.0, fraction), 6);
    }

    [Fact]
    public void AParkedCar_CostsNothing()
    {
        double fraction = AccidentRules.RepairFraction(0.0, 240.0);

        Assert.Equal(0.0, fraction, 6);
        Assert.Equal(0.0, AccidentRules.RepairCost(40000.0, fraction), 6);
    }

    [Fact]
    public void AnUnknownTopSpeed_ReadsZero_InsteadOfInventingADenominator()
    {
        double fraction = AccidentRules.RepairFraction(120.0, -1.0);

        Assert.Equal(0.0, fraction, 6);
        Assert.Equal(0.0, AccidentRules.RepairCost(40000.0, fraction), 6);
    }

    [Fact]
    public void NonFiniteOrUnpricedCars_CostNothing()
    {
        Assert.Equal(0.0, AccidentRules.RepairFraction(double.NaN, 240.0), 6);
        Assert.Equal(0.0, AccidentRules.RepairFraction(120.0, double.PositiveInfinity), 6);
        Assert.Equal(0.0, AccidentRules.RepairCost(0.0, 1.0), 6);
        Assert.Equal(0.0, AccidentRules.RepairCost(double.NaN, 1.0), 6);
        Assert.Equal(0.0, AccidentRules.RepairCost(40000.0, double.NaN), 6);
    }

    [Fact]
    public void AnOverCapShare_StillCostsOnlyTheCar()
    {
        // Last line of defence: even a caller that hands on a share bigger than
        // the car (a write-off plus interest) cannot bill more than its price.
        Assert.Equal(40000.0, AccidentRules.RepairCost(40000.0, 2.5), 6);
        Assert.Equal(0.0, AccidentRules.RepairCost(40000.0, -0.5), 6);
    }
}
