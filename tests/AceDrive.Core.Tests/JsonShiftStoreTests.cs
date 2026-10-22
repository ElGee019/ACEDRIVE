using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>shift.json boundary: the wall-clock stamps survive the round trip,
/// and every corrupt shape degrades to a closed shift instead of throwing.</summary>
public sealed class JsonShiftStoreTests
{
    private static readonly TaxiShiftSnapshot Open = new(
        IsStarted: true,
        new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 12, 12, 20, 0, DateTimeKind.Utc),
        JobsDone: 2, Earned: 66.44);

    [Fact]
    public void MissingFile_ReturnsNull()
    {
        var store = new JsonShiftStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "shift.json"),
            new TestLog());
        Assert.Null(store.Load());
    }

    [Fact]
    public void RoundTrip_PreservesTheOpenShift()
    {
        string file = TempFile();
        try
        {
            var log = new TestLog();
            var store = new JsonShiftStore(file, log);
            store.Save(Open);

            Assert.Equal(Open, store.Load());
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void InvalidContent_ReturnsNullWithWarning()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();
            Assert.Null(new JsonShiftStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void OpenShiftWithoutStamps_ReturnsNullWithWarning()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file,
                """{"IsStarted":true,"StartedAt":"0001-01-01T00:00:00","LastJobEnd":"0001-01-01T00:00:00","JobsDone":1,"Earned":10}""");
            var log = new TestLog();
            Assert.Null(new JsonShiftStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
}
