using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Live XP board: load, rank, restore and push.</summary>
public sealed class FirestoreRivalsTests
{
    private static FirestoreDocument Player(string id, double xp) =>
        new(id, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["xp"] = xp,
            ["displayName"] = id,
        });

    [Fact]
    public async Task Board_ExcludesOwnDocAndRanksTheRest()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Players] =
            [Player("me", 10), Player("ahead-1", 100), Player("ahead-2", 50)];
        var rivals = new FirestoreRivals(
            new FirestorePlayers(db, new FakeClock()),
            () => "me", new NullLogger(), CancellationToken.None);
        rivals.RestoreSnapshot(new DriverState(0, 0)
        {
            RivalsRank = 5,
            RivalsTotal = 9,
        });

        // Before the board lands, the saved snapshot answers (Unity fallback).
        RivalsResult? before = rivals.Refresh(10);
        Assert.Equal(5, before!.CurrentRank);
        Assert.Equal(9, before.TotalCompetitors);

        await rivals.RefreshBoardAsync();

        // The late board re-ranks against the saved snapshot: rank 3 of 3,
        // up two places from 5.
        RivalsResult? loaded = rivals.LastResult;
        Assert.Equal(3, loaded!.CurrentRank);
        Assert.Equal(3, loaded.TotalCompetitors);
        Assert.Equal(2, loaded.RankChange);
        Assert.Equal(5, loaded.PreviousRank);

        // A second refresh at the same XP is a quiet no-change.
        RivalsResult? again = rivals.Refresh(10);
        Assert.Equal(3, again!.CurrentRank);
        Assert.Equal(0, again.RankChange);
    }

    [Fact]
    public async Task BoardFailure_KeepsTheRestoredSnapshot()
    {
        var db = new FakeFirestoreDatabase { ListFails = true };
        var rivals = new FirestoreRivals(
            new FirestorePlayers(db, new FakeClock()),
            () => "me", new NullLogger(), CancellationToken.None);
        rivals.RestoreSnapshot(new DriverState(0, 0) { RivalsRank = 4, RivalsTotal = 12 });

        await rivals.RefreshBoardAsync();

        RivalsResult? result = rivals.Refresh(10);
        Assert.Equal(4, result!.CurrentRank);
        Assert.Equal(12, result.TotalCompetitors);
    }

    [Fact]
    public async Task PushXp_WritesThePlayerDoc()
    {
        var db = new FakeFirestoreDatabase();
        var rivals = new FirestoreRivals(
            new FirestorePlayers(db, new FakeClock()),
            () => "me", new NullLogger(), CancellationToken.None);

        await rivals.PushXpAsync(42.5, "Ace");

        var (path, fields) = Assert.Single(db.Sets);
        Assert.Equal("acevo_players/me", path);
        Assert.Equal(42.5, fields["xp"]);
        Assert.Equal("Ace", fields["displayName"]);
    }

    [Fact]
    public async Task PushXp_NoUid_DropsTheWrite()
    {
        var db = new FakeFirestoreDatabase();
        var rivals = new FirestoreRivals(
            new FirestorePlayers(db, new FakeClock()),
            () => null, new NullLogger(), CancellationToken.None);

        await rivals.PushXpAsync(42.5, "Ace");

        Assert.Empty(db.Sets);
    }
}
