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
                ]));

            DriverState? loaded = store.Load();

            Assert.NotNull(loaded);
            Assert.Equal(123.45, loaded.Cash, 2);
            TrunkItem item = Assert.Single(loaded.Trunk!);
            Assert.Equal("MAPLE SYRUP", item.Name);
            Assert.Equal(0.4, item.UnitVolume, 6);
            Assert.Equal(2, item.Quantity);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
}
