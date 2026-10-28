using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>driver.json boundary: the trunk rides the same save as cash/xp.</summary>
public sealed class JsonDriverStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsTheTrunk()
    {
        string file = TempFile();
        try
        {
            var log = new TestLog();
            var store = new JsonDriverStore(file, log);
            store.Save(new DriverState(
                123.45, 0.0,
                Trunk:
                [
                    new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 2 },
                ],
                Cornering: 31.0, Concentration: 42.0, OffTheLine: 53.0));

            DriverState? loaded = store.Load();

            Assert.NotNull(loaded);
            Assert.Equal(123.45, loaded.Cash, 2);
            TrunkItem item = Assert.Single(loaded.Trunk!);
            Assert.Equal("MAPLE SYRUP", item.Name);
            Assert.Equal(0.4, item.UnitVolume, 6);
            Assert.Equal(2, item.Quantity);
            Assert.Equal(31.0, loaded.Cornering);
            Assert.Equal(42.0, loaded.Concentration);
            Assert.Equal(53.0, loaded.OffTheLine);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Load_PreRatingSave_OpensOnUnitysNewGameProfile()
    {
        // A driver.json written before PORTPLAN 06 has no skill fields; the
        // record defaults carry Unity's new-game overall, so the wallet page
        // opens on 20/20/20 instead of three zeros.
        string file = TempFile();
        try
        {
            File.WriteAllText(file, """{"Cash":10,"Xp":2}""");
            DriverState? loaded = new JsonDriverStore(file, new TestLog()).Load();

            Assert.NotNull(loaded);
            Assert.Equal(DriverRating.NewGameOverall, loaded.Cornering);
            Assert.Equal(DriverRating.NewGameOverall, loaded.Concentration);
            Assert.Equal(DriverRating.NewGameOverall, loaded.OffTheLine);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsTheDeliveryCareer()
    {
        // The career rides the driver file (this port's PlayerData): the bought
        // hub, the CredP left and the ramp count must all come back, or a
        // restart loses the career the player paid for.
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES",
                    [new HubLevel("Grocers and Sons", 100)])])],
            CredPRamp.UnityDefault, ["Elena Marchetti"]);
        var career = new DeliveryCareer(hubs, 100);
        Assert.True(career.TryOpenCareer(1, 0, 0));
        Assert.Equal(1, career.RegisterDelivery("groceries").CredPEarned);

        string file = TempFile();
        try
        {
            var log = new TestLog();
            var store = new JsonDriverStore(file, log);
            store.Save(DriverState.Fresh with { Career = career.Snapshot() });

            DriverState? loaded = store.Load();

            Assert.NotNull(loaded?.Career);
            var resumed = new DeliveryCareer(hubs, loaded.Career!);
            Assert.Equal(career.CredP, resumed.CredP);
            OwnedHub hub = Assert.Single(resumed.Owned);
            Assert.Equal("Grocers and Sons", hub.Name);
            Assert.Equal("groceries_grocers_and_sons", hub.Key);
            // The second delivery after the restart keeps the ramp moving.
            Assert.Equal(2, resumed.RegisterDelivery("groceries").CredPEarned);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
}
