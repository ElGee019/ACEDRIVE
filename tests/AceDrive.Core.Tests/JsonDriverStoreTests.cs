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
                Cornering: 31.0, Concentration: 42.0, OffTheLine: 53.0,
                Assertivity: 3.7));

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
            Assert.Equal(3.7, loaded.Assertivity);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsTheCalendarStamps()
    {
        // The once-per-week stamps ride the driver file as a map, so a restart
        // inside the same week keeps the rows closed (WeeklyEventLedger).
        string file = TempFile();
        try
        {
            var store = new JsonDriverStore(file, new TestLog());
            var ledger = new WeeklyEventLedger();
            ledger.MarkTaken(
                ControlCalendar.RoadTest, new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc));

            store.Save(DriverState.Fresh with { WeeklyEventDates = ledger.Snapshot });
            DriverState? loaded = store.Load();

            Assert.NotNull(loaded);
            Assert.True(
                new WeeklyEventLedger(loaded).TakenOn(
                    ControlCalendar.RoadTest,
                    new DateTime(2026, 10, 13, 20, 0, 0, DateTimeKind.Utc)));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsTheCarePackages()
    {
        // The care-kit shelves and the car ride the driver file (PORTPLAN 21):
        // a restart after a pickup must reopen both halves of every kit.
        string file = TempFile();
        try
        {
            var store = new JsonDriverStore(file, new TestLog());
            var ledger = new CarePackageLedger();
            Assert.True(ledger.Grant("groceries_store"));
            Assert.True(ledger.Grant("groceries_store"));
            Assert.True(ledger.Grant("coffee_shop"));
            Assert.Equal(1, ledger.Pickup("coffee_shop", 1));
            _ = ledger.Defer(1);

            store.Save(DriverState.Fresh with { CarePackages = ledger.Snapshot() });
            DriverState? loaded = store.Load();

            Assert.NotNull(loaded);
            var resumed = new CarePackageLedger(loaded.CarePackages);
            Assert.Equal(1, resumed.Carried);
            Assert.Equal(3, resumed.Total);
            Assert.Equal(1, resumed.Unplaced);
            CarePackagePlacement shelf = Assert.Single(resumed.Stores);
            Assert.Equal("groceries_store", shelf.StoreId);
            Assert.Equal(2, shelf.Count);
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
            CredPRamp.UnityDefault);
        var career = new DeliveryCareer(hubs, 100, TestNames.For());
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
            var resumed = new DeliveryCareer(hubs, loaded.Career!, TestNames.For());
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

    [Fact]
    public void SaveAndLoad_KeepsACashDebt()
    {
        // The crash repair is a charge the player cannot refuse, so
        // DriverWallet.ChargeInFull lets the cash overdraw. A save carrying that
        // debt must survive the next launch: rejecting it reset cash, XP, the
        // discovered streets and the delivery career right after the player had
        // been billed for a crash.
        string file = TempFile();
        try
        {
            var log = new TestLog();
            var store = new JsonDriverStore(file, log);
            store.Save(DriverState.Fresh with
            {
                Cash = -563.37,
                StreetIds = ["home", "s1", "s2"],
            });

            DriverState? loaded = store.Load();

            Assert.NotNull(loaded);
            Assert.Equal(-563.37, loaded.Cash, 2);
            Assert.Equal(["home", "s1", "s2"], loaded.StreetIds);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
}
