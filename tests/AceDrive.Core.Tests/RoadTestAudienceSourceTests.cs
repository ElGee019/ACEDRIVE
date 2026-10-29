using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The road test's crowd readings (PORTPLAN item 49, Unity
/// RoadTestAudienceCalculator's three counters): the familiarity bands split
/// into friends and acquaintances, and the citizen total follows the live
/// delivery board so a shop opened mid-session still counts.
/// </summary>
public sealed class RoadTestAudienceSourceTests
{
    [Fact]
    public void Count_SplitsFamiliarityIntoFriendsAndAcquaintances()
    {
        var friends = new FakeFriendshipStore();
        for (int i = 0; i < RoadTestAudience.FriendLevel; i++)
        {
            friends.RegisterRide("Ann");
        }
        for (int i = 0; i < RoadTestAudience.AcquaintanceLevel; i++)
        {
            friends.RegisterRide("Ben");
        }
        friends.RegisterRide("Cal"); // level 1: neither band

        var board = new DeliveryBoard(DeliveryCatalog.Default);
        var city = new CityEconomy(board.Catalog.Stores, CityRules.Default);
        var source = new RoadTestAudienceSource(city, board, friends);

        RoadTestAudienceCounts counts = source.Count();

        Assert.Equal(1, counts.Friends);
        Assert.Equal(1, counts.Acquaintances);
        Assert.Equal(city.TotalCitizens(board.Catalog.Stores), counts.Citizens);
    }

    [Fact]
    public void Count_FollowsAShopConnectedAfterTheTownWasBuilt()
    {
        var board = new DeliveryBoard(DeliveryCatalog.Default);
        var city = new CityEconomy(board.Catalog.Stores, CityRules.Default);
        var source = new RoadTestAudienceSource(city, board, new FakeFriendshipStore());
        int before = source.Count().Citizens;

        var store = new DeliveryStore("late_shop", "Late Shop", "Ann", 20, 40, 1.0);
        city.AddStore(store);
        board.Add(store);

        // The board is the live holder: a row added after startup counts, so the
        // receipt never under-reports the town.
        Assert.True(source.Count().Citizens > before);
    }
}
