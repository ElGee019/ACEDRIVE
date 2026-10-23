using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The delivery cart order becomes the run's hub + store legs
/// (PORTPLAN 19; Unity's pickup and delivery routes without the map picks).</summary>
public sealed class DeliveryJobPlannerTests
{
    private static readonly HubCareer Groceries = new(
        "groceries", "GROCERIES",
        [new HubLevel("Grocers and Sons", 100), new HubLevel("The Food Hub", 150)]);
    private static readonly HubCareer Coffee = new(
        "coffee_beans", "COFFEE BEANS", [new HubLevel("CoffeeUs", 100)]);
    private static readonly HubCatalog Hubs = new([
        new HubTier(1, true, [Groceries, Coffee]),
    ]);

    private static readonly DeliveryStore Grocery =
        new("grocery", "Adenau Post", "Hahn", 10, 40, 2.50, "groceries");
    private static readonly DeliveryStore CoffeeStore =
        new("coffee", "Nürburg Kiosk", "Fuchs", 10, 40, 2.20, "coffee_beans");
    private static readonly DeliveryStore OpenStore =
        new("open", "Open Shop", "Weber", 10, 60, 3.10);

    private static readonly DeliveryCatalog Catalog =
        new([Grocery, CoffeeStore, OpenStore], 0.7, 500);

    private static DeliveryCareer Career()
    {
        var career = new DeliveryCareer(Hubs, 500);
        Assert.True(career.TryOpenCareer(1, 0, 0)); // groceries
        Assert.True(career.TryOpenCareer(1, 1, 0)); // coffee
        return career;
    }

    [Fact]
    public void OneHubPerCategory_ThenEveryStoreInCartOrder()
    {
        DeliveryCareer career = Career();

        IReadOnlyList<DeliveryStop> stops = DeliveryJobPlanner.Plan(
            Catalog, career, ["grocery", "coffee", "grocery"]);

        Assert.Equal(
            [
                DeliveryStopKind.Hub, DeliveryStopKind.Hub,
                DeliveryStopKind.Store, DeliveryStopKind.Store, DeliveryStopKind.Store,
            ],
            stops.Select(stop => stop.Kind));

        Assert.Equal(["groceries_grocers_and_sons", "coffee_beans_coffeeus"],
            stops.Take(2).Select(stop => stop.Key));
        Assert.Equal(["Grocers and Sons", "CoffeeUs"],
            stops.Take(2).Select(stop => stop.Name));
        Assert.Equal(["grocery", "coffee", "grocery"],
            stops.Skip(2).Select(stop => stop.Key));
    }

    [Fact]
    public void NewestOwnedHub_IsThePickupSource()
    {
        DeliveryCareer career = Career();
        Assert.True(career.TryOpenCareer(1, 0, 1)); // The Food Hub upgrade

        DeliveryStop hub = Assert.Single(
            DeliveryJobPlanner.Plan(Catalog, career, ["grocery"]),
            stop => stop.Kind == DeliveryStopKind.Hub);

        Assert.Equal("The Food Hub", hub.Name);
        Assert.Equal("groceries_the_food_hub", hub.Key);
    }

    [Fact]
    public void OpenStore_NeedsNoHubLeg()
    {
        IReadOnlyList<DeliveryStop> stops = DeliveryJobPlanner.Plan(
            Catalog, new DeliveryCareer(Hubs, 0), ["open"]);

        DeliveryStop stop = Assert.Single(stops);
        Assert.Equal(DeliveryStopKind.Store, stop.Kind);
        Assert.Equal("open", stop.Key);
    }

    [Fact]
    public void MissingStore_IsSkipped()
    {
        IReadOnlyList<DeliveryStop> stops = DeliveryJobPlanner.Plan(
            Catalog, new DeliveryCareer(Hubs, 0), ["gone"]);

        Assert.Empty(stops);
    }

    [Fact]
    public void EmptyCart_PlansNothing()
    {
        Assert.Empty(DeliveryJobPlanner.Plan(Catalog, Career(), []));
    }
}
