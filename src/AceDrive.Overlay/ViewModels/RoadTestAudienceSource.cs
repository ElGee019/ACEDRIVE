using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The crowd the road test's issue reaches (PORTPLAN item 49, Unity
/// RoadTestAudienceCalculator's three readings): the town's citizens from the
/// city economy, the familiar residents from the friendship store. One small
/// collaborator so the flow asks once per receipt and the stores keep their own
/// tables.
///
/// WHY the live board and not a copied list: a shop connected at runtime appends
/// its row to <see cref="DeliveryBoard"/>, so a snapshot taken at startup would
/// quietly under-count the town for the whole session.
///
/// WHY no always-friend doors: Unity counts <c>justins_place</c>,
/// <c>annabellas_place</c> and <c>home</c> as friends whatever their
/// familiarity. This port has no such residents — the named doors live on
/// generated businesses — so the familiarity band is the whole rule.
/// </summary>
public sealed class RoadTestAudienceSource
{
    private readonly CityEconomy _city;
    private readonly DeliveryBoard _board;
    private readonly IFriendshipStore _friends;

    public RoadTestAudienceSource(
        CityEconomy city, DeliveryBoard board, IFriendshipStore friends)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(friends);
        _city = city;
        _board = board;
        _friends = friends;
    }

    /// <summary>
    /// The three readings: citizens, friends (familiarity 5+) and acquaintances
    /// (the 2-4 band, which is exactly "at least 2" minus the friends).
    /// </summary>
    public RoadTestAudienceCounts Count()
    {
        int citizens = _city.TotalCitizens(_board.Catalog.Stores);
        int friends = _friends.CountAtLeast(RoadTestAudience.FriendLevel);
        int acquaintances =
            _friends.CountAtLeast(RoadTestAudience.AcquaintanceLevel) - friends;
        return new RoadTestAudienceCounts(
            Math.Max(0, citizens), Math.Max(0, friends), Math.Max(0, acquaintances));
    }
}
