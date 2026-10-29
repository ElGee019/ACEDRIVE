using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The brake leg's distance integration: the trapezoid between two 10 Hz speed
/// readings, guarded against the stalled or torn tick that would otherwise bank
/// distance the car never covered.
/// </summary>
public sealed class RoadTestBrakeTests
{
    [Fact]
    public void AddDistance_UsesTheTrapezoidBetweenTheTwoReadings()
    {
        // 100 km/h held for 0.1 s is 2.777... m.
        Assert.InRange(
            RoadTestBrake.AddDistance(0.0, 100.0, 100.0, 0.1), 2.777, 2.778);
    }

    [Fact]
    public void AddDistance_AveragesTheBrakingPair()
    {
        // 100 down to 50 over the tick averages 75 km/h: 2.083... m.
        Assert.InRange(
            RoadTestBrake.AddDistance(0.0, 100.0, 50.0, 0.1), 2.083, 2.084);
    }

    [Fact]
    public void AddDistance_AddsToTheRun()
    {
        Assert.InRange(
            RoadTestBrake.AddDistance(10.0, 100.0, 100.0, 0.1), 12.777, 12.778);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddDistance_DropsAStalledTick(double seconds)
    {
        // A tab-out or a lagged frame is not driving: the run must not grow by
        // the distance the car would have covered while the poll was away.
        Assert.Equal(10.0, RoadTestBrake.AddDistance(10.0, 100.0, 50.0, seconds));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AddDistance_DropsATornSpeedReading(double speedKmh)
    {
        Assert.Equal(10.0, RoadTestBrake.AddDistance(10.0, 100.0, speedKmh, 0.1));
        Assert.Equal(10.0, RoadTestBrake.AddDistance(10.0, speedKmh, 50.0, 0.1));
    }

    [Fact]
    public void AddDistance_KeepsTheRunWhenTheSumIsNotFinite()
    {
        // A torn reading at an absurd speed must not turn the run into infinity.
        Assert.Equal(
            double.MaxValue,
            RoadTestBrake.AddDistance(double.MaxValue, double.MaxValue, double.MaxValue, 0.1));
    }
}
