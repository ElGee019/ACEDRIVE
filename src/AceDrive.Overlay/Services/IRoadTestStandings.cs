using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The road-test boards (PORTPLAN item 49, Unity RoadTestStandingsService): the
/// run a test just set is pushed as this player's best, and the page reads the
/// car's board back to paint the standings after the test, plus the all-cars
/// record book (every car's best, one row per car) Unity shows beside it.
/// Implementations are the Firestore board and an offline null-object; the flow
/// never talks to the transport.
/// </summary>
public interface IRoadTestStandings
{
    /// <summary>True when a backend is configured; false keeps the board local.</summary>
    bool IsOnline { get; }

    /// <summary>The anonymous uid, null while auth has not landed or offline.</summary>
    string? OwnUid { get; }

    /// <summary>
    /// The car's best runs, lowest value first, capped at
    /// <paramref name="count"/>. Null when the board is unreachable, so the
    /// caller keeps the row it already painted instead of blanking the page.
    /// </summary>
    Task<IReadOnlyList<RoadTestEntry>?> TopAsync(
        RoadTestMetric metric, string carKey, int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The all-cars record book (Unity LoadGlobalBestPerCar): every car's best
    /// run, one row per car, lowest value first. Null when the board is
    /// unreachable, so the caller keeps the row it already painted.
    /// </summary>
    Task<IReadOnlyList<RoadTestEntry>?> TopCarsAsync(
        RoadTestMetric metric, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores <paramref name="value"/> as this player's best for the car: a
    /// slower stored value is kept, a faster one is overwritten.
    /// </summary>
    Task<bool> PushAsync(
        RoadTestMetric metric, string carKey, double value, string playerName,
        CancellationToken cancellationToken = default);
}
