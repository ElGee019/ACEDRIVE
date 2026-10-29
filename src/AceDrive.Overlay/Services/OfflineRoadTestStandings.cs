using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The offline road-test boards: no Firebase configured, so nothing is pushed
/// and no rival run is known. Both board reads return null on purpose — that is
/// the flow's "unreachable" answer, which keeps the row the page already painted
/// — exactly like Unity's unconfigured build, where the local-row board stays up
/// once the list load fails.
/// </summary>
public sealed class OfflineRoadTestStandings : IRoadTestStandings
{
    public bool IsOnline => false;

    public string? OwnUid => null;

    public Task<IReadOnlyList<RoadTestEntry>?> TopAsync(
        RoadTestMetric metric, string carKey, int count,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RoadTestEntry>?>(null);

    public Task<IReadOnlyList<RoadTestEntry>?> TopCarsAsync(
        RoadTestMetric metric, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RoadTestEntry>?>(null);

    public Task<bool> PushAsync(
        RoadTestMetric metric, string carKey, double value, string playerName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
