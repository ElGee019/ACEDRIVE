using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The missing-person run's persisted half (ToWhereIsWilly.md §2.9/§3.1): the
/// completion stamp rides the driver state as a game date, opens on exactly
/// that Wednesday and drops a stamp that is not a real date.
/// </summary>
public sealed class WhereIsWillyLedgerTests
{
    private static readonly DateTime Wednesday =
        new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FreshLedger_IsOpenAndWritesNull()
    {
        var ledger = new WhereIsWillyLedger();

        Assert.Null(ledger.LastCompletedDate);
        Assert.False(ledger.CompletedOn(Wednesday));
    }

    [Fact]
    public void MarkCompleted_StampsTheGameDateInTheInvariantShape()
    {
        var ledger = new WhereIsWillyLedger();

        ledger.MarkCompleted(Wednesday);

        Assert.Equal("2026-10-14", ledger.LastCompletedDate);
        Assert.True(ledger.CompletedOn(Wednesday));
        // The same day at any hour is stamped: the gate is the Wednesday, not
        // the minute the receipt was read.
        Assert.True(ledger.CompletedOn(Wednesday.AddHours(7)));
        Assert.False(ledger.CompletedOn(Wednesday.AddDays(1)));
    }

    [Fact]
    public void SavedStamp_ResumesFromTheDriverState()
    {
        DriverState saved = DriverState.Fresh with { LastWillyDate = "2026-10-14" };

        var ledger = new WhereIsWillyLedger(saved);

        Assert.Equal("2026-10-14", ledger.LastCompletedDate);
        Assert.True(ledger.CompletedOn(Wednesday));
    }

    [Fact]
    public void SavedStamp_DropsGarbageInsteadOfMatchingIt()
    {
        DriverState saved = DriverState.Fresh with { LastWillyDate = "14/10/2026" };

        var ledger = new WhereIsWillyLedger(saved);

        Assert.Null(ledger.LastCompletedDate);
        Assert.False(ledger.CompletedOn(Wednesday));
    }

    [Fact]
    public void RoundTripThroughTheDriverSnapshot_KeepsTheStamp()
    {
        var ledger = new WhereIsWillyLedger();
        ledger.MarkCompleted(Wednesday);

        // What GameStateSaver writes, read back the way the root reads it.
        DriverState written = DriverState.Fresh with { LastWillyDate = ledger.LastCompletedDate };
        var resumed = new WhereIsWillyLedger(written);

        Assert.Equal("2026-10-14", resumed.LastCompletedDate);
        Assert.True(resumed.CompletedOn(Wednesday));
    }
}
