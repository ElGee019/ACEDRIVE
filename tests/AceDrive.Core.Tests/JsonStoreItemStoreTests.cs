using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>store_items.json boundary: the item table + layout, and the fallback.</summary>
public sealed class JsonStoreItemStoreTests
{
    [Fact]
    public void Load_ReadsTheCatalogAndStoreLayout()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file, """
                {
                  "DefaultStock": 20,
                  "Layouts": {
                    "groceries": { "groceries": 8, "coffee_beans": 2 }
                  },
                  "Items": [
                    { "Name": "MAPLE SYRUP", "Price": 45.99, "Volume": 0.4, "Hunger": 3, "Category": "groceries" },
                    { "Name": "BANANA", "Price": 2.0, "Volume": 0.3, "Hunger": 4, "Category": "groceries" },
                    { "Name": "Kenya AA Nyeri Coffee", "Price": 20.0, "Volume": 0.5, "Hunger": 1, "Category": "coffee_beans" }
                  ]
                }
                """);
            var log = new TestLog();

            StoreCatalog catalog = new JsonStoreItemStore(file, log).Load();

            Assert.Equal(20, catalog.DefaultStock);
            Assert.Equal(3, catalog.Items.Count);
            Assert.NotNull(catalog.Layouts);
            Assert.Equal(8, catalog.Layouts!["groceries"]["groceries"]);
            Assert.Equal(2, catalog.Layouts!["groceries"]["coffee_beans"]);
            Assert.Equal(45.99, catalog.Find("maple syrup")!.Price, 2);
            Assert.Equal(2, catalog.ByCategory("groceries").Count);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Load_MissingFile_FallsBackWithWarning()
    {
        var log = new TestLog();

        StoreCatalog catalog = new JsonStoreItemStore(TempFile(), log).Load();

        Assert.Contains(catalog.Items, item => item.Name == "MAPLE SYRUP");
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void Load_InvalidCatalog_FallsBackWithWarning()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file, """
                {
                  "DefaultStock": 0,
                  "Items": [
                    { "Name": "MAPLE SYRUP", "Price": 45.99, "Volume": 0.4, "Hunger": 3, "Category": "groceries" }
                  ]
                }
                """);
            var log = new TestLog();

            StoreCatalog catalog = new JsonStoreItemStore(file, log).Load();

            Assert.Same(StoreCatalog.Default, catalog);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
}
