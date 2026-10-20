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

        Assert.Equal("max-grocery", Assert.Single(pois).Id);
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

            Assert.Equal("max-grocery", Assert.Single(pois).Id);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
}
