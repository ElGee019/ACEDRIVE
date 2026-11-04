using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The crew's delivery work, ported from LATESTDRIVEBS2
/// EmployeeService.AssignDelivery: one store that needs stock is picked, the
/// run lands exactly 24 hours after the click, and every store being full means
/// no run at all.
/// </summary>
public sealed class EmployeeDeliveriesTests
{
    private static readonly DateTime GameStart = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);

    private static (EmployeeDeliveries Deliveries, EmployeeAssignments Assignments, GameClock Game)
        Create(IReadOnlyList<DeliveryStore> stores, int seed = 0)
    {
        var clock = new FakeClock();
        var game = new GameClock(clock, GameStart);
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        return (
            new EmployeeDeliveries(assignments, game, () => stores, new Random(seed)),
            assignments,
            game);
    }

    [Fact]
    public void Assign_ArrivesExactlyTwentyFourHoursAfterTheClick()
    {
        var (deliveries, assignments, game) = Create([Store("store_a", stocked: 5)]);

        Assert.True(deliveries.Assign("Anna Berg"));

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        Assert.Equal(EmployeeJobKind.DeliveryJobs, job.Kind);
        Assert.Equal(game.GameMinute + (24 * 60), job.DueGameMinute);
        Assert.Equal("store_a", job.StoreKey);
        // The payout is one CR per unit the refill adds, so it is settled on
        // completion instead of being guessed at the click.
        Assert.Equal(0.0, job.Cr);
    }

    [Fact]
    public void Assign_PrefersAStoreAtMostEightyPercentFull()
    {
        var (deliveries, assignments, _) = Create(
            [Store("fuller", stocked: 18), Store("needy", stocked: 5)]);

        Assert.True(deliveries.Assign("Anna Berg"));

        Assert.Equal("needy", assignments.AssignmentOf("Anna Berg")?.StoreKey);
    }

    [Fact]
    public void Assign_WhenEveryStoreIsFuller_FallsBackToAnyStoreWithRoom()
    {
        var (deliveries, assignments, _) = Create(
            [Store("high", stocked: 18), Store("at_cap", stocked: 20)]);

        Assert.True(deliveries.Assign("Anna Berg"));

        Assert.Equal("high", assignments.AssignmentOf("Anna Berg")?.StoreKey);
    }

    [Fact]
    public void Assign_WhenEveryStoreIsFull_AssignsNothing()
    {
        var (deliveries, assignments, _) = Create(
            [Store("at_cap", stocked: 20), Store("also_full", stocked: 20)]);

        Assert.False(deliveries.Assign("Anna Berg"));

        Assert.False(assignments.IsOnJob("Anna Berg"));
    }

    [Fact]
    public void Assign_ReadsTheLiveShelves_NotTheStoreRow()
    {
        // The row the player sees can be stale; the live shelf is full, so the
        // run must not be booked against a store with nothing to refill.
        DeliveryStore live = Store("needy", stocked: 20);
        var clock = new FakeClock();
        var game = new GameClock(clock, GameStart);
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        var deliveries = new EmployeeDeliveries(
            assignments, game, () => [live], new Random(0));

        Assert.False(deliveries.Assign("Anna Berg"));
        Assert.False(assignments.IsOnJob("Anna Berg"));
    }

    private static DeliveryStore Store(string key, int stocked) =>
        new(key, key, "Owner", stocked, UnitCapacity: 20, PricePerUnit: 1.0);
}
