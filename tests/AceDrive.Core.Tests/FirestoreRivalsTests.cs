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

    private static FirestoreRivals Create(
        FakeFirestoreDatabase db, string? uid = "uid-1", string? ownName = "Me") =>
        new(new FirestorePlayers(db, new FakeClock(), new NullLogger()),
            () => uid, () => ownName, new NullLogger(), CancellationToken.None);

    [Fact]
    public async Task Board_ExcludesOwnDocAndRanksTheRest()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Players] =
            [Player("me", 10), Player("ahead-1", 100), Player("ahead-2", 50)];
        FirestoreRivals rivals = Create(db);
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
    public async Task Board_CountsAnEarlierUsernameOfTheSamePlayerAsARival()
    {
        // Reset GAME keeps the anonymous uid but lets the player pick a new
        // username; the old row must stay on the board (one row per name).
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Players] =
        [
            Player("elgee", 100),
            Player("elger", 10),
            Player("rival", 50),
        ];
        // The doc key for "ElGer" is "elger": it is this player's current row.
        FirestoreRivals rivals = Create(db, uid: "uid-1", ownName: "ElGer");

        await rivals.RefreshBoardAsync();

        RivalsResult? result = rivals.Refresh(10);
        Assert.Equal(3, result!.CurrentRank);
        Assert.Equal(3, result.TotalCompetitors);
    }

    [Fact]
    public async Task Board_HidesTheLegacyUidRowThatStillCarriesTheCurrentName()
    {
        // Rows written before the name-keyed board carry the uid as their id;
        // while the row still says our current name it must not turn the
        // player into their own competitor.
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Players] =
        [
            new FirestoreDocument("uid-1", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["xp"] = 10.0,
                ["displayName"] = "ElGer",
            }),
            Player("rival", 50),
        ];
        FirestoreRivals rivals = Create(db, uid: "uid-1", ownName: "ElGer");

        await rivals.RefreshBoardAsync();

        RivalsResult? result = rivals.Refresh(10);
        Assert.Equal(2, result!.CurrentRank);
        Assert.Equal(2, result.TotalCompetitors);
    }

    [Fact]
    public async Task Board_KeepsALegacyRowUnderAnEarlierUsernameAsARival()
    {
        // The one-time move needs the name-keyed rules. If it fails, the row
        // still records the player's earlier username, which is another world
        // identity and must stay on the board instead of leaving 1 of 1.
        var db = new FakeFirestoreDatabase
        {
            NextCreateResult = FirestoreCreateResult.Failed,
        };
        db.Collections[FirestoreCollections.Players] =
        [
            new FirestoreDocument("uid-1", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["xp"] = 100.0,
                ["displayName"] = "ElGee",
            }),
            Player("rival", 50),
        ];
        FirestoreRivals rivals = Create(db, uid: "uid-1", ownName: "ElGer");

        await rivals.RefreshBoardAsync();

        RivalsResult? result = rivals.Refresh(10);
        Assert.Equal(3, result!.CurrentRank);
        Assert.Equal(3, result.TotalCompetitors);
    }

    [Fact]
    public async Task Board_MovesTheLegacyUidRowUnderItsUsername()
    {
        // A board written before the name key stored everything on {uid}.
        // The first load must move that row to the recorded username so the
        // old identity survives as its own rival instead of being hidden.
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Players] =
        [
            new FirestoreDocument("uid-1", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["xp"] = 100.0,
                ["displayName"] = "ElGer",
            }),
            Player("rival", 50),
        ];
        FirestoreRivals rivals = Create(db, uid: "uid-1", ownName: "ElGee");

        await rivals.RefreshBoardAsync();

        var (path, fields) = Assert.Single(db.Sets);
        Assert.Equal("acevo_players/elger", path);
        Assert.Equal(100.0, fields["xp"]);
        Assert.Equal("ElGer", fields["displayName"]);
        Assert.Equal("uid-1", fields["ownerUid"]);
        Assert.Equal(["acevo_players/uid-1"], Assert.Single(db.Deleted));
    }

    [Fact]
    public async Task BoardFailure_KeepsTheRestoredSnapshot()
    {
        var db = new FakeFirestoreDatabase { ListFails = true };
        FirestoreRivals rivals = Create(db);
        rivals.RestoreSnapshot(new DriverState(0, 0) { RivalsRank = 4, RivalsTotal = 12 });

        await rivals.RefreshBoardAsync();

        RivalsResult? result = rivals.Refresh(10);
        Assert.Equal(4, result!.CurrentRank);
        Assert.Equal(12, result.TotalCompetitors);
    }

    [Fact]
    public async Task PushXp_WritesTheNameKeyedPlayerDoc()
    {
        var db = new FakeFirestoreDatabase();
        FirestoreRivals rivals = Create(db);

        await rivals.PushXpAsync(42.5, "Ace");

        var (path, fields) = Assert.Single(db.Sets);
        Assert.Equal("acevo_players/ace", path);
        Assert.Equal(42.5, fields["xp"]);
        Assert.Equal("Ace", fields["displayName"]);
        Assert.Equal("uid-1", fields["ownerUid"]);
    }

    [Fact]
    public async Task PushXp_NoUid_DropsTheWrite()
    {
        var db = new FakeFirestoreDatabase();
        FirestoreRivals rivals = Create(db, uid: null);

        await rivals.PushXpAsync(42.5, "Ace");

        Assert.Empty(db.Sets);
    }

    [Fact]
    public async Task PushXp_NoDisplayName_DropsTheWrite()
    {
        // Before the opening screen there is no world identity to key on.
        var db = new FakeFirestoreDatabase();
        FirestoreRivals rivals = Create(db);

        await rivals.PushXpAsync(42.5, "   ");

        Assert.Empty(db.Sets);
    }
}
