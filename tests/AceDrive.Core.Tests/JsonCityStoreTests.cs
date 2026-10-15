using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Town-file degradation: a missing or corrupt city.json starts a
/// fresh town — never an exception. Round-trip preserves every cell.</summary>
public sealed class JsonCityStoreTests
{
    private static readonly StoreState[] Cells =
    [
        new("adenau-post", 118, 26, 0.5),
        new("nurburg-kiosk", 70, 0, -0.5),
    ];

    [Fact]
    public void MissingFile_ReturnsNull()
    {
        var store = new JsonCityStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "city.json"),
            new TestLog());
        Assert.Null(store.Load());
    }

    [Fact]
    public void InvalidContent_ReturnsNullWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();
            Assert.Null(new JsonCityStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void RowsWithoutAKey_DropWithAWarning()
    {
        // A hand-edited row cannot be matched to a store; the rest still
        // resumes, so one bad line never costs the whole town.
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """[{"Key":"","Citizens":100,"StockedUnits":5,"GrowthAccumulator":0}]""");
            var log = new TestLog();
            IReadOnlyList<StoreState>? loaded = new JsonCityStore(file, log).Load();
            Assert.NotNull(loaded);
            Assert.Empty(loaded);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void RoundTrip_PreservesEveryCell()
    {
        string file = Path.GetTempFileName();
        try
        {
            var log = new TestLog();
            var store = new JsonCityStore(file, log);
            store.Save(Cells);

            IReadOnlyList<StoreState>? loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal(Cells.Length, loaded.Count);
            Assert.Equal(Cells[0], loaded[0]);
            Assert.Equal(Cells[1], loaded[1]);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    // The resume the app actually runs through: file-backed save, new economy.
    [Fact]
    public void RoundTrip_TownResumesTheSameCity()
    {
        string file = Path.GetTempFileName();
        try
        {
            var log = new TestLog();
            var store = new JsonCityStore(file, log);
            var board = new DeliveryStore[]
            {
                new("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50),
                new("nurburg-kiosk", "Nürburg Kiosk", "Fuchs", 6, 40, 2.20),
            };
            var first = new CityEconomy(board, CityRules.Default);
            first.AdvanceTo(new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc), hasDeliveryCareer: true);
            first.AdvanceTo(new DateTime(2026, 10, 12, 16, 30, 0, DateTimeKind.Utc), hasDeliveryCareer: true);
            store.Save(first.Snapshot());

            var restarted = new CityEconomy(board, CityRules.Default, store.Load());
            Assert.Equal(first.TotalCitizens(board), restarted.TotalCitizens(board));
            Assert.Equal(first.Live(board)[0].StockedUnits, restarted.Live(board)[0].StockedUnits);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void UnwritableFile_OnlyWarns()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var log = new TestLog();
            new JsonCityStore(dir, log).Save(Cells);
            Assert.Single(log.Warnings);
        }
        finally { Directory.Delete(dir); }
    }
}
