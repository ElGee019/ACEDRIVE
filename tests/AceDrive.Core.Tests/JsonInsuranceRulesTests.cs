using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Insurance rules loader: Unity's shipped config in data form (or the
/// shipped fallback) — never an exception.</summary>
public sealed class JsonInsuranceRulesTests
{
    [Fact]
    public void ValidFile_LoadsEveryRule()
    {
        var log = new TestLog();
        InsuranceRules rules = Load("""
            { "PremiumRate": 0.002, "BaseIncreaseFactor": 1.5, "MinimumDailyCost": 3.0, "ChargeHour": 2 }
            """, log);

        Assert.Equal(new InsuranceRules(0.002, 1.5, 3.0, 2), rules);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void MissingFile_FallsBackToDefaultWithWarning()
    {
        var log = new TestLog();
        InsuranceRules rules = new JsonInsuranceRules(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "insurance_rules.json"), log).Load();

        Assert.Same(InsuranceRules.Default, rules);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaultWithWarning()
    {
        var log = new TestLog();
        Assert.Same(InsuranceRules.Default, Load("{ not json", log));
        Assert.Single(log.Warnings);
    }

    [Theory]
    [InlineData(0.0, 1.15, 1.0, 1)]    // free rate
    [InlineData(-0.001, 1.15, 1.0, 1)] // negative rate
    [InlineData(0.0005, 0.9, 1.0, 1)]  // increase below 1 breaks the "+15%" copy
    [InlineData(0.0005, 1.15, -1.0, 1)] // negative floor
    [InlineData(0.0005, 1.15, 1.0, -1)] // hour before the day
    [InlineData(0.0005, 1.15, 1.0, 24)] // hour past the day
    public void InvalidRules_FallBackToUnityDefaults(double rate, double increase, double floor, int hour)
    {
        var log = new TestLog();
        Assert.Same(InsuranceRules.Default, Load(
            $$"""{ "PremiumRate": {{rate}}, "BaseIncreaseFactor": {{increase}}, "MinimumDailyCost": {{floor}}, "ChargeHour": {{hour}} }""",
            log));
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void ShippedData_KeepsTheUnityConfig()
    {
        var log = new TestLog();
        string file = Path.Combine(AppContext.BaseDirectory, "Data", "insurance_rules.json");
        Assert.True(File.Exists(file), "insurance_rules.json is not copied to the output");

        Assert.Equal(InsuranceRules.Default, new JsonInsuranceRules(file, log).Load());
        Assert.Empty(log.Warnings);
    }

    private static InsuranceRules Load(string json, TestLog log)
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, json);
            return new JsonInsuranceRules(file, log).Load();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
