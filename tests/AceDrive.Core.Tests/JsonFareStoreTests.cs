using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Data-file degradation: missing/invalid fares.json falls back
/// to built-in defaults with a warning — never an exception.</summary>
public sealed class JsonFareStoreTests
{
    private const string ValidJson = """
        {"Forfait":5.0,"TipRate":0.25,"FineAmount":20.0,"Tiers":[
        {"WidthKm":2.0,"RatePerKm":6.0},{"WidthKm":0.0,"RatePerKm":1.0}]}
        """;

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
            File.WriteAllText(file, """{"Forfait":-1,"TipRate":0.2,"FineAmount":50.0,"Tiers":[]}""");
            var log = new TestLog();
            Assert.Equal(FareTable.Default, new JsonFareStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void EmptyTiers_FallsBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """{"Forfait":10.0,"TipRate":0.2,"FineAmount":50.0,"Tiers":[]}""");
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
            File.WriteAllText(file, ValidJson);
            var log = new TestLog();
            FareTable table = new JsonFareStore(file, log).Load();
            Assert.Equal(5.0, table.Forfait);
            Assert.Equal(0.25, table.TipRate);
            Assert.Equal(20.0, table.FineAmount);
            Assert.Equal(
                [new FareTier(2.0, 6.0), new FareTier(0.0, 1.0)],
                table.Tiers);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
