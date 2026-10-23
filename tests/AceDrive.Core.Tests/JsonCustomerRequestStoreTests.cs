using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>customer_requests.json degradation and the shipped content shape:
/// missing/invalid files mean no requests, and the real data covers every
/// suffix the name pool can draw plus all six quiz categories.</summary>
public sealed class JsonCustomerRequestStoreTests
{
    private const string ValidJson = """
        {
          "timerSeconds": 14,
          "tier2Chance": 0.2,
          "tier3Chance": 0.2,
          "speedChallengeText": "Quickly!",
          "suffixRequests": { "and some friends": "[name] are here." },
          "questionCategories": [
            {
              "key": "weight",
              "stat": "MassKg",
              "unitKey": "QuizUnitKg",
              "bands": [
                { "tier": "medium", "variants": ["How heavy is this car?"] }
              ]
            }
          ]
        }
        """;

    private static string Write(string json)
    {
        string file = Path.GetTempFileName();
        File.WriteAllText(file, json);
        return file;
    }

    [Fact]
    public void MissingFile_MeansNoRequestsWithAWarning()
    {
        var log = new TestLog();
        var store = new JsonCustomerRequestStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "customer_requests.json"), log);

        Assert.Equal(CustomerRequestTable.Empty, store.Load());
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void InvalidJson_MeansNoRequestsWithAWarning()
    {
        string file = Write("{ not json");
        try
        {
            var log = new TestLog();
            Assert.Equal(CustomerRequestTable.Empty, new JsonCustomerRequestStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ValidFile_LoadsTheTable()
    {
        string file = Write(ValidJson);
        try
        {
            var log = new TestLog();
            CustomerRequestTable table = new JsonCustomerRequestStore(file, log).Load();

            Assert.Empty(log.Warnings);
            Assert.Equal(14, table.TimerSeconds);
            Assert.Equal(0.2, table.Tier2Chance);
            Assert.Equal(0.2, table.Tier3Chance);
            Assert.Equal("Quickly!", table.SpeedChallengeText);
            Assert.Equal("[name] are here.", table.SuffixRequests["and some friends"]);
            CarStatCategory category = Assert.Single(table.QuestionCategories);
            Assert.Equal("weight", category.Key);
            Assert.Equal(CarStat.MassKg, category.Stat);
            Assert.Equal("QuizUnitKg", category.UnitKey);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void DuplicateCategoryStat_MeansNoRequests()
    {
        // A missing "stat" deserializes to the enum default (MassKg): the
        // duplicate would make two categories ask the same question.
        string file = Write("""
            {
              "timerSeconds": 14, "tier2Chance": 0.2, "tier3Chance": 0.2,
              "speedChallengeText": "Quickly!",
              "suffixRequests": { "and some friends": "text" },
              "questionCategories": [
                { "key": "weight", "stat": "MassKg", "unitKey": "QuizUnitKg",
                  "bands": [ { "tier": "medium", "variants": ["A?"] } ] },
                { "key": "weight2", "unitKey": "QuizUnitKg",
                  "bands": [ { "tier": "medium", "variants": ["B?"] } ] }
              ]
            }
            """);
        try
        {
            var log = new TestLog();
            Assert.Equal(CustomerRequestTable.Empty, new JsonCustomerRequestStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void OutOfRangeChance_MeansNoRequests()
    {
        string file = Write(ValidJson.Replace("\"tier2Chance\": 0.2", "\"tier2Chance\": 1.2", StringComparison.Ordinal));
        try
        {
            var log = new TestLog();
            Assert.Equal(CustomerRequestTable.Empty, new JsonCustomerRequestStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void RealDataFile_CoversEverySuffixAndAllSixCategories()
    {
        string data = Path.Combine(AppContext.BaseDirectory, "Data");
        var log = new TestLog();
        CustomerRequestTable table =
            new JsonCustomerRequestStore(Path.Combine(data, "customer_requests.json"), log).Load();
        NamePoolTable pools =
            new JsonNamePoolStore(Path.Combine(data, "name_pools.json"), log).Load();

        Assert.Empty(log.Warnings);
        Assert.Equal(19, table.TimerSeconds);
        Assert.Equal(49, table.SuffixRequests.Count);
        Assert.Equal(6, table.QuestionCategories.Count);
        Assert.Equal(6, table.QuestionCategories.Select(c => c.Stat).Distinct().Count());
        Assert.All(table.QuestionCategories, category => Assert.NotEmpty(category.Bands));

        foreach (string suffix in table.SuffixRequests.Keys)
        {
            Assert.Contains(suffix, pools.Suffixes);
        }
    }
}
