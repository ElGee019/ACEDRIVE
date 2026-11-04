using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Live market-valuation board over the REST Firestore transport (Unity
/// ValuationEntriesService + ValuationPopScreen's snapshot listener).
///
/// WHY a background watcher instead of Unity's listener: the REST API has no
/// Listen call, so the board re-reads the list on its own task every
/// <see cref="WatchSeconds"/> seconds while an entry is live. The read is a
/// range query — only the entries that landed since the last one — so an idle
/// poll answers no documents, and Firestore bills per document returned, which
/// makes a poll that finds nothing free. That task is not the 10 Hz gameplay
/// loop and never touches disk or the panel; it only publishes a fresh batch
/// through <see cref="Changed"/>, which the flow drains on its next tick.
/// Offline keeps the null-object board and this class is never built.
/// </summary>
public sealed class FirestoreValuationBoard : IValuationBoard, IDisposable
{
    // Unity's listener is immediate; the REST ceiling is one bounded re-read.
    // 60 s halves the reads of a 30 s cadence (a six-hour session stays under
    // 400 polls) and a newcomer is still noticed inside a minute, which is
    // noise against a weekly event.
    private const int WatchSeconds = 60;

    // A rival's enteredAt comes from that player's own clock, and clocks drift:
    // the cursor starts this far back, so an entry stamped slightly behind the
    // last cursor is not missed for good. The settled set drops the overlap, and
    // while nobody enters nothing is re-read, so the margin costs nothing. A
    // badly skewed clock is still covered by the restart catch-up.
    private static readonly TimeSpan WatchOverlap = TimeSpan.FromMinutes(2);

    private readonly FirestoreValuationEntries _entries;
    private readonly Func<string?> _uid;
    private readonly ILog _log;
    private readonly CancellationToken _stopToken;
    private CancellationTokenSource? _watch;
    private bool _disposed;

    // The newest enteredAt this watcher has settled; every read asks for what
    // landed after it. Only the watch loop moves it.
    private DateTime _since;

    public FirestoreValuationBoard(
        FirestoreValuationEntries entries, Func<string?> uid, ILog log,
        CancellationToken stopToken)
    {
        _entries = entries;
        _uid = uid;
        _log = log;
        _stopToken = stopToken;
    }

    public bool IsOnline => true;

    public string? OwnUid => _uid();

    public event Action<IReadOnlyList<ValuationEntry>>? Changed;

    public event Action? OwnEntryMissing;

    public async Task<IReadOnlyList<ValuationEntry>?> ListAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FirestoreDocument>? documents =
            await _entries.ListAsync(cancellationToken).ConfigureAwait(false);
        return documents is null ? null : Map(documents);
    }

    /// <summary>
    /// The entries that landed after the stamp, in list order: the watcher's
    /// read. An idle list answers empty, and an empty answer costs no backend
    /// document reads.
    /// </summary>
    public Task<IReadOnlyList<ValuationEntry>?> ListSinceAsync(
        DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        ReadSinceAsync(sinceUtc, cancellationToken);

    /// <summary>
    /// True/false when the backend answered whether this player's entry is
    /// listed; null when it could not tell, so a failed read is never mistaken
    /// for a wipe.
    /// </summary>
    public Task<bool?> OwnEntryExistsAsync(
        CancellationToken cancellationToken = default) =>
        OwnUid is { Length: > 0 } uid
            ? _entries.OwnEntryExistsAsync(uid, cancellationToken)
            : Task.FromResult<bool?>(false);

    public Task<bool> PushEntryAsync(
        string playerName, string carKey, int jobsWithCar,
        double engineBrokenPercent, double score,
        CancellationToken cancellationToken = default) =>
        _entries.PushEntryAsync(
            playerName, carKey, jobsWithCar, engineBrokenPercent, score,
            cancellationToken);

    public Task<bool> WipeOwnAsync(CancellationToken cancellationToken = default) =>
        OwnUid is { Length: > 0 } uid
            ? _entries.WipeOwnAsync(uid, cancellationToken)
            : Task.FromResult(false);

    public void StartWatching(DateTime sinceUtc)
    {
        if (_disposed || _watch is not null)
        {
            return;
        }

        _since = sinceUtc.Kind == DateTimeKind.Utc
            ? sinceUtc
            : sinceUtc.ToUniversalTime();
        _watch = CancellationTokenSource.CreateLinkedTokenSource(_stopToken);
        SafeTask.Forget(WatchAsync(_watch.Token), _log, "valuation watcher");
    }

    public void StopWatching()
    {
        if (_watch is null)
        {
            return;
        }

        try
        {
            _watch.Cancel();
        }
        finally
        {
            _watch.Dispose();
            _watch = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        StopWatching();
    }

    // One step per interval: only what landed since the last one, so nothing
    // new answers zero documents and costs nothing.
    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(WatchSeconds), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!await WatchOnceAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>
    /// One watcher step: read what landed since the cursor, confirm the own
    /// entry is still listed, then publish the batch and move the cursor.
    /// Answers false when the own entry is gone, which stops the loop. Public
    /// so the step can be driven without waiting out the interval; the timer
    /// belongs to <see cref="StartWatching"/>.
    /// </summary>
    public async Task<bool> WatchOnceAsync(
        CancellationToken cancellationToken = default)
    {
        // Auth has not landed yet: there is no own entry to read or check.
        if (string.IsNullOrEmpty(OwnUid))
        {
            return true;
        }

        IReadOnlyList<ValuationEntry>? entries = await ReadSinceAsync(
            SinceWithOverlap(), cancellationToken).ConfigureAwait(false);
        if (entries is not { Count: > 0 })
        {
            return true;
        }

        // Only publish while the own doc is still listed: a push that never
        // landed must not earn against rivals. An unanswerable check keeps the
        // cursor and retries on the next poll.
        bool? own = await OwnEntryExistsAsync(cancellationToken)
            .ConfigureAwait(false);
        if (own is null)
        {
            return true;
        }
        if (own is false)
        {
            OwnEntryMissing?.Invoke();
            return false;
        }

        _since = Newest(entries, _since);
        Changed?.Invoke(entries);
        return true;
    }

    private DateTime SinceWithOverlap() =>
        _since >= DateTime.MinValue + WatchOverlap
            ? _since - WatchOverlap
            : DateTime.MinValue;

    private static DateTime Newest(
        IReadOnlyList<ValuationEntry> entries, DateTime fallback)
    {
        DateTime newest = fallback;
        foreach (ValuationEntry entry in entries)
        {
            if (entry.EnteredAt is { } stamp && stamp > newest)
            {
                newest = stamp;
            }
        }
        return newest;
    }

    private async Task<IReadOnlyList<ValuationEntry>?> ReadSinceAsync(
        DateTime sinceUtc, CancellationToken cancellationToken)
    {
        IReadOnlyList<FirestoreDocument>? documents =
            await _entries.ListSinceAsync(sinceUtc, cancellationToken)
                .ConfigureAwait(false);
        return documents is null ? null : Map(documents);
    }

    private static List<ValuationEntry> Map(IReadOnlyList<FirestoreDocument> documents)
    {
        List<ValuationEntry> entries = [];
        foreach (FirestoreDocument document in documents)
        {
            // The house entry is local (Unity never writes it); a legacy cloud
            // copy must not shadow the freshly drawn local car or seed the
            // settled set with a phantom rival.
            if (string.Equals(
                document.Id, ValuationRules.HouseEntryDocId, StringComparison.Ordinal))
            {
                continue;
            }
            if (ValuationEntryMapper.Map(document) is { } entry)
            {
                entries.Add(entry);
            }
        }
        return entries;
    }
}
