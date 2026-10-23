using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Data-file degradation for the driving-slice rules: missing or
/// invalid Data/taxi_rules.json falls back to the built-in defaults.</summary>
public sealed class JsonTaxiRulesStoreTests
{
    private const string ValidJson = """
        {"ArrivalRadiusMeters":30,"MaxMeterSpeedKmh":350,
         "DisplayDistanceStepMeters":5,"NoticeSeconds":2.5}
        """;

    [Fact]
    public void MissingFile_FallsBackWithWarning()
    {
        var log = new TestLog();
        var store = new JsonTaxiRulesStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "taxi_rules.json"), log);

        Assert.Equal(TaxiRules.Default, store.Load());
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
            Assert.Equal(TaxiRules.Default, new JsonTaxiRulesStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void NegativeValues_FallBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                {"ArrivalRadiusMeters":-1,"MaxMeterSpeedKmh":400,
                 "DisplayDistanceStepMeters":10,"NoticeSeconds":3}
                """);
            var log = new TestLog();
            Assert.Equal(TaxiRules.Default, new JsonTaxiRulesStore(file, log).Load());
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

            TaxiRules rules = new JsonTaxiRulesStore(file, log).Load();

            Assert.Equal(new TaxiRules(30.0, 350.0, 5.0, 2.5), rules);
            Assert.Equal(SpeedChallengeRules.Default, rules.SpeedChallenge);
            // A file written before the request page existed keeps the delay.
            Assert.Equal(5.0, rules.RequestDelaySeconds);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void RequestDelayBlock_LoadsItsSeconds()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                {"ArrivalRadiusMeters":30,"MaxMeterSpeedKmh":350,
                 "DisplayDistanceStepMeters":5,"NoticeSeconds":2.5,
                 "RequestDelaySeconds":2}
                """);
            var log = new TestLog();

            TaxiRules rules = new JsonTaxiRulesStore(file, log).Load();

            Assert.Equal(2.0, rules.RequestDelaySeconds);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void SpeedChallengeBlock_LoadsItsKnobs()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                {"ArrivalRadiusMeters":30,"MaxMeterSpeedKmh":350,
                 "DisplayDistanceStepMeters":5,"NoticeSeconds":2.5,
                 "SpeedChallenge":{"TopSpeedFraction":0.6,"MinAverageKmh":45,
                  "MaxAverageKmh":120,"SlackFactor":1.4,
                  "BufferSeconds":15}}
                """);
            var log = new TestLog();

            TaxiRules rules = new JsonTaxiRulesStore(file, log).Load();

            Assert.Equal(
                new SpeedChallengeRules(0.6, 45.0, 120.0, 1.4, 15),
                rules.SpeedChallenge);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void BrokenSpeedChallengeBlock_FallsBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                {"ArrivalRadiusMeters":30,"MaxMeterSpeedKmh":350,
                 "DisplayDistanceStepMeters":5,"NoticeSeconds":2.5,
                 "SpeedChallenge":{"TopSpeedFraction":0,"MinAverageKmh":45,
                  "MaxAverageKmh":120,"SlackFactor":1.4,
                  "BufferSeconds":15}}
                """);
            var log = new TestLog();

            Assert.Equal(TaxiRules.Default, new JsonTaxiRulesStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
