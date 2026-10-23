using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>The addresses file: a discovered door survives a restart.</summary>
public sealed class JsonPoiDirectoryStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsEveryDoor()
    {
        string file = TempFile();
        try
        {
            var log = new TestLog();
            var store = new JsonPoiDirectoryStore(file, log);
            PointOfInterest bank = new(
                "bank", "Bank", new AddressLocation("Street 2", 17, HouseSide.Left),
                PoiTypes.Bank);
            PointOfInterest max = new(
                PoiCatalog.MaxGroceryId, "Max's Grocery Store",
                new AddressLocation("Street 1", 4, HouseSide.Right),
                PoiTypes.Grocery, "Max Silvestri", "groceries", ["MAPLE SYRUP"]);

            store.Save([bank, max]);
            IReadOnlyList<PointOfInterest> loaded = store.Load();

            Assert.Equal(2, loaded.Count);
            Assert.Equal(bank, loaded[0]);
            PointOfInterest maxLoaded = loaded[1];
            Assert.Equal(max.Id, maxLoaded.Id);
            Assert.Equal(max.Address, maxLoaded.Address);
            Assert.Equal(max.Name, maxLoaded.Name);
            Assert.Equal(max.OwnerName, maxLoaded.OwnerName);
            Assert.Equal(max.Category, maxLoaded.Category);
            Assert.Equal(["MAPLE SYRUP"], maxLoaded.GuaranteedItems);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Load_MissingFile_IsEmptyWithoutWarning()
    {
        var log = new TestLog();

        IReadOnlyList<PointOfInterest> pois =
            new JsonPoiDirectoryStore(TempFile(), log).Load();

        Assert.Empty(pois);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void Load_CorruptFile_DegradesToEmptyWithWarning()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();

            IReadOnlyList<PointOfInterest> pois =
                new JsonPoiDirectoryStore(file, log).Load();

            Assert.Empty(pois);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Load_DropsUnusableRows()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file,
                """
                [
                  {"id":"bank","name":"Bank",
                   "address":{"streetName":"Street 1","houseNumber":4}},
                  {"id":"","name":"No id",
                   "address":{"streetName":"Street 1","houseNumber":4}},
                  {"id":"far","name":"Far",
                   "address":{"streetName":"","houseNumber":4}}
                ]
                """);
            var log = new TestLog();

            IReadOnlyList<PointOfInterest> pois =
                new JsonPoiDirectoryStore(file, log).Load();

            Assert.Equal("bank", Assert.Single(pois).Id);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
}
