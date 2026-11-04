using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The fill detector (PORTPLAN item 37): EVO's pump moves the level, so
/// a run of rises settles into one fill once the tank stands still — and a
/// session change never bills the jump.</summary>
public sealed class FuelFillTrackerTests
{
    private static readonly DateTime Start = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static FuelFillTracker Tracker(FuelRules? rules = null) =>
        new(rules ?? FuelRules.Default);

    [Fact]
    public void ARisingLevel_SettlesIntoOneFill_AfterTheStillWindow()
    {
        FuelFillTracker tracker = Tracker();

        Assert.Null(tracker.Observe(20.0, Start, sessionReset: false));       // primes
        Assert.Null(tracker.Observe(25.0, Start.AddSeconds(1), false));
        Assert.Null(tracker.Observe(30.0, Start.AddSeconds(2), false));
        FuelFill? fill = tracker.Observe(30.0, Start.AddSeconds(4), false);    // still 2 s

        Assert.NotNull(fill);
        Assert.Equal(10.0, fill!.Litres, 10);
    }

    [Fact]
    public void ManySmallPumpSteps_AddUpToOneFill()
    {
        FuelFillTracker tracker = Tracker();
        tracker.Observe(20.0, Start, false);

        for (int i = 1; i <= 10; i++)
        {
            Assert.Null(tracker.Observe(20.0 + (i * 0.7), Start.AddSeconds(i), false));
        }

        FuelFill? fill = tracker.Observe(27.0, Start.AddSeconds(12), false);
        Assert.NotNull(fill);
        Assert.Equal(7.0, fill!.Litres, 10);
    }

    [Fact]
    public void DrivingOffRightAfterAPump_SettlesInsteadOfLosingTheFill()
    {
        FuelFillTracker tracker = Tracker();
        tracker.Observe(20.0, Start, false);
        tracker.Observe(26.0, Start.AddSeconds(1), false);

        FuelFill? fill = tracker.Observe(25.9, Start.AddSeconds(1.2), false);

        Assert.NotNull(fill);
        Assert.Equal(6.0, fill!.Litres, 10);
    }

    [Fact]
    public void AQuarterLitreTrickle_IsNotAVisitToThePump()
    {
        FuelFillTracker tracker = Tracker();
        tracker.Observe(20.0, Start, false);
        tracker.Observe(20.3, Start.AddSeconds(1), false);

        Assert.Null(tracker.Observe(20.3, Start.AddSeconds(5), false));
    }

    [Fact]
    public void Burn_OnlyMovesTheBaseline()
    {
        FuelFillTracker tracker = Tracker();
        tracker.Observe(20.0, Start, false);

        Assert.Null(tracker.Observe(18.0, Start.AddSeconds(10), false));
        Assert.Null(tracker.Observe(17.0, Start.AddSeconds(20), false));

        // A fill after the burn still measures from the freshest level.
        tracker.Observe(20.0, Start.AddSeconds(30), false);
        FuelFill? fill = tracker.Observe(20.0, Start.AddSeconds(35), false);
        Assert.NotNull(fill);
        Assert.Equal(3.0, fill!.Litres, 10);
    }

    [Fact]
    public void ASessionChange_ReprimesInsteadOfBillingTheJump()
    {
        FuelFillTracker tracker = Tracker();
        tracker.Observe(20.0, Start, false);

        // A reload spawns with a full tank: not a purchase.
        Assert.Null(tracker.Observe(60.0, Start.AddSeconds(1), sessionReset: true));
        Assert.Null(tracker.Observe(65.0, Start.AddSeconds(2), false));

        FuelFill? fill = tracker.Observe(65.0, Start.AddSeconds(4), false);
        Assert.NotNull(fill);
        Assert.Equal(5.0, fill!.Litres, 10);
    }

    [Fact]
    public void ATornReading_KeepsTheBaseline()
    {
        FuelFillTracker tracker = Tracker();
        tracker.Observe(20.0, Start, false);

        Assert.Null(tracker.Observe(double.NaN, Start.AddSeconds(1), false));
        Assert.Null(tracker.Observe(-4.0, Start.AddSeconds(2), false));
        tracker.Observe(25.0, Start.AddSeconds(3), false);

        FuelFill? fill = tracker.Observe(25.0, Start.AddSeconds(5), false);
        Assert.NotNull(fill);
        Assert.Equal(5.0, fill!.Litres, 10);
    }

    [Fact]
    public void TheAuthoredMinimum_DropsATrickleFill()
    {
        FuelFillTracker tracker = Tracker(FuelRules.Default with { MinFillLitres = 1.0 });
        tracker.Observe(20.0, Start, false);
        tracker.Observe(20.5, Start.AddSeconds(1), false);

        Assert.Null(tracker.Observe(20.5, Start.AddSeconds(5), false));
    }

    [Fact]
    public void TheLitres_AreRoundedToTheCentTheLedgerShows()
    {
        FuelFillTracker tracker = Tracker();
        tracker.Observe(20.0, Start, false);
        tracker.Observe(25.678, Start.AddSeconds(1), false);

        FuelFill? fill = tracker.Observe(25.678, Start.AddSeconds(4), false);
        Assert.NotNull(fill);
        Assert.Equal(5.68, fill!.Litres, 10);
    }
}
