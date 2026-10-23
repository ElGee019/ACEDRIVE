using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>pois.json boundary: read, and degrade to the built-in table.</summary>
public sealed class JsonPoiStoreTests
{
    [Fact]
    public void Load_ReadsThePoiTable()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file, """
                [
                  {
                    "id": "max-grocery",
                    "name": "Max's Grocery Store",
                    "type": "grocery",
                    "ownerName": "Max Silvestri",
                    "category": "groceries",
                    "guaranteedItems": ["MAPLE SYRUP"]
                  }
                ]
                """);
            var log = new TestLog();

            IReadOnlyList<PoiDefinition> pois = new JsonPoiStore(file, log).Load();

            PoiDefinition poi = Assert.Single(pois);
            Assert.Equal("max-grocery", poi.Id);
            Assert.Equal("Max's Grocery Store", poi.Name);
            Assert.Equal(PoiTypes.Grocery, poi.Type);
            Assert.Equal("Max Silvestri", poi.OwnerName);
            Assert.Equal("groceries", poi.Category);
            Assert.Equal(["MAPLE SYRUP"], poi.GuaranteedItems);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Load_OldRowWithoutType_DefaultsToGrocery()
    {
        string file = TempFile();
        try
        {
            // A pre-typed pois.json must keep working: the record defaults
            // make the row a grocery with no owner/category.
            File.WriteAllText(file, """
                [
                  { "id": "max-grocery", "name": "Max's Grocery Store" }
                ]
                """);
            var log = new TestLog();

            PoiDefinition poi = Assert.Single(new JsonPoiStore(file, log).Load());

            Assert.Equal(PoiTypes.Grocery, poi.Type);
            Assert.Equal("", poi.OwnerName);
            Assert.Equal("", poi.Category);
            Assert.Null(poi.GuaranteedItems);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Load_MissingFile_FallsBackWithWarning()
    {
        var log = new TestLog();

        IReadOnlyList<PoiDefinition> pois = new JsonPoiStore(TempFile(), log).Load();

        Assert.Equal(
            PoiCatalog.Default.Select(poi => poi.Id),
            pois.Select(poi => poi.Id));
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void Load_EmptyFile_FallsBackWithWarning()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file, "[]");
            var log = new TestLog();

            IReadOnlyList<PoiDefinition> pois = new JsonPoiStore(file, log).Load();

            Assert.Equal(
                PoiCatalog.Default.Select(poi => poi.Id),
                pois.Select(poi => poi.Id));
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

    [Fact]
    public void ShippedTable_CarriesTheUnitySpawnRules()
    {
        // Data/pois.json is the live content: the business doors open at
        // Unity's street/job thresholds, and a new career starts without one.
        string file = Path.Combine(AppContext.BaseDirectory, "Data", "pois.json");
        IReadOnlyList<PoiDefinition> table = new JsonPoiStore(file, new TestLog()).Load();

        Assert.Contains(
            table,
            poi => poi.Id == PoiCatalog.MaxGroceryId && poi.Spawn is null);
        AssertCarwash(Assert.Single(table, poi => poi.Type == PoiTypes.CarWash));
        AssertInsurance(Assert.Single(table, poi => poi.Type == PoiTypes.Insurance));
        AssertBank(Assert.Single(table, poi => poi.Type == PoiTypes.Bank));
        AssertDealer(Assert.Single(table, poi => poi.Type == PoiTypes.Dealer));
        AssertMechanic(Assert.Single(table, poi => poi.Type == PoiTypes.Mechanic));
        AssertStore(
            Assert.Single(table, poi => poi.Type == PoiTypes.GroceryStore),
            "groceries", 20);
        AssertStore(
            Assert.Single(table, poi => poi.Type == PoiTypes.CoffeeShop),
            "coffee_beans", 14);
    }

    [Fact]
    public void Load_InvalidSpawnRule_FallsBackToTheBuiltInTable()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file, """
                [
                  {
                    "id": "bank",
                    "name": "Bank",
                    "type": "bank",
                    "namePattern": "{0}'s Bank",
                    "spawn": { "pacing": "streets", "firstStreets": 15 }
                  }
                ]
                """);
            var log = new TestLog();

            IReadOnlyList<PoiDefinition> pois = new JsonPoiStore(file, log).Load();

            Assert.Equal(
                PoiCatalog.Default.Select(poi => poi.Id),
                pois.Select(poi => poi.Id));
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static void AssertCarwash(PoiDefinition poi)
    {
        Assert.Equal("{0}'s Carwash", poi.NamePattern);
        Assert.Equal(PoiPacing.Streets, poi.Spawn!.Pacing);
        Assert.Equal(6, poi.Spawn.FirstStreets);
        Assert.Equal(47, poi.Spawn.IntervalStreets);
    }

    private static void AssertInsurance(PoiDefinition poi)
    {
        Assert.Equal(PoiPacing.Streets, poi.Spawn!.Pacing);
        Assert.Equal(13, poi.Spawn.FirstStreets);
        Assert.Equal(57, poi.Spawn.IntervalStreets);
    }

    private static void AssertBank(PoiDefinition poi)
    {
        Assert.Equal(PoiPacing.Streets, poi.Spawn!.Pacing);
        Assert.Equal(15, poi.Spawn.FirstStreets);
        Assert.Equal(51, poi.Spawn.IntervalStreets);
    }

    private static void AssertDealer(PoiDefinition poi)
    {
        Assert.Equal(PoiPacing.Either, poi.Spawn!.Pacing);
        Assert.Equal(37, poi.Spawn.FirstStreets);
        Assert.Equal(37, poi.Spawn.IntervalStreets);
        Assert.Equal(10, poi.Spawn.FirstJobs);
    }

    private static void AssertMechanic(PoiDefinition poi)
    {
        Assert.Equal(PoiPacing.JobsFirstThenStreets, poi.Spawn!.Pacing);
        Assert.Equal(3, poi.Spawn.FirstStreets);
        Assert.Equal(36, poi.Spawn.IntervalStreets);
        Assert.Equal(36, poi.Spawn.FirstJobs);
    }

    private static void AssertStore(
        PoiDefinition poi, string category, int interval)
    {
        Assert.Equal(category, poi.Category);
        Assert.Equal(PoiPacing.OwnedCategory, poi.Spawn!.Pacing);
        Assert.Equal(interval, poi.Spawn.IntervalStreets);
    }
}
