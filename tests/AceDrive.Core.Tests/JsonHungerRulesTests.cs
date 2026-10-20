using System.Globalization;
using System.Text.Json;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Hunger rules loader: Unity's shipped asset in data form, Unity's
/// shipped asset back out (never an exception — the overlay must start over
/// any data file).</summary>
public sealed class JsonHungerRulesTests
{
    private const string ValidJson = """
        {
          "MaxHunger": 40,
          "PerHour": 2,
          "PancakeThreshold": 15
        }
        """;

    [Fact]
    public void ValidFile_LoadsEveryRule()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var log = new TestLog();
        HungerRules rules = Load(ValidJson, log);

        Assert.Equal(40, rules.MaxHunger);
        Assert.Equal(2, rules.PerHour);
        Assert.Equal(15, rules.PancakeThreshold);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void MissingFile_FallsBackToDefaultWithWarning()
    {
        var log = new TestLog();
        HungerRules rules = new JsonHungerRules(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "hunger_rules.json"), log).Load();

        Assert.Equal(HungerRules.Default, rules);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaultWithWarning()
    {
        var log = new TestLog();
        Assert.Equal(HungerRules.Default, Load("{ not json", log));
        Assert.Single(log.Warnings);
    }

    [Theory]
    [InlineData(0, 1, 14)]   // no appetite: the meter would never move
    [InlineData(36, 0, 14)]  // zero increment: the meter would never move
    [InlineData(-4, 1, 14)]  // negative cap
    [InlineData(36, 1, 0)]   // threshold below one can never equal a reading
    [InlineData(36, 1, 36)]  // unity returns at full hunger, so an event AT the cap never fires
    [InlineData(36, 1, 50)]  // threshold past the cap can never equal a reading
    public void InvalidRules_FallBackToUnityDefaults(int maxHunger, int perHour, int threshold)
    {
        // Each case breaks exactly one rule, so the guard that catches it is
        // the one under test (a partial JSON would fail every check at once).
        var log = new TestLog();
        string json = JsonSerializer.Serialize(
            new HungerRules(maxHunger, perHour, threshold));
        Assert.Equal(HungerRules.Default, Load(json, log));
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void ShippedData_KeepsTheUnityConfig()
    {
        // The shipped table is the ported balance: a drift from Unity's
        // GameTimeConfig.asset / PlayerDefaults shows up here instead of in
        // the game.
        var log = new TestLog();
        string file = Path.Combine(
            AppContext.BaseDirectory, "Data", "hunger_rules.json");
        Assert.True(File.Exists(file), "hunger_rules.json is not copied to the output");

        Assert.Equal(HungerRules.Default, new JsonHungerRules(file, log).Load());
        Assert.Empty(log.Warnings);
    }

    private static HungerRules Load(string json, TestLog log)
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, json);
            return new JsonHungerRules(file, log).Load();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
