using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The customer-door draw: away from the dealer and the player's home
/// while the map has a third street, another door on the dealer's block when it
/// does not, and a clean failure when no second door exists.</summary>
public sealed class CarHaulAddressPlanTests
{
    [Fact]
    public void Draw_AvoidsBothTheDealersAndTheHomeStreet()
    {
        AddressMap map = Map(
            new AddressStreet("s1", "Street 1", 0.0, 0.3),
            new AddressStreet("s2", "Street 2", 0.3, 0.6),
            new AddressStreet("s3", "Street 3", 0.6, 1.0));

        Assert.True(CarHaulAddressPlan.TryDraw(
            map, new Random(3),
            new AddressLocation("Street 1", 4, HouseSide.Left),
            new AddressLocation("Street 2", 2, HouseSide.Right),
            out AddressLocation customer));

        Assert.Equal("Street 3", customer.StreetName);
    }

    [Fact]
    public void Draw_PrefersTheDealersBlockOverTheHomeStreet()
    {
        // Only two streets: the home street stays excluded and the customer
        // gets another door on the dealer's block instead.
        AddressMap map = Map(
            new AddressStreet("s1", "Street 1", 0.0, 0.5),
            new AddressStreet("s2", "Street 2", 0.5, 1.0));

        for (int seed = 0; seed < 20; seed++)
        {
            Assert.True(CarHaulAddressPlan.TryDraw(
                map, new Random(seed),
                new AddressLocation("Street 1", 4, HouseSide.Left),
                new AddressLocation("Street 2", 2, HouseSide.Right),
                out AddressLocation customer));

            Assert.Equal("Street 1", customer.StreetName);
            Assert.NotEqual(4, customer.HouseNumber);
        }
    }

    [Fact]
    public void Draw_UsesAnotherHouseWhenTheDealersStreetIsTheOnlyOne()
    {
        AddressMap map = Map(new AddressStreet("s1", "Street 1", 0.0, 0.2));

        for (int seed = 0; seed < 20; seed++)
        {
            Assert.True(CarHaulAddressPlan.TryDraw(
                map, new Random(seed),
                new AddressLocation("Street 1", 2, HouseSide.Left),
                homeDoor: null,
                out AddressLocation customer));

            Assert.Equal("Street 1", customer.StreetName);
            Assert.NotEqual(2, customer.HouseNumber);
        }
    }

    [Fact]
    public void Draw_FailsWhenTheDealersStreetHasASingleDoor()
    {
        AddressMap map = Map(new AddressStreet("s1", "Street 1", 0.0, 0.01));

        Assert.False(CarHaulAddressPlan.TryDraw(
            map, new Random(1),
            new AddressLocation("Street 1", 1, HouseSide.Right),
            homeDoor: null,
            out _));
    }

    [Fact]
    public void Draw_FailsWhenTheMapHasNoHouses()
    {
        AddressMap map = new("Germany", "Brands", 0.0, 30.0, []);

        Assert.False(CarHaulAddressPlan.TryDraw(
            map, new Random(1), default, homeDoor: null, out _));
    }

    [Fact]
    public void Draw_FallsBackToTheHomeStreetWhenItIsTheOnlyOne()
    {
        // The dealer is on a street outside this map, so the single known
        // street is the player's own: there is nowhere else to deliver.
        AddressMap map = Map(new AddressStreet("s1", "Street 1", 0.0, 0.2));

        Assert.True(CarHaulAddressPlan.TryDraw(
            map, new Random(2),
            new AddressLocation("Somewhere else", 1, HouseSide.Left),
            new AddressLocation("Street 1", 3, HouseSide.Right),
            out AddressLocation customer));

        Assert.Equal("Street 1", customer.StreetName);
        Assert.NotEqual(3, customer.HouseNumber);
    }

    [Fact]
    public void Draw_SkipsDoorsThatAlreadyHaveAnOccupant()
    {
        // While one street still has a free house, an owned door elsewhere on
        // it must not be drawn for the customer either.
        AddressMap map = Map(
            new AddressStreet("s1", "Street 1", 0.0, 0.5),
            new AddressStreet("s2", "Street 2", 0.5, 1.0));
        var free = new AddressLocation("Street 2", 1, HouseSide.Right);

        for (int seed = 0; seed < 20; seed++)
        {
            Assert.True(CarHaulAddressPlan.TryDraw(
                map, new Random(seed),
                new AddressLocation("Street 1", 4, HouseSide.Left),
                homeDoor: null,
                out AddressLocation customer,
                door => !door.Matches(free)));

            Assert.True(customer.Matches(free));
        }
    }

    [Fact]
    public void Draw_NeverUsesThePlayersOwnDoor()
    {
        // The only free house left is the player's: the haul must fail rather
        // than deliver the car to the player's own address.
        AddressMap map = Map(new AddressStreet("s1", "Street 1", 0.0, 0.2));
        var home = new AddressLocation("Street 1", 3, HouseSide.Right);

        Assert.False(CarHaulAddressPlan.TryDraw(
            map, new Random(1),
            new AddressLocation("Somewhere else", 1, HouseSide.Left),
            home, out _,
            door => !door.Matches(home)));
    }

    private static AddressMap Map(params AddressStreet[] streets) =>
        new("Germany", "Brands", 1929.0, 30.0, streets);
}
