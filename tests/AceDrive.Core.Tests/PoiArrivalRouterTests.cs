using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Typed POI dispatch (PORTPLAN item 24): the first owning handler
/// wins, unknown types stay a no-op, and Max's grocery builds its page.</summary>
public sealed class PoiArrivalRouterTests
{
    private static readonly PointOfInterest Maxs = new(
        "max-grocery", "Max's Grocery Store",
        new AddressLocation("Street 1", 7, HouseSide.Right),
        PoiTypes.Grocery, "Max Silvestri", "groceries");

    private static readonly ICommand NoOp = new RelayCommand(() => { });

    private static PoiArrivalRouter Router(params IPoiArrivalHandler[] handlers) =>
        new(handlers);

    [Fact]
    public void Open_UsesTheFirstHandlerThatOwnsTheType()
    {
        var first = new FakePoiArrivalHandler("bank", "bank-screen");
        var second = new FakePoiArrivalHandler(PoiTypes.Grocery, "store-screen");

        object? screen = Router(first, second).Open(Maxs, NoOp, () => { });

        Assert.Equal("store-screen", screen);
        Assert.Equal(0, first.Opens);
        Assert.Equal(1, second.Opens);
    }

    [Fact]
    public void Open_UnknownType_ReturnsNull()
    {
        object? screen = Router(new FakePoiArrivalHandler("bank"))
            .Open(Maxs, NoOp, () => { });

        Assert.Null(screen);
    }

    [Fact]
    public void StoreHandler_OwnsEveryGroceryAlias()
    {
        StorePoiHandler handler = StoreHandler();

        Assert.True(handler.Owns(Maxs));
        Assert.True(handler.Owns(Maxs with { Type = PoiTypes.GroceryStore }));
        Assert.True(handler.Owns(Maxs with { Type = PoiTypes.MaxsGrocery }));
        Assert.False(handler.Owns(Maxs with { Type = "bank" }));
    }

    [Fact]
    public void StoreHandler_OpensTheStorePage()
    {
        StorePoiHandler handler = StoreHandler();

        object? screen = handler.Open(Maxs, NoOp, () => { });

        var store = Assert.IsType<StoreScreenViewModel>(screen);
        Assert.Equal("MAX'S GROCERY STORE", store.StoreTitle);
        Assert.Contains(store.Items, item => item.Item.Name == "MAPLE SYRUP");
    }

    private static StorePoiHandler StoreHandler()
    {
        var car = new CarSpec(
            "ks_test_grocery", 2020, "Test", "Grocery", TrunkLitres: 60);
        var garage = new Garage([car]);
        garage.Add(car, week: 1);
        return new StorePoiHandler(
            new StoreNetwork(StoreCatalog.Default, PoiCatalog.Default),
            new Trunk(),
            new DriverWallet(new DriverState(1000.0, 0.0)),
            garage,
            new FakeFriendshipStore(),
            new VoiceBeat(new FakeClock()));
    }
}
