using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The live promises behind the passive asks: which inputs count, the moving
/// floor that protects stops and idling, the rev/speed limits and the sticky
/// first violation (a broken promise never un-breaks).
/// </summary>
public sealed class RideConditionMonitorTests
{
    [Fact]
    public void NoLimits_NeverViolates()
    {
        var monitor = new RideConditionMonitor(RideCondition.None, RideMonitorRules.Default);

        Assert.Equal(RideViolation.None, monitor.Observe(200, 9000, 1, 1, 1));
    }

    [Fact]
    public void Brakes_CountOnlyAboveTheMovingFloor()
    {
        var monitor = new RideConditionMonitor(
            new RideCondition(BrakeBrusque: true), RideMonitorRules.Default);

        // Rolling to a stop: the same pedal is not a hard brake at a crawl.
        Assert.Equal(RideViolation.None, monitor.Observe(10, 3000, 0.1, 0.95, 0));
        Assert.Equal(RideViolation.Brake, monitor.Observe(60, 3000, 0.1, 0.95, 0));
    }

    [Fact]
    public void Throttle_CountsFromStandstill()
    {
        var monitor = new RideConditionMonitor(
            new RideCondition(ThrottleBrusque: true), RideMonitorRules.Default);

        // A full-throttle launch at the door is the brusque input the ask
        // complains about, so this one is not gated by the moving floor.
        Assert.Equal(RideViolation.Throttle, monitor.Observe(0, 2000, 0.9, 0, 0));
    }

    [Fact]
    public void Steering_CountsOnlyAboveTheMovingFloor()
    {
        var monitor = new RideConditionMonitor(
            new RideCondition(SteeringBrusque: true), RideMonitorRules.Default);

        Assert.Equal(RideViolation.None, monitor.Observe(10, 3000, 0.2, 0, 0.5));
        Assert.Equal(RideViolation.Steering, monitor.Observe(60, 3000, 0.2, 0, 0.5));
    }

    [Fact]
    public void MinRevs_IgnoreTheIdleButNotACruise()
    {
        var monitor = new RideConditionMonitor(
            new RideCondition(MinRpm: 2000), RideMonitorRules.Default);

        // Standing at the door or a light idles below the ask; moving does not.
        Assert.Equal(RideViolation.None, monitor.Observe(5, 900, 0.1, 0, 0));
        Assert.Equal(RideViolation.RevsTooLow, monitor.Observe(60, 1500, 0.2, 0, 0));
    }

    [Fact]
    public void MaxRevs_CountAtStandstill()
    {
        var monitor = new RideConditionMonitor(
            new RideCondition(MaxRpm: 3000), RideMonitorRules.Default);

        Assert.Equal(RideViolation.RevsTooHigh, monitor.Observe(0, 3500, 0.1, 0, 0));
    }

    [Fact]
    public void Speed_LimitReadsWithItsTolerance()
    {
        var monitor = new RideConditionMonitor(
            new RideCondition(MaxSpeedKmh: 90), RideMonitorRules.Default);

        // 90.5 is inside the 1 km/h tolerance; 91.5 is not.
        Assert.Equal(RideViolation.None, monitor.Observe(90.5, 3000, 0.2, 0, 0));
        Assert.Equal(RideViolation.Speed, monitor.Observe(91.5, 3000, 0.2, 0, 0));
    }

    [Fact]
    public void Violation_IsSticky()
    {
        var monitor = new RideConditionMonitor(
            new RideCondition(BrakeBrusque: true), RideMonitorRules.Default);
        Assert.Equal(RideViolation.Brake, monitor.Observe(60, 3000, 0.1, 0.9, 0));

        // A clean sample afterwards does not restore the promise.
        Assert.Equal(RideViolation.Brake, monitor.Observe(30, 3000, 0.1, 0, 0));
        Assert.Equal(RideViolation.Brake, monitor.Violation);
    }

    [Fact]
    public void CustomRules_MoveTheThresholds()
    {
        var rules = RideMonitorRules.Default with { BrakeBrusque = 0.4 };
        var monitor = new RideConditionMonitor(
            new RideCondition(BrakeBrusque: true), rules);

        Assert.Equal(RideViolation.Brake, monitor.Observe(60, 3000, 0.1, 0.5, 0));
    }
}
