using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>The null-object backend keeps the receipt mechanism honest:
/// restored snapshots answer Refresh, nothing is ever pushed anywhere and
/// the live-update event stays quiet (no board exists offline).</summary>
public sealed class OfflineRivalsTests
{
    [Fact]
    public void Refresh_KnowsNothing_OnAFreshDriver()
    {
        var rivals = new OfflineRivals();
        rivals.RestoreSnapshot(null);
        Assert.Null(rivals.LastResult);
        Assert.Null(rivals.Refresh(25.0));
    }

    [Fact]
    public void RestoreSnapshot_SeedsSavedRank_AsNoChange()
    {
        var rivals = new OfflineRivals();
        rivals.RestoreSnapshot(new DriverState(100.0, 42.0, 7, 40));
        RivalsResult? r = rivals.Refresh(42.5);
        Assert.NotNull(r);
        Assert.Equal(7, r.CurrentRank);
        Assert.Equal(40, r.TotalCompetitors);
        Assert.Equal(0, r.RankChange);
        Assert.Equal(7, r.PreviousRank);
        Assert.False(r.IsFirstTime);
    }

    [Theory]
    [InlineData(0, 40)]
    [InlineData(7, 0)]
    [InlineData(-1, 40)]
    public void RestoreSnapshot_IgnoresNeverSaved(int rank, int total)
    {
        var rivals = new OfflineRivals();
        rivals.RestoreSnapshot(new DriverState(1.0, 1.0, rank, total));
        Assert.Null(rivals.Refresh(1.0));
    }

    [Fact]
    public void PushXp_IsSilentlyDropped()
    {
        var rivals = new OfflineRivals();
        rivals.PushXp(10.0, "Test");
        Assert.Null(rivals.LastResult);
    }
}
