using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// The shipped Touristenfahrten table parses into the record shape the flow
/// reads (both pickup kinds, all five corners, the soup price), and a missing
/// file degrades to the built-in table with one warning.
/// </summary>
public sealed class JsonTouristenMenuTests
{
    [Fact]
    public void ShippedTable_LoadsBothPickupKindsAndEveryCorner()
    {
        string file = Path.Combine(
            AppContext.BaseDirectory, "Data", "touristenfahrten.json");
        TouristenMenu menu = new JsonTouristenMenu(file, new TestLog()).Load();

        Assert.True(menu.IsUsable);
        Assert.Equal(5, menu.Dropoffs.Count);
        Assert.Contains(menu.Dropoffs, place => place.Name == "Breidscheid Bridge");

        TouristenPickup pizza = Assert.IsType<TouristenPickup>(
            menu.PickupFor(PoiTypes.PizzaPlace));
        Assert.Contains(
            pizza.Items, item => item.Key == "soup" && Math.Abs(item.Price - 8.5) < 0.001);
        Assert.NotNull(menu.PickupFor(PoiTypes.CoffeeShop));
    }

    [Fact]
    public void Load_MissingFile_FallsBackWithWarning()
    {
        var log = new TestLog();
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

        TouristenMenu menu = new JsonTouristenMenu(file, log).Load();

        Assert.Equal(
            TouristenMenu.Default.Dropoffs.Select(place => place.Id),
            menu.Dropoffs.Select(place => place.Id));
        Assert.Single(log.Warnings);
    }
}
