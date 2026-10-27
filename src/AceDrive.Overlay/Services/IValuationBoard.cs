using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The weekly market-valuation list (PORTPLAN item 49, Unity
/// ValuationEntriesService). The flow reads the list to draw a competitor,
/// pushes the player's own entry once a week, and wipes that entry on the
/// Monday reset. The live watcher is the event seam: the implementation
/// raises <see cref="Changed"/> with a fresh list after entries land, so the
/// flow compares newcomers without polling the network from the gameplay loop.
/// </summary>
public interface IValuationBoard
{
    /// <summary>True when a backend is configured; false keeps the flow local.</summary>
    bool IsOnline { get; }

    /// <summary>The anonymous uid, null while auth has not landed or offline.</summary>
    string? OwnUid { get; }

    /// <summary>Every entry currently on the weekly list, null when unreachable.</summary>
    Task<IReadOnlyList<ValuationEntry>?> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Pushes (or overwrites) this player's entry for the week.</summary>
    Task<bool> PushEntryAsync(
        string playerName, string carKey, int jobsWithCar,
        double engineBrokenPercent, double score,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes only this player's entry on the weekly reset.</summary>
    Task<bool> WipeOwnAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts the live newcomer watcher (no-op offline).</summary>
    void StartWatching();

    /// <summary>Stops the watcher when the entry goes away (reset/offline).</summary>
    void StopWatching();

    /// <summary>A fresh list arrived; raised off the UI thread.</summary>
    event Action<IReadOnlyList<ValuationEntry>>? Changed;
}
