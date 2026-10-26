using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia race balance (Data/cento_race.json): a missing or
/// broken file falls back to Unity's own numbers with one warning, so a bad edit
/// never takes the race down.</summary>
public sealed class JsonCentoRaceRulesStoreTests
{
    [Fact]
    public void MissingFile_FallsBackToTheBuiltInBalance()
    {
        var log = new TestLog();
        var store = new JsonCentoRaceRulesStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "cento_race.json"), log);

        CentoRaceRules rules = store.Load();

        Assert.Equal(CentoRaceRules.Default, rules);
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
                  "placementEarnings": [ 400, 300, 200, 100 ],
                  "starBaseChance": 0.5,
                  "starSteepness": 0.2,
                  "starClassPivot": 10,
                  "starPlacementMultipliers": [ 1.5, 1.0 ],
                  "starPrizePerWin": 250,
                  "awardItemName": "Award {0} - {1}",
                  "certificateItemName": "Certificate {0} {1} - Tier {2}"
                }
                """);
            var log = new TestLog();
            CentoRaceRules rules = new JsonCentoRaceRulesStore(file, log).Load();

            Assert.Equal(400.0, rules.PlacementEarnings[0]);
            Assert.Equal(0.5, rules.StarBaseChance);
            Assert.Equal(250.0, rules.StarPrizePerWin);
            Assert.Equal("Award {0} - {1}", rules.AwardItemName);
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
                { "placementEarnings": [], "starBaseChance": 2.0,
                  "awardItemName": "", "certificateItemName": "" }
                """);
            var log = new TestLog();
            CentoRaceRules rules = new JsonCentoRaceRulesStore(file, log).Load();

            Assert.Equal(CentoRaceRules.Default, rules);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
