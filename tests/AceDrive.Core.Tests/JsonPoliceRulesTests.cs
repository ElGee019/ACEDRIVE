using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Data-file degradation for the speed-challenge police rules:
/// missing or invalid Data/police.json falls back to the built-in defaults.</summary>
public sealed class JsonPoliceRulesTests
{
    private const string ValidJson = """
        {"patrolsPerJob":2,"patrolRadiusMeters":25,
         "speedLimitKmh":60,"excludedStopRadiusMeters":50,
         "warningRadiusMeters":100}
        """;

    [Fact]
    public void MissingFile_FallsBackWithWarning()
    {
        var log = new TestLog();
        var store = new JsonPoliceRulesStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "police.json"), log);

        Assert.Equal(PoliceRules.Default, store.Load());
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
            Assert.Equal(PoliceRules.Default, new JsonPoliceRulesStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void NegativeOrZeroValues_FallBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                {"patrolsPerJob":-1,"patrolRadiusMeters":30,
                 "speedLimitKmh":50,"excludedStopRadiusMeters":60,
                 "warningRadiusMeters":120}
                """);
            var log = new TestLog();
            Assert.Equal(PoliceRules.Default, new JsonPoliceRulesStore(file, log).Load());
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

            PoliceRules rules = new JsonPoliceRulesStore(file, log).Load();

            Assert.Equal(new PoliceRules(2, 25.0, 60, 50.0, 100.0), rules);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void RealDataFile_LoadsTheShippedRules()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "Data", "police.json");
        var log = new TestLog();

        PoliceRules rules = new JsonPoliceRulesStore(file, log).Load();

        Assert.Empty(log.Warnings);
        Assert.Equal(1, rules.PatrolsPerJob);
        Assert.Equal(30.0, rules.PatrolRadiusMeters);
        Assert.Equal(50, rules.SpeedLimitKmh);
        Assert.Equal(60.0, rules.ExcludedStopRadiusMeters);
        Assert.Equal(120.0, rules.WarningRadiusMeters);
    }
}
