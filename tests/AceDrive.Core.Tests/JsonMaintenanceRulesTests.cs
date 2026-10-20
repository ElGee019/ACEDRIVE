using System.Text.Json;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Condition rules loader: Unity's shipped tables in data form (or the
/// shipped fallback) — never an exception, the overlay must start over any data
/// file.</summary>
public sealed class JsonMaintenanceRulesTests
{
    private const string ValidJson = """
        {
          "Parts": [
            { "Key": "oil_change", "IntervalKm": 1200, "FullPrice": 250 }
          ],
          "ClassBands": [
            { "MinClass": 10, "Multiplier": 0.8 },
            { "MinClass": 0, "Multiplier": 0.4 }
          ],
          "EngineBands": [
            { "MaxCc": 2147483647, "RedKm": 123456 }
          ],
          "CurveExponent": 3.0,
          "MaxBreakdownChance": 0.5,
          "BreakdownFractionMin": 0.2,
          "BreakdownFractionMax": 0.8,
          "PickupFee": 77.0,
          "EngineRepairFraction": 0.11,
          "NewEngineFraction": 0.31,
          "WeeklyIntervalDays": 5,
          "YearIntervalDays": 300,
          "YearJitterDays": 3
        }
        """;

    [Fact]
    public void ValidFile_LoadsEveryRule()
    {
        var log = new TestLog();
        MaintenanceRules rules = Load(ValidJson, log);

        Assert.Single(rules.Parts);
        Assert.Equal("oil_change", rules.Parts[0].Key);
        Assert.Equal(1200.0, rules.Parts[0].IntervalKm);
        Assert.Equal(250.0, rules.Parts[0].FullPrice);
        Assert.Equal(0.4, rules.MultiplierFor(0));
        Assert.Equal(0.8, rules.MultiplierFor(10));
        Assert.Equal(123456.0, rules.RedKmFor(6000));
        Assert.Equal(3.0, rules.CurveExponent);
        Assert.Equal(0.5, rules.MaxBreakdownChance);
        Assert.Equal(77.0, rules.PickupFee);
        Assert.Equal(5, rules.WeeklyIntervalDays);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void MissingFile_FallsBackToDefaultWithWarning()
    {
        var log = new TestLog();
        MaintenanceRules rules = new JsonMaintenanceRules(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "maintenance_rules.json"), log).Load();

        Assert.Same(MaintenanceRules.Default, rules);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaultWithWarning()
    {
        var log = new TestLog();
        Assert.Same(MaintenanceRules.Default, Load("{ not json", log));
        Assert.Single(log.Warnings);
    }

    [Theory]
    [InlineData("parts-empty")]
    [InlineData("part-bad")]
    [InlineData("class-bands-empty")]
    [InlineData("class-bands-lowest-above-zero")]
    [InlineData("class-bands-not-descending")]
    [InlineData("class-multiplier-out-of-range")]
    [InlineData("engine-bands-empty")]
    [InlineData("engine-bands-not-ascending")]
    [InlineData("engine-red-km-negative")]
    [InlineData("curve-zero")]
    [InlineData("cap-zero")]
    [InlineData("cap-above-one")]
    [InlineData("fraction-window-inverted")]
    [InlineData("pickup-negative")]
    [InlineData("repair-fraction-zero")]
    [InlineData("new-engine-fraction-above-one")]
    [InlineData("weekly-zero")]
    [InlineData("year-zero")]
    [InlineData("jitter-negative")]
    public void InvalidRules_FallBackToUnityDefaults(string which)
    {
        // Each case breaks exactly one guard, so the guard under test is the
        // one that catches it (a partial table would fail many checks at once).
        var log = new TestLog();
        MaintenanceRules rules = which switch
        {
            "parts-empty" => MaintenanceRules.Default with { Parts = [] },
            "part-bad" => MaintenanceRules.Default with { Parts = [new MaintenancePart("", 0.0, 0.0)] },
            "class-bands-empty" => MaintenanceRules.Default with { ClassBands = [] },
            "class-bands-lowest-above-zero" => MaintenanceRules.Default with { ClassBands = [new ClassPriceBand(5, 0.5)] },
            "class-bands-not-descending" => MaintenanceRules.Default with
            {
                ClassBands = [new ClassPriceBand(0, 0.4), new ClassPriceBand(16, 1.0)],
            },
            "class-multiplier-out-of-range" => MaintenanceRules.Default with { ClassBands = [new ClassPriceBand(0, 1.5)] },
            "engine-bands-empty" => MaintenanceRules.Default with { EngineBands = [] },
            "engine-bands-not-ascending" => MaintenanceRules.Default with
            {
                EngineBands = [new EngineBand(2000, 300000.0), new EngineBand(1000, 250000.0)],
            },
            "engine-red-km-negative" => MaintenanceRules.Default with { EngineBands = [new EngineBand(int.MaxValue, -1.0)] },
            "curve-zero" => MaintenanceRules.Default with { CurveExponent = 0.0 },
            "cap-zero" => MaintenanceRules.Default with { MaxBreakdownChance = 0.0 },
            "cap-above-one" => MaintenanceRules.Default with { MaxBreakdownChance = 1.5 },
            "fraction-window-inverted" => MaintenanceRules.Default with { BreakdownFractionMin = 0.9, BreakdownFractionMax = 0.1 },
            "pickup-negative" => MaintenanceRules.Default with { PickupFee = -1.0 },
            "repair-fraction-zero" => MaintenanceRules.Default with { EngineRepairFraction = 0.0 },
            "new-engine-fraction-above-one" => MaintenanceRules.Default with { NewEngineFraction = 1.5 },
            "weekly-zero" => MaintenanceRules.Default with { WeeklyIntervalDays = 0 },
            "year-zero" => MaintenanceRules.Default with { YearIntervalDays = 0 },
            "jitter-negative" => MaintenanceRules.Default with { YearJitterDays = -1 },
            _ => throw new ArgumentOutOfRangeException(nameof(which)),
        };

        Assert.Same(MaintenanceRules.Default, Load(JsonSerializer.Serialize(rules), log));
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void ShippedData_KeepsTheUnityTables()
    {
        // The shipped table is the ported balance: a drift from Unity's
        // CarMaintenanceService/EngineHealthService shows up here, not in-game.
        var log = new TestLog();
        string file = Path.Combine(AppContext.BaseDirectory, "Data", "maintenance_rules.json");
        Assert.True(File.Exists(file), "maintenance_rules.json is not copied to the output");

        MaintenanceRules rules = new JsonMaintenanceRules(file, log).Load();
        Assert.Empty(log.Warnings);
        Assert.Equal(8, rules.Parts.Count);
        Assert.Equal(0.10, rules.MaxBreakdownChance);
        Assert.Equal(100.0, rules.PickupFee);
        Assert.Equal(300000.0, rules.RedKmFor(1460));
        Assert.Equal(1.0, rules.MultiplierFor(16));
        Assert.Equal(0.4, rules.MultiplierFor(6));
    }

    private static MaintenanceRules Load(string json, TestLog log)
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, json);
            return new JsonMaintenanceRules(file, log).Load();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
