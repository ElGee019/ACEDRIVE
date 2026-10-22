using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Weekly valuation entry storage and the Monday reset.</summary>
public sealed class FirestoreValuationEntriesTests
{
    private static FirestoreValuationEntries Create(FakeFirestoreDatabase db) =>
        new(db, _ => Task.FromResult<string?>("uid-1"),
            new FakeClock(), new NullLogger());

    [Fact]
    public async Task PushEntry_WritesTheShowcaseFieldsAndTheTtl()
    {
        var db = new FakeFirestoreDatabase();
        FirestoreValuationEntries entries = Create(db);

        Assert.True(await entries.PushEntryAsync(
            "Ace", "ks_car", jobsWithCar: 3, engineBrokenPercent: 0.2, score: 88.5));

        var (path, fields) = Assert.Single(db.Sets);
        Assert.Equal("acevo_enteredForValuation/uid-1", path);
        Assert.Equal("ks_car", fields["carId"]);
        Assert.Equal(3, fields["jobsWithCar"]);
        Assert.IsType<DateTime>(fields["expiresAt"]);
    }

    [Fact]
    public async Task Reset_DeletesTheListAndSeedsTheHouseEntry()
    {
        var db = new FakeFirestoreDatabase();
        db.Collections[FirestoreCollections.Valuation] =
        [
            new FirestoreDocument("uid-1", new Dictionary<string, object?>(StringComparer.Ordinal)),
            new FirestoreDocument("uid-2", new Dictionary<string, object?>(StringComparer.Ordinal)),
        ];
        FirestoreValuationEntries entries = Create(db);

        Assert.True(await entries.ResetAsync("ks_house"));

        var deleted = Assert.Single(db.Deleted);
        Assert.Equal(
            ["acevo_enteredForValuation/uid-1", "acevo_enteredForValuation/uid-2"],
            deleted);
        var (path, fields) = Assert.Single(db.Sets);
        Assert.Equal("acevo_enteredForValuation/house-entry", path);
        Assert.Equal(ValuationRules.HouseEntryName, fields["playerName"]);
    }

    [Fact]
    public async Task Wipe_EmptyList_IsSuccess()
    {
        var db = new FakeFirestoreDatabase();
        Assert.True(await Create(db).WipeAsync());
        Assert.Empty(db.Deleted);
    }
}
