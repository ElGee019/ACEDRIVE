using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// The weekly valuation board (PORTPLAN 49): the REST list mapped through the
/// Unity field names, the player's own doc as the only wipe target, the legacy
/// cloud house doc filtered out, and the offline null-object that never
/// reports a rival.
/// </summary>
public sealed class FirestoreValuationBoardTests
{
    [Fact]
    public async Task List_MapsTheWeeklyDocsAndDropsTheLegacyHouseEntry()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Valuation] =
        [
            new FirestoreDocument(ValuationRules.HouseEntryDocId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["playerName"] = ValuationRules.HouseEntryName,
                    ["score"] = 999.0,
                }),
            new FirestoreDocument("uid-2", new Dictionary<string, object?>(
                StringComparer.Ordinal)
            {
                ["playerName"] = "Rival",
                ["carId"] = "ks_test",
                ["score"] = 88.5,
            }),
        ];
        FirestoreValuationBoard board = Create(db);

        IReadOnlyList<ValuationEntry>? entries = await board.ListAsync();

        Assert.NotNull(entries);
        ValuationEntry rival = Assert.Single(entries!);
        Assert.Equal("uid-2", rival.Uid);
        Assert.Equal("Rival", rival.PlayerName);
    }

    [Fact]
    public async Task List_OnAFailedRead_AnswersNull()
    {
        var db = new FakeFirestoreDatabase { ListFails = true };
        Assert.Null(await Create(db).ListAsync());
    }

    [Fact]
    public async Task Push_WritesThisPlayersEntry()
    {
        var db = new FakeFirestoreDatabase();
        FirestoreValuationBoard board = Create(db);

        bool pushed = await board.PushEntryAsync("Ace", "ks_test", 2, 0.1, 77.0);

        Assert.True(pushed);
        var (path, fields) = Assert.Single(db.Sets);
        Assert.Equal("acevo_enteredForValuation/uid-1", path);
        Assert.Equal("ks_test", fields["carId"]);
        Assert.Equal(2, fields["jobsWithCar"]);
    }

    [Fact]
    public async Task WipeOwn_DeletesOnlyThePlayersDocument()
    {
        var db = new FakeFirestoreDatabase();
        FirestoreValuationBoard board = Create(db);

        Assert.True(await board.WipeOwnAsync());

        Assert.Equal(
            ["acevo_enteredForValuation/uid-1"], Assert.Single(db.Deleted));
    }

    [Fact]
    public async Task WipeOwn_WithoutAUid_DoesNothing()
    {
        var db = new FakeFirestoreDatabase();
        FirestoreValuationBoard board = Create(db, uid: null);

        Assert.False(await board.WipeOwnAsync());
        Assert.Empty(db.Deleted);
    }

    [Fact]
    public void Watcher_StartsAndStopsIdempotently()
    {
        var db = new FakeFirestoreDatabase();
        FirestoreValuationBoard board = Create(db);

        board.StartWatching();
        board.StartWatching();
        board.StopWatching();
        board.StopWatching();

        Assert.True(board.IsOnline);
        Assert.Equal("uid-1", board.OwnUid);
    }

    [Fact]
    public async Task OfflineBoard_NeverReportsARival()
    {
        var board = new OfflineValuationBoard();

        Assert.False(board.IsOnline);
        Assert.Null(board.OwnUid);
        Assert.Empty((await board.ListAsync())!);
        Assert.True(await board.PushEntryAsync("Ace", "ks_test", 0, 0.0, 0.0));
        Assert.True(await board.WipeOwnAsync());
        // The watcher and its event stay no-ops: nothing offline can land.
        board.StartWatching();
        board.StopWatching();
    }

    private static FirestoreValuationBoard Create(
        FakeFirestoreDatabase db, string? uid = "uid-1") =>
        new(
            new FirestoreValuationEntries(
                db, _ => Task.FromResult(uid), new FakeClock(), new NullLogger()),
            () => uid, new NullLogger(), CancellationToken.None);
}
