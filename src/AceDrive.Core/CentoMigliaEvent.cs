// Cento Miglia entry (PORTPLAN item 49): what the page built when the player
// pressed ENTER EVENT. Pure data, so the race port can take it without
// reaching back into the page.
namespace AceDrive.Core;

/// <summary>
/// One built Cento Miglia event: the club that issued it, the registration
/// number its tier drew and the route length the operating zone allowed. Unity
/// keeps the same three on MilleMigliaManager (tier, regNumber, the route's
/// distance); the port hands them to the host as one record because the race
/// that would consume them is the next slice.
/// </summary>
public sealed record CentoMigliaEvent(
    int TierNumber,
    string TierLabel,
    int Registration,
    double DistanceKm);
