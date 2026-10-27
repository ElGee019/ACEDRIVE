using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Live market-valuation board over the REST Firestore transport (Unity
/// ValuationEntriesService + ValuationPopScreen's snapshot listener).
///
/// WHY a background watcher instead of Unity's listener: the REST API has no
/// Listen call, so the board re-reads the weekly list on its own task every
/// <see cref="WatchSeconds"/> seconds while an entry is live. That task is not
/// the 10 Hz gameplay loop and never touches disk or the panel; it only
/// publishes a fresh list through <see cref="Changed"/>, which the flow drains
/// on its next tick. Offline keeps the null-object board and this class is
/// never built.
/// </summary>
public sealed class FirestoreValuationBoard : IValuationBoard, IDisposable
{
    // Unity's listener is immediate; the REST ceiling is one bounded re-read.
    // 30 s keeps a six-hour session under 800 list calls while a newcomer is
    // noticed inside half a minute.
    private const int WatchSeconds = 30;

    private readonly FirestoreValuationEntries _entries;
    private readonly Func<string?> _uid;
    private readonly ILog _log;
    private readonly CancellationToken _stopToken;
    private CancellationTokenSource? _watch;
    private bool _disposed;

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

    public async Task<IReadOnlyList<ValuationEntry>?> ListAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FirestoreDocument>? documents =
            await _entries.ListAsync(cancellationToken).ConfigureAwait(false);
        if (documents is null)
        {
            return null;
        }

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

    public void StartWatching()
    {
        if (_disposed || _watch is not null)
        {
            return;
        }

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

    // One bounded re-read per interval. A failed read stays quiet (the flow
    // keeps the last list); the next tick retries.
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

            IReadOnlyList<ValuationEntry>? entries =
                await ListAsync(cancellationToken).ConfigureAwait(false);
            if (entries is not null)
            {
                Changed?.Invoke(entries);
            }
        }
    }
}
