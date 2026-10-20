using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Hunger-file degradation: a missing or corrupt hunger.json starts
/// fed — never an exception. Round-trip preserves reading and one-shot flag.</summary>
public sealed class JsonHungerStateStoreTests
{
    [Fact]
    public void MissingFile_ReturnsNull()
    {
        var store = new JsonHungerStateStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "hunger.json"),
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
            Assert.Null(new JsonHungerStateStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void NegativeReading_DropsWithAWarning()
    {
        // A hand-edited negative reading is impossible state; the meter
        // re-seeds instead of resuming it.
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """{"Hunger":-3,"ThresholdTriggered":false}""");
            var log = new TestLog();
            Assert.Null(new JsonHungerStateStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void RoundTrip_PreservesTheReading()
    {
        string file = Path.GetTempFileName();
        try
        {
            var log = new TestLog();
            var store = new JsonHungerStateStore(file, log);
            store.Save(new HungerState(14, true));

            HungerState? loaded = store.Load();
            Assert.Equal(new HungerState(14, true), loaded);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    // The resume the app actually runs through: file-backed save, new meter.
    [Fact]
    public void RoundTrip_MeterResumesTheSameReading()
    {
        string file = Path.GetTempFileName();
        try
        {
            var log = new TestLog();
            var store = new JsonHungerStateStore(file, log);
            var start = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
            var first = new Hunger(HungerRules.Default);
            _ = first.AdvanceTo(start, onboarded: true, hasPancakes: true);
            for (int i = 0; i < 14; i++)
            {
                _ = first.AdvanceTo(start.AddHours(i + 1), onboarded: true, hasPancakes: true);
            }
            store.Save(first.Snapshot());

            var restarted = new Hunger(HungerRules.Default, store.Load());
            Assert.Equal(first.Value, restarted.Value);
            Assert.Equal(first.ThresholdAlarmed, restarted.ThresholdAlarmed);
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
            new JsonHungerStateStore(dir, log).Save(new HungerState(5, false));
            Assert.Single(log.Warnings);
        }
        finally { Directory.Delete(dir); }
    }
}
