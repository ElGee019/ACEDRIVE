using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>World-standings math pinned to the Unity RivalsService rules:
/// strictly-greater ranking, saved-data fallback while the board is
/// missing, change pinned to the last observed rank.</summary>
public sealed class RivalsStandingsTests
{
    [Fact]
    public void EmptyBoard_RanksFirstOfOne()
    {
        Assert.Equal(1, RivalsStandings.Rank(0.0, []));
    }

    [Fact]
    public void Rank_CountsOnlyStrictlyGreaterXp()
    {
        // Three ahead (13/12/9), two equal, one behind: ties stay behind
        // the player, like the Unity board (equal XP never overtakes).
        double[] others = [13.0, 12.0, 5.0, 5.0, 9.0, 4.0];
        Assert.Equal(4, RivalsStandings.Rank(5.0, others));
    }

    [Fact]
    public void Refresh_WithoutBoard_FallsBackToSavedResult()
    {
        RivalsResult saved = new(7, 40, 0, 7, false);
        Assert.Equal(saved, RivalsStandings.Refresh(100.0, null, boardLoaded: false, last: saved));
    }

    [Fact]
    public void Refresh_WithoutBoardOrResult_KnowsNothing()
    {
        Assert.Null(RivalsStandings.Refresh(100.0, null, boardLoaded: false, last: null));
    }

    [Fact]
    public void Refresh_FirstBoard_StartsPreviousRankAtTheTotal()
    {
        // Unity: no earlier rank to compare with, so the board size itself
        // is the "before" — climbing onto an empty top starts at no change.
        RivalsResult? r = RivalsStandings.Refresh(50.0, [60.0, 70.0], boardLoaded: true, last: null);
        Assert.NotNull(r);
        Assert.Equal(3, r.CurrentRank);
        Assert.Equal(3, r.TotalCompetitors);
        Assert.Equal(0, r.RankChange);
        Assert.Equal(3, r.PreviousRank);
        Assert.True(r.IsFirstTime);
    }

    [Fact]
    public void Refresh_NextBoard_ComputesChangeFromLastRank()
    {
        RivalsResult last = new(5, 24, 0, 5, false);
        // One strictly greater among four others → rank 2 of five.
        RivalsResult? r = RivalsStandings.Refresh(
            100.0, [150.0, 90.0, 100.0, 80.0], boardLoaded: true, last: last);
        Assert.NotNull(r);
        Assert.Equal(2, r.CurrentRank);
        Assert.Equal(5, r.TotalCompetitors);
        Assert.Equal(3, r.RankChange);
        Assert.Equal(5, r.PreviousRank);
        Assert.False(r.IsFirstTime);
    }

    [Fact]
    public void Refresh_LosesGround_OnTheSameBoard()
    {
        // Rivals push past the player while the board reloads: the change
        // goes negative and the delta line switches to "You lost …".
        RivalsResult last = new(1, 4, 0, 1, false);
        RivalsResult? r = RivalsStandings.Refresh(10.0, [20.0, 30.0], boardLoaded: true, last: last);
        Assert.NotNull(r);
        Assert.Equal(-2, r.RankChange);
    }
}
