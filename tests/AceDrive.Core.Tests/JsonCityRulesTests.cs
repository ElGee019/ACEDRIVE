using System.Globalization;
using System.Text.Json;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>City rules loader: valid data in, Unity's shipped config out
/// (never an exception — the overlay must start over any data file).</summary>
public sealed class JsonCityRulesTests
{
    private const string ValidJson = """
        {
          "DefaultCitizens": 120, "MinCitizens": 80,
          "CitizenTickMinutes": 20, "DepletionTickMinutes": 60,
          "HighPercent": 70, "MediumPercent": 55, "LowPercent": 35, "VeryLowPercent": 20,
          "HighDelta": 2, "MediumDelta": 1, "NeutralDelta": 0, "LowDelta": -1, "VeryLowDelta": -2,
          "DepletionFirstCitizens": 150, "DepletionStepCitizens": 50
        }
        """;

    [Fact]
    public void ValidFile_LoadsEveryRule()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var log = new TestLog();
        CityRules rules = Load(ValidJson, log);

        Assert.Equal(120, rules.DefaultCitizens);
        Assert.Equal(20, rules.CitizenTickMinutes);
        Assert.Equal(-2, rules.VeryLowDelta);
        Assert.Equal(50, rules.DepletionStepCitizens);
        Assert.Empty(log.Warnings);
    }

    [Theory]
    [MemberData(nameof(BrokenRules))]
    public void InvalidRules_FallBackToUnityDefaults(CityRules broken)
    {
        // Each case breaks exactly one rule, so the guard that catches it is
        // the one under test (a partial JSON would fail every check at once).
        var log = new TestLog();
        Assert.Equal(CityRules.Default, Load(JsonSerializer.Serialize(broken), log));
        Assert.Single(log.Warnings);
    }

    public static IEnumerable<object[]> BrokenRules()
    {
        yield return [CityRules.Default with { CitizenTickMinutes = 0 }];
        yield return [CityRules.Default with { DepletionTickMinutes = -5 }];
        yield return [CityRules.Default with { DefaultCitizens = 60, MinCitizens = 80 }];
        yield return [CityRules.Default with { HighPercent = 50 }];
        yield return [CityRules.Default with { LowPercent = 25, VeryLowPercent = 25 }];
        yield return [CityRules.Default with { DepletionFirstCitizens = 0 }];
    }

    [Fact]
    public void MissingFile_FallsBackToDefaultWithWarning()
    {
        var log = new TestLog();
        CityRules rules = new JsonCityRules(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "city_rules.json"), log).Load();

        Assert.Equal(CityRules.Default, rules);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaultWithWarning()
    {
        var log = new TestLog();
        Assert.Equal(CityRules.Default, Load("{ not json", log));
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void ShippedData_KeepsTheUnityEconomy()
    {
        // The shipped table is the ported balance: a drift from Unity's
        // StoreEconomyConfig asset shows up here instead of in the game.
        var log = new TestLog();
        string file = Path.Combine(
            AppContext.BaseDirectory, "Data", "city_rules.json");
        Assert.True(File.Exists(file), "city_rules.json is not copied to the output");

        Assert.Equal(CityRules.Default, new JsonCityRules(file, log).Load());
        Assert.Empty(log.Warnings);
    }

    private static CityRules Load(string json, TestLog log)
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, json);
            return new JsonCityRules(file, log).Load();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
