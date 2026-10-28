using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The one-time upgrade path (PORTPLAN 51 interim): a career bought before it
/// was persisted left its hub pickup door in the address directory, so the
/// ownership is rebuilt from the door instead of being lost with the old
/// in-memory career.
/// </summary>
public sealed class DeliveryCareerRecoveryTests
{
    private static readonly HubCatalog Hubs = new(
        [new HubTier(1, true, [
            new HubCareer("groceries", "GROCERIES",
                [new HubLevel("Grocers and Sons", 100), new HubLevel("The Food Hub", 150)]),
            new HubCareer("coffee_beans", "COFFEE BEANS", [new HubLevel("CoffeeUs", 100)])])],
        CredPRamp.UnityDefault, ["Elena Marchetti"]);

    private static PointOfInterest Door(
        string id, string name, string category, string owner = "Elena Marchetti") =>
        new(id, name, new AddressLocation("Street 2", 4, HouseSide.Left),
            PoiTypes.Hub, owner, category);

    [Fact]
    public void Recover_RebuildsTheBoughtHubFromItsDoor()
    {
        DeliveryCareerState? state = DeliveryCareerRecovery.Recover(Hubs,
            [Door("groceries_grocers_and_sons", "Grocers and Sons", "groceries")]);

        Assert.NotNull(state);
        Assert.Equal(0, state.CredP);
        OwnedHub hub = Assert.Single(state.Owned!);
        Assert.Equal("Grocers and Sons", hub.Name);
        Assert.Equal(100, hub.Cost);
        Assert.Equal(1, hub.Tier);
        Assert.Equal(1, hub.Level);
        Assert.Equal("Elena Marchetti", hub.OwnerName);
        Assert.Equal("groceries_grocers_and_sons", hub.Key);
    }

    [Fact]
    public void Recover_RebuildsAnUpgradedLevel()
    {
        DeliveryCareerState? state = DeliveryCareerRecovery.Recover(Hubs,
            [Door("groceries_the_food_hub", "The Food Hub", "groceries")]);

        OwnedHub hub = Assert.Single(state!.Owned!);
        Assert.Equal(150, hub.Cost);
        Assert.Equal(2, hub.Level);
        // The rebuilt level drives the ramp: level 2 starts at 2 CredP.
        var career = new DeliveryCareer(Hubs, state);
        Assert.Equal(2, career.RegisterDelivery("groceries").CredPEarned);
    }

    [Fact]
    public void Recover_IgnoresOtherDoorsAndDuplicateHubs()
    {
        DeliveryCareerState? state = DeliveryCareerRecovery.Recover(Hubs,
        [
            Door("groceries_grocers_and_sons", "Grocers and Sons", "groceries"),
            Door("groceries_grocers_and_sons", "Grocers and Sons", "groceries"),
            new PointOfInterest(
                "shop", "Shop", new AddressLocation("Street 1", 2, HouseSide.Right)),
        ]);

        Assert.Single(state!.Owned!);
    }

    [Fact]
    public void Recover_KeepsAPurchaseTheContentNoLongerNames()
    {
        DeliveryCareerState? state = DeliveryCareerRecovery.Recover(Hubs,
            [Door("groceries_old_name", "Old Name", "groceries")]);

        OwnedHub hub = Assert.Single(state!.Owned!);
        Assert.Equal("Old Name", hub.Name);
        Assert.Equal(1, hub.Level);
    }

    [Fact]
    public void Recover_NoHubDoors_NothingToRecover()
    {
        Assert.Null(DeliveryCareerRecovery.Recover(Hubs, []));
    }
}
