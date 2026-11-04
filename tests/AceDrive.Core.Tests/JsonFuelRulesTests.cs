using System.Text.Json;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>The pump's rules loader: Unity's shipped numbers in data form, or
/// the shipped fallback — never an exception, the overlay must start over any
/// data file.</summary>
public sealed class JsonFuelRulesTests
{
    private const string ValidJson = """
        {
          "StartPricePerLitre": 1.70,
          "DailyChangeMin": -0.05,
          "DailyChangeMax": 0.10,
          "MinPricePerLitre": 1.30,
          "MaxPricePerLitre": 2.50,
          "MinFillLitres": 1.0,
          "SettleSeconds": 2.0,
          "OwnerName": "Anke Vogt"
        }
        """;

    [Fact]
    public void ValidFile_LoadsEveryRule()
    {
        var log = new TestLog();
        FuelRules rules = Load(ValidJson, log);

        Assert.Equal(1.70, rules.StartPricePerLitre);
        Assert.Equal(-0.05, rules.DailyChangeMin);
        Assert.Equal(0.10, rules.DailyChangeMax);
        Assert.Equal(1.30, rules.MinPricePerLitre);
        Assert.Equal(2.50, rules.MaxPricePerLitre);
        Assert.Equal(1.0, rules.MinFillLitres);
        Assert.Equal(2.0, rules.SettleSeconds);
        Assert.Equal("Anke Vogt", rules.OwnerName);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void MissingFile_FallsBackToUnitysDefaultsWithWarning()
    {
        var log = new TestLog();
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString(), "fuel_rules.json");

        Assert.Same(FuelRules.Default, new JsonFuelRules(file, log).Load());
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void CorruptFile_FallsBackToUnitysDefaultsWithWarning()
    {
        var log = new TestLog();
        Assert.Same(FuelRules.Default, Load("{ not json", log));
        Assert.Single(log.Warnings);
    }

    [Theory]
    [InlineData("start-below-band")]
    [InlineData("start-above-band")]
    [InlineData("band-inverted")]
    [InlineData("band-zero")]
    [InlineData("window-inverted")]
    [InlineData("min-fill-zero")]
    [InlineData("settle-zero")]
    [InlineData("owner-missing")]
    public void InvalidRules_FallBackToUnityDefaults(string which)
    {
        // Each case breaks exactly one guard, so the guard under test is the
        // one that catches it (a partial table would fail many checks at once).
        var log = new TestLog();
        FuelRules rules = which switch
        {
            "start-below-band" => FuelRules.Default with { StartPricePerLitre = 1.0 },
            "start-above-band" => FuelRules.Default with { StartPricePerLitre = 3.0 },
            "band-inverted" => FuelRules.Default with { MinPricePerLitre = 2.5, MaxPricePerLitre = 1.5 },
            "band-zero" => FuelRules.Default with { MinPricePerLitre = 0.0 },
            "window-inverted" => FuelRules.Default with { DailyChangeMin = 0.2, DailyChangeMax = -0.2 },
            "min-fill-zero" => FuelRules.Default with { MinFillLitres = 0.0 },
            "settle-zero" => FuelRules.Default with { SettleSeconds = 0.0 },
            "owner-missing" => FuelRules.Default with { OwnerName = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(which)),
        };

        Assert.Same(FuelRules.Default, Load(JsonSerializer.Serialize(rules), log));
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void ShippedData_KeepsUnitysNumbers()
    {
        // The shipped table is the ported balance: a drift from Unity's
        // PlayerDefaults/GameTimeConfigSO shows up here, not in-game.
        var log = new TestLog();
        string file = Path.Combine(AppContext.BaseDirectory, "Data", "fuel_rules.json");
        Assert.True(File.Exists(file), "fuel_rules.json is not copied to the output");

        FuelRules rules = new JsonFuelRules(file, log).Load();

        Assert.Empty(log.Warnings);
        Assert.Equal(1.65, rules.StartPricePerLitre);
        Assert.Equal(-0.15, rules.DailyChangeMin);
        Assert.Equal(0.15, rules.DailyChangeMax);
        Assert.Equal(1.40, rules.MinPricePerLitre);
        Assert.Equal(2.20, rules.MaxPricePerLitre);
    }

    private static FuelRules Load(string json, TestLog log)
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, json);
            return new JsonFuelRules(file, log).Load();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
