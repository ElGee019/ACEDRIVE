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
    public async Task ListSince_AsksForTheEntriesNewerThanTheStamp()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Valuation] =
        [
            new FirestoreDocument("old", new Dictionary<string, object?>(
                StringComparer.Ordinal)
            {
                ["playerName"] = "Old",
                ["enteredAt"] = "2026-11-02T11:00:00.0000000Z",
            }),
            new FirestoreDocument("new", new Dictionary<string, object?>(
                StringComparer.Ordinal)
            {
                ["playerName"] = "New",
                ["enteredAt"] = "2026-11-02T13:00:00.0000000Z",
            }),
        ];
        FirestoreValuationBoard board = Create(db);
        DateTime since = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

        IReadOnlyList<ValuationEntry>? entries = await board.ListSinceAsync(since);

        // One range read on the stamp the entries carry, in the same invariant
        // text form the push writes: no list, no document the player already
        // settled against.
        var (collection, field, after) = Assert.Single(db.TextAfterQueries);
        Assert.Equal(FirestoreCollections.Valuation, collection);
        Assert.Equal("enteredAt", field);
        Assert.Equal(FirebaseOptions.Timestamp(since), after);
        Assert.Equal("New", Assert.Single(entries!).PlayerName);
    }

    [Fact]
    public async Task OwnEntryExists_AnswersTheDocumentsPresenceAndNullWhenUnreadable()
    {
        var db = new FakeFirestoreDatabase();
        FirestoreValuationBoard board = Create(db);

        Assert.False(await board.OwnEntryExistsAsync());

        db.ByPath["acevo_enteredForValuation/uid-1"] =
            new FirestoreDocument("uid-1", new Dictionary<string, object?>(
                StringComparer.Ordinal));
        Assert.True(await board.OwnEntryExistsAsync());

        // A read that could not be answered must never read as "gone": the
        // flow would forget a live entry over a network blip.
        var unreadable = new FakeFirestoreDatabase { ListFails = true };
        Assert.Null(await Create(unreadable).OwnEntryExistsAsync());
    }

    [Fact]
    public async Task WatchOnce_WithholdsTheBatchWhileTheOwnEntryIsGone()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Valuation] =
        [
            Rival("uid-2", "Rival", 88.5, "2026-11-02T12:30:00.0000000Z"),
        ];
        FirestoreValuationBoard board = Create(db);
        int batches = 0;
        bool missing = false;
        board.Changed += _ => batches++;
        board.OwnEntryMissing += () => missing = true;
        StartWatchingByHand(board);

        Assert.False(await board.WatchOnceAsync());

        // The own doc is not listed: the batch is withheld, the loop is told to
        // stop, and the cursor stays put so a later step can still settle it.
        Assert.True(missing);
        Assert.Equal(0, batches);

        db.ByPath["acevo_enteredForValuation/uid-1"] =
            new FirestoreDocument("uid-1", new Dictionary<string, object?>(
                StringComparer.Ordinal));

        Assert.True(await board.WatchOnceAsync());
        Assert.Equal(1, batches);
    }

    [Fact]
    public async Task WatchOnce_PublishesNewEntriesAndMovesTheCursorPastThem()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Valuation] =
        [
            Rival("uid-2", "First", 88.5, "2026-11-02T12:30:00.0000000Z"),
            Rival("uid-3", "Second", 91.0, "2026-11-02T13:00:00.0000000Z"),
        ];
        db.ByPath["acevo_enteredForValuation/uid-1"] =
            new FirestoreDocument("uid-1", new Dictionary<string, object?>(
                StringComparer.Ordinal));
        FirestoreValuationBoard board = Create(db);
        IReadOnlyList<ValuationEntry>? published = null;
        board.Changed += entries => published = entries;
        StartWatchingByHand(board);

        Assert.True(await board.WatchOnceAsync());

        Assert.Equal(
            ["First", "Second"], published!.Select(entry => entry.PlayerName));
        // The next read starts from the newest stamp it saw (minus the clock
        // drift margin), not from the entry's own stamp: the weekly list is
        // never re-scanned in full.
        Assert.Single(db.TextAfterQueries);
        await board.WatchOnceAsync();
        Assert.Equal(2, db.TextAfterQueries.Count);
        Assert.True(
            string.CompareOrdinal(
                db.TextAfterQueries[1].After, db.TextAfterQueries[0].After) > 0);
    }

    [Fact]
    public void Watcher_StartsAndStopsIdempotently()
    {
        var db = new FakeFirestoreDatabase();
        FirestoreValuationBoard board = Create(db);
        DateTime since = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

        board.StartWatching(since);
        board.StartWatching(since);
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
        // The watcher and its events stay no-ops: nothing offline can land.
        board.StartWatching(DateTime.UtcNow);
        board.StopWatching();
    }

    private static FirestoreValuationBoard Create(
        FakeFirestoreDatabase db, string? uid = "uid-1") =>
        new(
            new FirestoreValuationEntries(
                db, _ => Task.FromResult(uid), new FakeClock(), new NullLogger()),
            () => uid, new NullLogger(), CancellationToken.None);

    // The step under test is driven by hand; starting the watcher only sets the
    // cursor, and stopping it keeps the 60 s timer out of the test.
    private static void StartWatchingByHand(FirestoreValuationBoard board)
    {
        board.StartWatching(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));
        board.StopWatching();
    }

    private static FirestoreDocument Rival(
        string uid, string name, double score, string enteredAt) =>
        new(uid, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playerName"] = name,
            ["carId"] = "ks_test",
            ["score"] = score,
            ["enteredAt"] = enteredAt,
        });
}
