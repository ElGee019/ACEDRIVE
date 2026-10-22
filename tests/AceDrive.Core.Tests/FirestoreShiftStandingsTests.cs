using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Live best-shift board: load, tie-break and the restored fallback.</summary>
public sealed class FirestoreShiftStandingsTests
{
    private static readonly DateTime Epoch = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    private static FirestoreDocument Player(string id, double earnings, DateTime? stamp)
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["highestShiftEarnings"] = earnings,
        };
        if (stamp is { } value)
        {
            fields["highestShiftEarningsFirstEarnedAt"] = FirebaseOptions.Timestamp(value);
        }
        return new FirestoreDocument(id, fields);
    }

    private static FirestoreShiftStandings Create(FakeFirestoreDatabase db) =>
        new(new FirestorePlayers(db, new FakeClock()),
            () => "me", new NullLogger(), CancellationToken.None);

    [Fact]
    public void Board_ExcludesOwnDocAndRanksTheRest()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Players] =
            [Player("me", 10, Epoch), Player("ahead", 500, Epoch), Player("behind", 80, Epoch)];
        FirestoreShiftStandings board = Create(db);
        board.RestoreSnapshot(new DriverState(0, 0) { ShiftRank = 5, ShiftTotal = 9 });

        // The refresh triggers the load; the late board re-ranks against the
        // saved snapshot, so the movement reads 5 -> 2.
        ShiftStandingsResult? loaded = board.Refresh(100, Epoch.Ticks);
        Assert.Equal(2, loaded!.CurrentRank);
        Assert.Equal(3, loaded.TotalCompetitors);
        Assert.Equal(3, loaded.RankChange);
        Assert.Equal(5, loaded.PreviousRank);

        // A second refresh at the same record is a quiet no-change.
        ShiftStandingsResult? again = board.Refresh(100, Epoch.Ticks);
        Assert.Equal(2, again!.CurrentRank);
        Assert.Equal(0, again.RankChange);
    }

    [Fact]
    public void Tie_MissingStampRanksBelowAnEarlierEarner()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Players] =
        [
            Player("me", 500, Epoch),
            // No first-earned stamp: it must not win the tie (Unity MaxValue).
            Player("stampless", 500, stamp: null),
            Player("first", 500, Epoch.AddHours(-1)),
        ];
        FirestoreShiftStandings board = Create(db);

        ShiftStandingsResult? result = board.Refresh(500, Epoch.Ticks);

        Assert.Equal(2, result!.CurrentRank);
        Assert.Equal(3, result.TotalCompetitors);
    }

    [Fact]
    public void BoardFailure_KeepsTheRestoredSnapshot()
    {
        var db = new FakeFirestoreDatabase { ListFails = true };
        FirestoreShiftStandings board = Create(db);
        board.RestoreSnapshot(new DriverState(0, 0) { ShiftRank = 4, ShiftTotal = 12 });

        ShiftStandingsResult? result = board.Refresh(100, Epoch.Ticks);

        Assert.Equal(4, result!.CurrentRank);
        Assert.Equal(12, result.TotalCompetitors);
    }
}
