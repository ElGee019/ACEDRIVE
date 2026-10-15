using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Hub catalog loader: valid data in, graceful fallback out (the
/// overlay must never fail to start over a data file).</summary>
public sealed class JsonHubCatalogTests
{
    private const string ValidJson = """
        {
          "Tiers": [
            { "Id": 1, "Enabled": true, "Careers": [
                { "Id": "groceries", "Label": "GROCERIES", "Levels": [
                    { "Name": "Grocers and Sons", "Cost": 100 },
                    { "Name": "The Food Hub", "Cost": 150 } ] },
                { "Id": "coffee_beans", "Label": "COFFEE BEANS", "Levels": [
                    { "Name": "CoffeeUs", "Cost": 100 } ] } ] },
            { "Id": 2, "Enabled": false, "Careers": [] }
          ]
        }
        """;

    [Fact]
    public void ValidFile_LoadsTiersAndCareers()
    {
        var catalog = LoadFrom(ValidJson, out TestLog log);
        Assert.Equal(2, catalog.Tiers.Count);
        Assert.True(catalog.Tiers[0].Enabled);
        Assert.False(catalog.Tiers[1].Enabled);
        HubCareer groceries = catalog.Tiers[0].Careers[0];
        Assert.Equal("groceries", groceries.Id);
        Assert.Equal(
            [("Grocers and Sons", 100.0), ("The Food Hub", 150.0)],
            groceries.Levels.Select(l => (l.Name, l.Cost)).ToArray());
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void MissingFile_FallsBackToDefaultWithWarning()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var log = new TestLog();
        var catalog = new JsonHubCatalog(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "x.json"), log).Load();
        Assert.Empty(catalog.Tiers);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaultWithWarning()
    {
        var catalog = LoadFrom("{ not json", out TestLog log);
        Assert.Empty(catalog.Tiers);
        Assert.Single(log.Warnings);
    }

    // Unity tracks hubs by name across the catalog and gates stores by
    // category id, so duplicates, zero prices and empty shells must reject.
    [Theory]
    [InlineData("""{"Tiers":[{"Id":1,"Enabled":true,"Careers":[{"Id":"a","Label":"A","Levels":[{"Name":"X","Cost":100}]},{"Id":"b","Label":"B","Levels":[{"Name":"X","Cost":120}]}]}]}""")]
    [InlineData("""{"Tiers":[{"Id":1,"Enabled":true,"Careers":[{"Id":"a","Label":"A","Levels":[{"Name":"X","Cost":0}]}]}]}""")]
    [InlineData("""{"Tiers":[{"Id":1,"Enabled":true,"Careers":[]}]}""")]
    [InlineData("""{"Tiers":[{"Id":1,"Enabled":true,"Careers":[{"Id":"a","Label":"A","Levels":[]}]}]}""")]
    [InlineData("""{"Tiers":[{"Id":1,"Enabled":true,"Careers":[{"Id":"","Label":"A","Levels":[{"Name":"X","Cost":100}]}]}]}""")]
    [InlineData("""{"Tiers":[{"Id":1,"Enabled":true,"Careers":[{"Id":"a","Label":"","Levels":[{"Name":"X","Cost":100}]}]}]}""")]
    [InlineData("""{"Tiers":[{"Id":1,"Enabled":true,"Careers":[{"Id":"a","Label":"A","Levels":[{"Name":"","Cost":100}]}]}]}""")]
    [InlineData("""{"Tiers":[{"Id":1,"Enabled":true,"Careers":[{"Id":"a","Label":"A","Levels":[{"Name":"X","Cost":100}]}]},{"Id":1,"Enabled":false,"Careers":[]}]}""")]
    public void InvalidContent_FallsBackToDefault(string json)
    {
        var catalog = LoadFrom(json, out TestLog log);
        Assert.Empty(catalog.Tiers);
        Assert.Single(log.Warnings);
    }

    private static HubCatalog LoadFrom(string json, out TestLog log)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        string file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(file, json);
        try
        {
            log = new TestLog();
            return new JsonHubCatalog(file, log).Load();
        }
        finally { File.Delete(file); }
    }
}
