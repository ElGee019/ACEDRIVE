using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Data-file degradation: missing/invalid fares.json falls back
/// to built-in defaults with a warning — never an exception.</summary>
public sealed class JsonFareStoreTests
{
    [Fact]
    public void MissingFile_FallsBackWithWarning()
    {
        var log = new TestLog();
        var store = new JsonFareStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "fares.json"), log);
        Assert.Equal(FareTable.Default, store.Load());
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
            Assert.Equal(FareTable.Default, new JsonFareStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void NegativeRates_FallsBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """{"BaseFare":-1,"PerKmRate":1.8,"TipRate":0.1}""");
            var log = new TestLog();
            Assert.Equal(FareTable.Default, new JsonFareStore(file, log).Load());
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
            File.WriteAllText(file, """{"BaseFare":5.0,"PerKmRate":2.0,"TipRate":0.2}""");
            var log = new TestLog();
            Assert.Equal(new FareTable(5.0, 2.0, 0.2), new JsonFareStore(file, log).Load());
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
