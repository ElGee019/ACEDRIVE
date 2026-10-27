// Pure gameplay data. The catch-up a restart banks after entries landed while
// the overlay was closed (Unity ValuationOfflineService.OfflineGains).
namespace AceDrive.Core;

/// <summary>
/// The wins that happened while away plus the full settled uid set, so the
/// live listener never pays the same entrant twice.
/// </summary>
public sealed record ValuationOfflineGains(
    double TotalXp,
    int WinCount,
    IReadOnlyList<string> NewSeenUids);
