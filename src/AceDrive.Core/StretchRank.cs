// Pure data. No WPF / Windows / telemetry / backend references.
namespace AceDrive.Core;

/// <summary>
/// One stretch's standing for a car model: the player's rank among the drivers
/// who ran that same model there, and how many that field holds. The weekly
/// market valuation turns a model's rows into a single 0..1 ranking
/// (<see cref="ValuationRules.LeaderboardRanking"/>); the rows themselves arrive
/// with the stretch leaderboards, which the overlay does not have yet, so the
/// owned cars carry <see cref="OwnedCar.DefaultLeaderboardRanking"/> until then.
/// Each row is one stretch: the caller owns uniqueness.
/// </summary>
public sealed record StretchRank(int SameCarRank, int TotalSameCarPlayers);
