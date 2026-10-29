using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// The road-test boards a suite scripts: the runs a read should answer with, the
/// all-cars record book, and the pushes it recorded. Offline by default, like
/// the shipping null-object; <see cref="IsOnline"/> opts the online assertions
/// in.
/// </summary>
internal sealed class FakeRoadTestStandings : IRoadTestStandings
{
    public List<RoadTestEntry> Entries { get; } = [];

    /// <summary>What <see cref="TopAsync"/> answers; null models an unreachable board.</summary>
    public IReadOnlyList<RoadTestEntry>? Board { get; set; }

    /// <summary>What <see cref="TopCarsAsync"/> answers; null models an unreachable book.</summary>
    public IReadOnlyList<RoadTestEntry>? Cars { get; set; }

    public List<(RoadTestMetric Metric, string CarKey, double Value, string PlayerName)> Pushed { get; } = [];

    public bool IsOnline { get; set; }

    public string? OwnUid { get; set; }

    public Task<IReadOnlyList<RoadTestEntry>?> TopAsync(
        RoadTestMetric metric, string carKey, int count,
        CancellationToken cancellationToken = default)
    {
        _ = metric;
        _ = carKey;
        _ = count;
        _ = cancellationToken;
        return Task.FromResult(Board);
    }

    public Task<IReadOnlyList<RoadTestEntry>?> TopCarsAsync(
        RoadTestMetric metric, CancellationToken cancellationToken = default)
    {
        _ = metric;
        _ = cancellationToken;
        return Task.FromResult(Cars);
    }

    public Task<bool> PushAsync(
        RoadTestMetric metric, string carKey, double value, string playerName,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        Pushed.Add((metric, carKey, value, playerName));
        return Task.FromResult(true);
    }
}
