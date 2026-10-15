using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Pick-file degradation: a missing or invalid roadtest.json means
/// a fresh draw — never an exception. Round-trip preserves pick and bank.</summary>
public sealed class JsonRoadTestStoreTests
{
    private static readonly RoadTestSnapshot Sample = new(
        "ks_abarth_695_biposto", ["ks_mazda_mx5_na", "ks_datsun_240z_fairlady"]);

    [Fact]
    public void MissingFile_ReturnsNull()
    {
        var store = new JsonRoadTestStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "roadtest.json"),
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
            Assert.Null(new JsonRoadTestStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void MissingBank_ReturnsNullWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """{"CarId":"ks_mazda_mx5_na"}""");
            var log = new TestLog();
            Assert.Null(new JsonRoadTestStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void RoundTrip_PreservesPickAndBank()
    {
        string file = Path.GetTempFileName();
        try
        {
            var log = new TestLog();
            var store = new JsonRoadTestStore(file, log);
            store.Save(Sample);

            RoadTestSnapshot? loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal(Sample.CarId, loaded.CarId);
            Assert.Equal(Sample.TestedIds, loaded.TestedIds, StringComparer.Ordinal);
            Assert.Single(log.Infos);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    // The ledger resume is what the restart actually runs through: same pool,
    // file-backed save, unrelated seed.
    [Fact]
    public void RoundTrip_LedgerResumesTheSameCar()
    {
        string file = Path.GetTempFileName();
        try
        {
            var log = new TestLog();
            var store = new JsonRoadTestStore(file, log);
            var first = new RoadTestLedger(TestRoadCars.Pool, seed: 7);
            store.Save(first.Snapshot());

            var restarted = new RoadTestLedger(TestRoadCars.Pool, seed: 1234, saved: store.Load());
            Assert.Equal(first.Current?.Id, restarted.Current?.Id);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void UnwritableFile_OnlyWarns()
    {
        // A directory where the file should be: the write fails, the game does
        // not, and the warning is the only trace (AGENTS.md: degrade gracefully).
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var log = new TestLog();
            new JsonRoadTestStore(dir, log).Save(new RoadTestSnapshot(null, []));
            Assert.Single(log.Warnings);
        }
        finally { Directory.Delete(dir); }
    }
}
