using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Best-guarded road-test writes and board sorting.</summary>
public sealed class FirestoreRoadTestStandingsTests
{
    private static FirestoreRoadTestStandings Create(FakeFirestoreDatabase db) =>
        new(db, _ => Task.FromResult<string?>("uid-1"), () => "uid-1",
            new FakeClock(), new NullLogger());

    private static FirestoreDocument Result(
        string uid, string car, double value, string stamp) =>
        new(uid + "|" + car, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["uid"] = uid,
            ["playerName"] = uid,
            ["carId"] = car,
            ["time"] = value,
            ["lastUpdatedAt"] = stamp,
        });

    [Fact]
    public async Task Push_FirstResult_CreatesTheDoc()
    {
        var db = new FakeFirestoreDatabase();
        FirestoreRoadTestStandings board = Create(db);

        Assert.True(await board.PushAsync(
            RoadTestMetric.AccelerationTime, "ks_car", 5.4, "Ace"));

        var (path, fields) = Assert.Single(db.Sets);
        Assert.Equal("acevo_roadTestStandings/uid-1|ks_car", path);
        Assert.Equal(5.4, fields["time"]);
        Assert.Equal("ks_car", fields["carId"]);
    }

    [Fact]
    public async Task Push_SlowerThanTheStoredBest_IsIgnored()
    {
        var db = new FakeFirestoreDatabase();
        db.ByPath["acevo_roadTestStandings/uid-1|ks_car"] =
            Result("uid-1", "ks_car", 5.4, "2026-01-01T00:00:00Z");
        FirestoreRoadTestStandings board = Create(db);

        Assert.True(await board.PushAsync(
            RoadTestMetric.AccelerationTime, "ks_car", 6.1, "Ace"));
        Assert.Empty(db.Sets);
    }

    [Fact]
    public async Task Push_BetterThanTheStoredBest_Overwrites()
    {
        var db = new FakeFirestoreDatabase();
        db.ByPath["acevo_roadTestStandings/uid-1|ks_car"] =
            Result("uid-1", "ks_car", 6.1, "2026-01-01T00:00:00Z");
        FirestoreRoadTestStandings board = Create(db);

        Assert.True(await board.PushAsync(
            RoadTestMetric.AccelerationTime, "ks_car", 5.4, "Ace"));

        var (_, fields) = Assert.Single(db.Sets);
        Assert.Equal(5.4, fields["time"]);
    }

    [Fact]
    public async Task LoadTop_SortsLowerValuesFirst()
    {
        var db = new FakeFirestoreDatabase
        {
            QueryRows =
            [
                Result("uid-2", "ks_car", 7.9, "2026-01-02T00:00:00Z"),
                Result("uid-3", "ks_car", 5.4, "2026-01-03T00:00:00Z"),
            ],
        };
        FirestoreRoadTestStandings board = Create(db);

        IReadOnlyList<RoadTestEntry>? rows = await board.TopAsync(
            RoadTestMetric.AccelerationTime, "ks_car", 10);

        Assert.NotNull(rows);
        Assert.Equal(["uid-3", "uid-2"], rows!.Select(row => row.Uid));
    }

    [Fact]
    public async Task LoadTopCars_KeepsEachCarsBestAcrossPlayers()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections["acevo_roadTestStandings"] =
        [
            Result("uid-1", "ks_a", 6.4, "2026-01-01T00:00:00Z"),
            Result("uid-2", "ks_b", 5.1, "2026-01-02T00:00:00Z"),
            Result("uid-3", "ks_a", 4.2, "2026-01-03T00:00:00Z"),
        ];
        FirestoreRoadTestStandings board = Create(db);

        IReadOnlyList<RoadTestEntry>? rows = await board.TopCarsAsync(
            RoadTestMetric.AccelerationTime);

        Assert.NotNull(rows);
        Assert.Equal(["ks_a", "ks_b"], rows!.Select(row => row.CarKey));
        Assert.Equal("uid-3", rows[0].Uid);
    }

    [Fact]
    public async Task LoadTopCars_UnreachableBook_IsNull()
    {
        var db = new FakeFirestoreDatabase { ListFails = true };
        FirestoreRoadTestStandings board = Create(db);

        Assert.Null(await board.TopCarsAsync(RoadTestMetric.BrakeDistance));
    }
}
