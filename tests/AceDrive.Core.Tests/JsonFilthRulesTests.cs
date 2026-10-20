using System.Globalization;
using System.Text.Json;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Filth rules loader: Unity's shipped CarWashService in data form,
/// and Unity's defaults back out (never an exception — the overlay must start
/// over any data file).</summary>
public sealed class JsonFilthRulesTests
{
    private const string ValidJson = """
        {
          "MaxFilth": 30,
          "WashPrice": 75
        }
        """;

    [Fact]
    public void ValidFile_LoadsEveryRule()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var log = new TestLog();
        FilthRules rules = Load(ValidJson, log);

        Assert.Equal(30, rules.MaxFilth);
        Assert.Equal(75, rules.WashPrice);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void MissingFile_FallsBackToDefaultWithWarning()
    {
        var log = new TestLog();
        FilthRules rules = new JsonFilthRules(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "filth_rules.json"), log).Load();

        Assert.Equal(FilthRules.Default, rules);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaultWithWarning()
    {
        var log = new TestLog();
        Assert.Equal(FilthRules.Default, Load("{ not json", log));
        Assert.Single(log.Warnings);
    }

    [Theory]
    [InlineData(0, 50)]    // no cap: the reading could never hold dirt
    [InlineData(-4, 50)]   // negative cap
    [InlineData(25, 0)]    // a free wash makes the price meaningless
    [InlineData(25, -5)]   // negative price
    public void InvalidRules_FallBackToUnityDefaults(int maxFilth, int washPrice)
    {
        // Each case breaks exactly one rule, so the guard that catches it is
        // the one under test (a partial JSON would fail every check at once).
        var log = new TestLog();
        string json = JsonSerializer.Serialize(new FilthRules(maxFilth, washPrice));
        Assert.Equal(FilthRules.Default, Load(json, log));
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void ShippedData_KeepsTheUnityConfig()
    {
        // The shipped table is the ported balance: a drift from Unity's
        // CarWashService shows up here instead of in the game.
        var log = new TestLog();
        string file = Path.Combine(
            AppContext.BaseDirectory, "Data", "filth_rules.json");
        Assert.True(File.Exists(file), "filth_rules.json is not copied to the output");

        Assert.Equal(FilthRules.Default, new JsonFilthRules(file, log).Load());
        Assert.Empty(log.Warnings);
    }

    private static FilthRules Load(string json, TestLog log)
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, json);
            return new JsonFilthRules(file, log).Load();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
