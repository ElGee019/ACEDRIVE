using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// Live operating-zone board: the own-row exclusion, the strictly-greater rank
/// rule (a tie never overtakes) and the live-update event the expansion page
/// follows instead of polling.
/// </summary>
public sealed class FirestoreZoneStandingsTests
{
    private static FirestoreDocument Player(string id, double zoneKm) => new(
        id, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["longestZoneKm"] = zoneKm,
        });

    private static FirestoreZoneStandings Create(FakeFirestoreDatabase db) =>
        new(new FirestorePlayers(db, new FakeClock(), new NullLogger()),
            () => "uid-1", () => "Me", new NullLogger(), CancellationToken.None);

    [Fact]
    public void Board_ExcludesOwnDocAndRanksTheRest()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Players] =
            [Player("me", 10), Player("ahead", 5.5), Player("behind", 1)];
        FirestoreZoneStandings board = Create(db);
        Assert.True(board.IsAvailable);

        // The refresh triggers the load, and the late board publishes the rank
        // against the cached rows: one rival is longer, one is shorter.
        board.Refresh(2.9);

        ZoneStandingsResult? result = board.LastResult;
        Assert.NotNull(result);
        Assert.Equal(2, result.CurrentRank);
        Assert.Equal(3, result.TotalCompetitors);
        Assert.True(result.IsFirstTime);
    }

    [Fact]
    public void Tie_KeepsTheSameRankAsTheRivalWithTheSameArea()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Players] =
            [Player("me", 10), Player("tied", 2.9)];
        FirestoreZoneStandings board = Create(db);

        board.Refresh(2.9);

        // Only a STRICTLY longer zone counts as ahead, so a tie never pushes
        // the player down the board (Unity CalculatePlayerRank).
        Assert.Equal(1, board.LastResult!.CurrentRank);
        Assert.Equal(2, board.LastResult.TotalCompetitors);
    }

    [Fact]
    public void Refresh_PublishesTheResultToSubscribers()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Players] = [Player("ahead", 5.5)];
        FirestoreZoneStandings board = Create(db);
        ZoneStandingsResult? published = null;
        board.ResultUpdated += result => published = result;

        board.Refresh(2.9);

        Assert.NotNull(published);
        Assert.Same(board.LastResult, published);
    }
}
