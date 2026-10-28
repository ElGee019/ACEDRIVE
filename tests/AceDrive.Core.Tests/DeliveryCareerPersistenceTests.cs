using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The delivery career's persistent half (PORTPLAN 18/51): the balance, the
/// bought hubs and the per-hub ramp counts survive a restart, so a player who
/// opened a career does not find it gone at the next launch.
/// </summary>
public sealed class DeliveryCareerPersistenceTests
{
    private static readonly HubCatalog Hubs = new(
        [new HubTier(1, true, [
            new HubCareer("groceries", "GROCERIES",
                [new HubLevel("Grocers and Sons", 100), new HubLevel("The Food Hub", 150)])])],
        CredPRamp.UnityDefault, ["Elena Marchetti"]);

    [Fact]
    public void Snapshot_RestoresTheBalanceAndTheBoughtHubs()
    {
        var career = new DeliveryCareer(Hubs, 300);
        Assert.True(career.TryOpenCareer(1, 0, 0));
        career.AddCredP(25);

        var resumed = new DeliveryCareer(Hubs, career.Snapshot());

        Assert.Equal(225, resumed.CredP);
        OwnedHub hub = Assert.Single(resumed.Owned);
        Assert.Equal("Grocers and Sons", hub.Name);
        Assert.Equal("groceries_grocers_and_sons", hub.Key);
        Assert.True(resumed.OwnsCategory("groceries"));
    }

    [Fact]
    public void Snapshot_IsNotMovedByALaterPurchase()
    {
        // The written state must not alias the live list: a purchase after the
        // bank is the throttle's bug, not the snapshot's.
        var career = new DeliveryCareer(Hubs, 300);
        Assert.True(career.TryOpenCareer(1, 0, 0));
        DeliveryCareerState state = career.Snapshot();
        Assert.True(career.TryOpenCareer(1, 0, 1));

        Assert.Single(state.Owned!);
        Assert.Equal("Grocers and Sons", state.Owned![0].Name);
    }

    [Fact]
    public void Resume_KeepsTheRampCounting()
    {
        var career = new DeliveryCareer(Hubs, 100);
        Assert.True(career.TryOpenCareer(1, 0, 0));
        Assert.Equal(1, career.RegisterDelivery("groceries").CredPEarned);

        var resumed = new DeliveryCareer(Hubs, career.Snapshot());

        // The first delivery after the restart is the hub's second: the ramp
        // reads the saved count, so a relaunch never rewinds the progression.
        Assert.Equal(2, resumed.RegisterDelivery("groceries").CredPEarned);
    }

    [Fact]
    public void Resume_ToleratesAHandEditedCareer()
    {
        // A negative balance clamps, a hub without a name or category drops, a
        // duplicate keeps one copy, a missing map key is rebuilt and a dirty
        // ramp count is ignored rather than reaching the earn rule.
        var state = new DeliveryCareerState(
            -5,
            [
                new OwnedHub("", "groceries", 100, 1, 1),
                new OwnedHub("Grocers and Sons", "groceries", 100, 1, 1),
                new OwnedHub("Grocers and Sons", "groceries", 100, 1, 1),
            ],
            new Dictionary<string, int>
            {
                [""] = 3,
                ["Grocers and Sons"] = -2,
            });

        var career = new DeliveryCareer(Hubs, state);

        Assert.Equal(0, career.CredP);
        OwnedHub hub = Assert.Single(career.Owned);
        Assert.Equal("groceries_grocers_and_sons", hub.Key);
        Assert.Equal(1, career.RegisterDelivery("groceries").CredPEarned);
    }
}
