using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>The road test's issue balance (Data/road_test.json): a missing or
/// broken file falls back to Unity's own numbers with one warning, so a bad edit
/// never stops the test from settling.</summary>
public sealed class JsonRoadTestRulesStoreTests
{
    [Fact]
    public void MissingFile_FallsBackToTheBuiltInBalance()
    {
        var log = new TestLog();
        var store = new JsonRoadTestRulesStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "road_test.json"), log);

        RoadTestAudienceRules rules = store.Load();

        Assert.Equal(RoadTestAudienceRules.Default, rules);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void ValidContent_ReplacesTheBalance()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                {
                  "baseForfeit": 600,
                  "magazinePriceDivisor": 4.0,
                  "commissionPercent": 20.0
                }
                """);
            var log = new TestLog();
            RoadTestAudienceRules rules = new JsonRoadTestRulesStore(file, log).Load();

            Assert.Equal(600, rules.BaseForfeit);
            Assert.Equal(4.0, rules.MagazinePriceDivisor);
            Assert.Equal(20.0, rules.CommissionPercent);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void InvalidContent_FallsBackWithAWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                { "baseForfeit": -1, "magazinePriceDivisor": 0.0, "commissionPercent": 10.0 }
                """);
            var log = new TestLog();
            RoadTestAudienceRules rules = new JsonRoadTestRulesStore(file, log).Load();

            Assert.Equal(RoadTestAudienceRules.Default, rules);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
