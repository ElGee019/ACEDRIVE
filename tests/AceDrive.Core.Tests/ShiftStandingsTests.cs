using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Best-single-shift rank math and its persisted record.</summary>
public sealed class ShiftStandingsTests
{
    [Fact]
    public void Rank_CountsStrictlyHigherEarnings()
    {
        Assert.Equal(1, ShiftStandings.Rank(500, 10, []));
        Assert.Equal(2, ShiftStandings.Rank(
            500, 10, [new ShiftBoardEntry(600, 1)]));
        Assert.Equal(1, ShiftStandings.Rank(
            600, 10, [new ShiftBoardEntry(600, 11)]));
    }

    [Fact]
    public void Rank_EqualEarnings_EarlierStampWins()
    {
        // The other player reached 500 first, so the player is behind.
        Assert.Equal(2, ShiftStandings.Rank(
            500, 20, [new ShiftBoardEntry(500, 10)]));
        // The player reached it first, so the other is behind.
        Assert.Equal(1, ShiftStandings.Rank(
            500, 10, [new ShiftBoardEntry(500, 20)]));
    }

    [Fact]
    public void Refresh_FirstBoard_UsesItsSizeAsPrevious()
    {
        var result = ShiftStandings.Refresh(
            500, 10, [new ShiftBoardEntry(600, 1), new ShiftBoardEntry(400, 2)],
            boardLoaded: true, last: null);

        Assert.NotNull(result);
        Assert.Equal(2, result!.CurrentRank);
        Assert.Equal(3, result.TotalCompetitors);
        Assert.Equal(1, result.RankChange);
        Assert.Equal(3, result.PreviousRank);
        Assert.True(result.IsFirstTime);
    }

    [Fact]
    public void Refresh_MissingBoard_KeepsTheLastSnapshot()
    {
        var last = new ShiftStandingsResult(3, 10, 0, 3, false);
        Assert.Same(last, ShiftStandings.Refresh(
            500, 10, null, boardLoaded: false, last: last));
    }

    [Fact]
    public void ShiftRecord_OnlyImprovesOnStrictlyHigherEarnings()
    {
        var record = new ShiftRecord(null);
        Assert.True(record.Record(500, 100));
        Assert.Equal(500, record.BestEarnings);
        Assert.Equal(100, record.BestFirstEarnedTicks);

        Assert.False(record.Record(500, 200));
        Assert.Equal(100, record.BestFirstEarnedTicks);

        Assert.True(record.Record(700, 300));
        Assert.Equal(300, record.BestFirstEarnedTicks);
    }

    [Fact]
    public void ShiftRecord_RestoresSavedValuesAndSnapshot()
    {
        var saved = new DriverState(0, 0)
        {
            BestShiftEarnings = 800,
            BestShiftFirstEarnedTicks = 123,
            ShiftRank = 4,
            ShiftTotal = 20,
        };
        var record = new ShiftRecord(saved);
        Assert.Equal(800, record.BestEarnings);
        Assert.Equal(123, record.BestFirstEarnedTicks);
        Assert.Equal(4, record.Rank);
        Assert.Equal(20, record.Total);
    }
}
