using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Global km board → cached rarity + increment/reload.</summary>
public sealed class FirestoreCarRarityTests
{
    private static FirestoreDocument Car(string id, long km) =>
        new(id, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["totalKmDriven"] = km,
        });

    [Fact]
    public async Task Refresh_RanksTheLoadedBoard()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Cars] =
            [Car("ks_a", 300), Car("ks_b", 100), Car("ks_c", 50)];
        var rarity = new FirestoreCarRarity(
            new FirestoreCars(db, new FakeClock()), new NullLogger(), CancellationToken.None);

        await rarity.RefreshAsync();

        Assert.Equal(3, rarity.CarCount);
        Assert.Equal(1, rarity.GetRarity("ks_a"));
        Assert.Equal(3, rarity.GetRarity("ks_c"));
        Assert.Equal(4, rarity.GetRarity("ks_unknown"));
        Assert.Equal(100, rarity.GetKm("ks_b"));
    }

    [Fact]
    public async Task Refresh_Failure_KeepsTheOfflineTable()
    {
        var db = new FakeFirestoreDatabase { ListFails = true };
        var rarity = new FirestoreCarRarity(
            new FirestoreCars(db, new FakeClock()), new NullLogger(), CancellationToken.None);

        await rarity.RefreshAsync();

        Assert.Equal(0, rarity.CarCount);
        Assert.Equal(1, rarity.GetRarity("ks_a"));
    }

    [Fact]
    public async Task RecordKm_IncrementsAndReloads()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Cars] = [Car("ks_a", 100)];
        var rarity = new FirestoreCarRarity(
            new FirestoreCars(db, new FakeClock()), new NullLogger(), CancellationToken.None);

        await rarity.RecordKmAsync("ks_a", 8);

        var (path, _, increments) = Assert.Single(db.Increments);
        Assert.Equal("acevo_cars/ks_a", path);
        Assert.Equal(8, increments["totalKmDriven"]);
        // The reload read the same fake board; the increment itself is the
        // server's job, so the local rank stays the one the fake returned.
        Assert.Equal(1, rarity.GetRarity("ks_a"));
    }

    [Fact]
    public async Task RecordKm_EmptyKey_DoesNothing()
    {
        var db = new FakeFirestoreDatabase();
        var rarity = new FirestoreCarRarity(
            new FirestoreCars(db, new FakeClock()), new NullLogger(), CancellationToken.None);

        await rarity.RecordKmAsync("", 8);

        Assert.Empty(db.Increments);
    }
}
