using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// The valuation document mapping (PORTPLAN 49): the Unity field names, the
/// clamps a hand-edited or older doc may need, and the id fallback that keeps
/// a doc without a uid field comparable.
/// </summary>
public sealed class ValuationEntryMapperTests
{
    [Fact]
    public void Map_ReadsEveryUnityField()
    {
        var doc = new FirestoreDocument("uid-1", new Dictionary<string, object?>(
            StringComparer.Ordinal)
        {
            ["uid"] = "uid-field",
            ["playerName"] = "Ace",
            ["carId"] = "ks_test",
            ["jobsWithCar"] = 3L,
            ["engineBrokenPercent"] = 0.25,
            ["score"] = 88.5,
            ["enteredAt"] = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc),
        });

        ValuationEntry? entry = ValuationEntryMapper.Map(doc);

        Assert.NotNull(entry);
        Assert.Equal("uid-field", entry!.Uid);
        Assert.Equal("Ace", entry.PlayerName);
        Assert.Equal("ks_test", entry.CarKey);
        Assert.Equal(3, entry.JobsWithCar);
        Assert.Equal(0.25, entry.EngineBrokenPercent);
        Assert.Equal(88.5, entry.Score);
        Assert.Equal(
            new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc), entry.EnteredAt);
    }

    [Fact]
    public void Map_FallsBackToTheDocumentIdAndClampsTheNumbers()
    {
        var doc = new FirestoreDocument("uid-1", new Dictionary<string, object?>(
            StringComparer.Ordinal)
        {
            ["jobsWithCar"] = -4L,
            ["engineBrokenPercent"] = 9.0,
            ["score"] = -1.0,
        });

        ValuationEntry? entry = ValuationEntryMapper.Map(doc);

        Assert.NotNull(entry);
        Assert.Equal("uid-1", entry!.Uid);
        Assert.Equal(string.Empty, entry.PlayerName);
        Assert.Equal(string.Empty, entry.CarKey);
        Assert.Equal(0, entry.JobsWithCar);
        Assert.Equal(1.0, entry.EngineBrokenPercent);
        Assert.Equal(0.0, entry.Score);
        Assert.Null(entry.EnteredAt);
    }

    [Fact]
    public void Map_RejectsADocumentWithoutIdentity()
    {
        Assert.Null(ValuationEntryMapper.Map(
            new FirestoreDocument(string.Empty, new Dictionary<string, object?>())));
    }

    [Fact]
    public void MapAll_SkipsTheUnmappableRows()
    {
        IReadOnlyList<ValuationEntry> entries = ValuationEntryMapper.MapAll(
        [
            new FirestoreDocument("uid-1", new Dictionary<string, object?>()),
            new FirestoreDocument(string.Empty, new Dictionary<string, object?>()),
            new FirestoreDocument("uid-2", new Dictionary<string, object?>()),
        ]);

        Assert.Equal(["uid-1", "uid-2"], entries.Select(entry => entry.Uid));
    }
}
