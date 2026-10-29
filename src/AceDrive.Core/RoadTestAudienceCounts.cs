// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The three readings the road test's receipt prices the crowd from (PORTPLAN
// item 49, Unity RoadTestAudienceCalculator.GetTotalCitizens/GetFriendCount/
// GetAcquaintanceCount). The flow gathers them once per receipt so the page
// never reads a store itself.
namespace AceDrive.Core;

/// <summary>
/// How many people the issue reaches: the town's citizens, the residents the
/// driver is a friend of (familiarity 5+) and the acquaintances (familiarity
/// 2-4). Non-positive observations are clamped to zero.
/// </summary>
public sealed record RoadTestAudienceCounts(int Citizens, int Friends, int Acquaintances)
{
    /// <summary>No crowd readings at all: a career that has not met anyone.</summary>
    public static readonly RoadTestAudienceCounts None = new(0, 0, 0);
}
