using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Cento Miglia tier content (Data/cento_miglia.json): a missing or
/// broken file leaves the page without tiers, and rows that cannot be shown are
/// dropped instead of taking the table down.</summary>
public sealed class JsonCentoTierStoreTests
{
    [Fact]
    public void MissingFile_LoadsNoTiersWithAWarning()
    {
        var log = new TestLog();
        var store = new JsonCentoTierStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "cento_miglia.json"), log);
        Assert.Empty(store.Load());
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void InvalidContent_LoadsNoTiersWithAWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();
            Assert.Empty(new JsonCentoTierStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void UnusableRows_AreDroppedAndTheOrderSurvives()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                [
                  {"id":"gentlemen_drivers","label":"Gentlemen Drivers Club","description":"Free for all.","reputationRequired":1},
                  {"id":"","label":"No id","description":"x","reputationRequired":2},
                  {"id":"event3","label":"","description":"No label","reputationRequired":4},
                  {"id":"event4","label":"Country Club","description":"x","reputationRequired":-1},
                  {"id":"gentlemen_drivers","label":"Duplicate","description":"x","reputationRequired":8},
                  {"id":"event6","label":"Sports","description":"Description for Event 6","reputationRequired":32}
                ]
                """);
            var log = new TestLog();
            IReadOnlyList<CentoTier> tiers = new JsonCentoTierStore(file, log).Load();

            // The id-less, label-less, negative-level and duplicate rows are out;
            // the rest keep the file's order.
            Assert.Equal(
                ["gentlemen_drivers", "event6"], tiers.Select(tier => tier.Id));
            Assert.Equal(32, tiers[1].ReputationRequired);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
