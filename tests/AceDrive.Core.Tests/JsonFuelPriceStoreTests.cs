using System.Text.Json;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>The fuel-market file (PORTPLAN item 37): a missing file is a first
/// run, a corrupt one degrades to the shipped seed, and Save keeps the one read
/// the session paid in step with what was banked.</summary>
public sealed class JsonFuelPriceStoreTests
{
    [Fact]
    public void AMissingFile_IsAFirstRun()
    {
        var log = new TestLog();
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString(), "fuel.json");

        Assert.Null(new JsonFuelPriceStore(file, log).Load());
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void ABankedPrice_ResumesOntheNextSession()
    {
        var log = new TestLog();
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString(), "fuel.json");
        try
        {
            new JsonFuelPriceStore(file, log).Save(new FuelPriceState(1.87, 42));

            FuelPriceState? resumed = new JsonFuelPriceStore(file, log).Load();

            Assert.Equal(1.87, resumed!.PricePerLitre, 10);
            Assert.Equal(42, resumed.LastRollDay);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_SeesTheNewPriceWithoutASecondRead()
    {
        // The cache is the point: after a roll, Load must answer with what Save
        // just wrote, not with the file the session opened on.
        var log = new TestLog();
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString(), "fuel.json");
        try
        {
            var store = new JsonFuelPriceStore(file, log);
            Assert.Null(store.Load());
            store.Save(new FuelPriceState(1.72, 43));

            Assert.Equal(1.72, store.Load()!.PricePerLitre, 10);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
        }
    }

    [Fact]
    public void ACorruptFile_DegradesToTheShippedSeedWithWarning()
    {
        var log = new TestLog();
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");

            Assert.Null(new JsonFuelPriceStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ANonPositivePrice_DegradesToTheShippedSeedWithWarning()
    {
        var log = new TestLog();
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, JsonSerializer.Serialize(new FuelPriceState(-2.0, 42)));

            Assert.Null(new JsonFuelPriceStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
