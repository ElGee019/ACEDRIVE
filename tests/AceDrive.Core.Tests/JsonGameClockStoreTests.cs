using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Clock-file degradation: missing/invalid gameclock.json starts a
/// fresh week — never an exception. Round-trip preserves the moment.</summary>
public sealed class JsonGameClockStoreTests
{
    [Fact]
    public void MissingFile_ReturnsNull()
    {
        var store = new JsonGameClockStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "gameclock.json"),
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
            Assert.Null(new JsonGameClockStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void InvalidWeek_ReturnsNullWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """{"GameNow":"2026-10-12T12:00:00Z","Week":0}""");
            var log = new TestLog();
            Assert.Null(new JsonGameClockStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void RoundTrip_PreservesMomentAndWeek()
    {
        string file = Path.GetTempFileName();
        try
        {
            var log = new TestLog();
            var store = new JsonGameClockStore(file, log);
            var saved = new GameClockState(
                new DateTime(2026, 10, 18, 8, 5, 0, DateTimeKind.Utc), 3);
            store.Save(saved);
            GameClockState? loaded = store.Load();
            Assert.Equal(saved, loaded);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
