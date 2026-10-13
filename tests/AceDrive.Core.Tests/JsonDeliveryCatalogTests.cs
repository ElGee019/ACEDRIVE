using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Delivery catalog loader: valid data in, graceful fallback out
/// (the overlay must never fail to start over a data file).</summary>
public sealed class JsonDeliveryCatalogTests
{
    private const string ValidJson = """
        {
          "WholesaleRate": 0.7,
          "StartCredP": 100,
          "Stores": [
            { "Key": "a", "Name": "Store A", "Owner": "Hahn", "StockedUnits": 12, "UnitCapacity": 40, "PricePerUnit": 2.5, "Citizens": 140, "GrowthPerHour": 1 },
            { "Key": "b", "Name": "Store B", "Owner": "Fuchs", "StockedUnits": 6, "UnitCapacity": 40, "PricePerUnit": 2.2, "Citizens": 45, "GrowthPerHour": -2 }
          ]
        }
        """;

    [Fact]
    public void ValidFile_LoadsStoresAndBalance()
    {
        var catalog = LoadFrom(ValidJson, out TestLog log);
        Assert.Equal(2, catalog.Stores.Count);
        Assert.Equal(0.7, catalog.WholesaleRate);
        Assert.Equal(100, catalog.StartCredP);
        Assert.Equal("Store A", catalog.Stores[0].Name);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void MissingFile_FallsBackToDefaultWithWarning()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var log = new TestLog();
        var catalog = new JsonDeliveryCatalog(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "x.json"), log).Load();
        Assert.Empty(catalog.Stores);
        Assert.Equal(0.7, catalog.WholesaleRate);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaultWithWarning()
    {
        var catalog = LoadFrom("{ not json", out TestLog log);
        Assert.Empty(catalog.Stores);
        Assert.Single(log.Warnings);
    }

    [Theory]
    [InlineData("""{"WholesaleRate":0,"StartCredP":100,"Stores":[{"Key":"a","Name":"A","Owner":"O","StockedUnits":1,"UnitCapacity":40,"PricePerUnit":2,"Citizens":10,"GrowthPerHour":0}]}""")]
    [InlineData("""{"WholesaleRate":0.7,"StartCredP":100,"Stores":[{"Key":"a","Name":"A","Owner":"O","StockedUnits":41,"UnitCapacity":40,"PricePerUnit":2,"Citizens":10,"GrowthPerHour":0}]}""")]
    [InlineData("""{"WholesaleRate":0.7,"StartCredP":100,"Stores":[{"Key":"a","Name":"A","Owner":"O","StockedUnits":1,"UnitCapacity":0,"PricePerUnit":2,"Citizens":10,"GrowthPerHour":0}]}""")]
    [InlineData("""{"WholesaleRate":0.7,"StartCredP":100,"Stores":[{"Key":"a","Name":"A","Owner":"O","StockedUnits":1,"UnitCapacity":40,"PricePerUnit":-1,"Citizens":10,"GrowthPerHour":0},{"Key":"a","Name":"Dup","Owner":"O","StockedUnits":1,"UnitCapacity":40,"PricePerUnit":2,"Citizens":10,"GrowthPerHour":0}]}""")]
    public void InvalidBalanceOrStore_FallsBackToDefault(string json)
    {
        var catalog = LoadFrom(json, out TestLog log);
        Assert.Empty(catalog.Stores);
        Assert.Single(log.Warnings);
    }

    private static DeliveryCatalog LoadFrom(string json, out TestLog log)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        string file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(file, json);
        try
        {
            log = new TestLog();
            return new JsonDeliveryCatalog(file, log).Load();
        }
        finally { File.Delete(file); }
    }
}
