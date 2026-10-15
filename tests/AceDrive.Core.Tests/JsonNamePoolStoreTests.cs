using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Data-file degradation: missing/invalid name_pools.json falls
/// back to the built-in German pool with a warning — never an exception,
/// and never an undrawable table (JsonFareStore's discipline).</summary>
public sealed class JsonNamePoolStoreTests
{
    [Fact]
    public void MissingFile_FallsBackWithWarning()
    {
        var log = new TestLog();
        var store = new JsonNamePoolStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "name_pools.json"), log);
        Assert.Equal(NamePoolTable.Default, store.Load());
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void InvalidContent_FallsBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();
            Assert.Equal(NamePoolTable.Default, new JsonNamePoolStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void EmptyPools_FallsBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """{"pools":[],"suffixes":["and some friends"]}""");
            var log = new TestLog();
            Assert.Equal(NamePoolTable.Default, new JsonNamePoolStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ZeroWeightPool_FallsBackWithWarning()
    {
        // A pool nobody can draw from would zero the roulette total: the
        // whole table degrades, the generator never sees an empty pick.
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                {"pools":[{"id":"a","weight":0,"firstNames":["Max"],"surnames":["Weber"]}],
                 "suffixes":["and some friends"]}
                """);
            var log = new TestLog();
            Assert.Equal(NamePoolTable.Default, new JsonNamePoolStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ValidFile_LoadsWithoutWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                {"pools":[{"id":"german","weight":0.75,"firstNames":["Max","Anna"],"surnames":["Weber"]}],
                 "suffixes":["and some friends","and some colleagues"]}
                """);
            var log = new TestLog();
            NamePoolTable table = new JsonNamePoolStore(file, log).Load();
            // Field-by-field reads (like JsonTodoStoreTests): record equality
            // would compare the JSON-backed lists by reference.
            NamePool pool = Assert.Single(table.Pools);
            Assert.Equal("german", pool.Id);
            Assert.Equal(0.75, pool.Weight);
            Assert.True(pool.FirstNames.SequenceEqual(["Max", "Anna"]));
            Assert.True(pool.Surnames.SequenceEqual(["Weber"]));
            Assert.True(table.Suffixes.SequenceEqual(["and some friends", "and some colleagues"]));
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
