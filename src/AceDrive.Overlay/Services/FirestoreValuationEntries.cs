using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Weekly vehicle-valuation entries (PORTPLAN item 49, Unity
/// ValuationEntriesService): one doc per entrant keyed by uid, a permanent
/// house entry, a 48 h TTL stamp and the Monday-midnight wipe. Backend parity
/// only until the valuation screen lands.
/// </summary>
public sealed class FirestoreValuationEntries
{
    private readonly IFirestoreDatabase _db;
    private readonly Func<CancellationToken, Task<string?>> _uidAsync;
    private readonly IClock _clock;
    private readonly ILog _log;

    public FirestoreValuationEntries(
        IFirestoreDatabase db, Func<CancellationToken, Task<string?>> uidAsync,
        IClock clock, ILog log)
    {
        _db = db;
        _uidAsync = uidAsync;
        _clock = clock;
        _log = log;
    }

    public Task<IReadOnlyList<FirestoreDocument>?> ListAsync(
        CancellationToken cancellationToken = default) =>
        _db.ListAsync(FirestoreCollections.Valuation, cancellationToken);

    public async Task<bool> PushEntryAsync(
        string playerName, string carKey, int jobsWithCar,
        double engineBrokenPercent, double score,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(carKey)) return true;
        string? uid = await UidAsync(cancellationToken).ConfigureAwait(false);
        if (uid is null) return false;

        return await _db.SetFieldsAsync(
            FirestoreCollections.Valuation + "/" + uid,
            EntryFields(
                uid, playerName, carKey, jobsWithCar, engineBrokenPercent, score),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Wipes the weekly list, then writes the permanent house entry.</summary>
    public async Task<bool> ResetAsync(
        string houseCarKey, CancellationToken cancellationToken = default)
    {
        if (!await WipeAsync(cancellationToken).ConfigureAwait(false))
        {
            _log.LogWarning("firebase: valuation wipe failed");
            return false;
        }

        return await _db.SetFieldsAsync(
            FirestoreCollections.Valuation + "/" + ValuationRules.HouseEntryDocId,
            EntryFields(
                ValuationRules.HouseEntryDocId, ValuationRules.HouseEntryName,
                houseCarKey, 0, 0.0, 0.0),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes only this player's own entry (Unity WipeOwnEntry): the weekly
    /// reset must never remove a rival's doc from a shared list. Missing uid
    /// answers false instead of wiping the list.
    /// </summary>
    public async Task<bool> WipeOwnAsync(
        string uid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(uid)) return false;
        return await _db.BatchDeleteAsync(
            [FirestoreCollections.Valuation + "/" + uid], cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> WipeAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FirestoreDocument>? documents =
            await ListAsync(cancellationToken).ConfigureAwait(false);
        if (documents is null) return false;
        if (documents.Count == 0) return true;

        List<string> paths = [];
        foreach (FirestoreDocument doc in documents)
        {
            paths.Add(FirestoreCollections.Valuation + "/" + doc.Id);
        }
        return await _db.BatchDeleteAsync(paths, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> UidAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _uidAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return null;
        }
    }

    private Dictionary<string, object?> EntryFields(
        string uid, string playerName, string carKey, int jobsWithCar,
        double engineBrokenPercent, double score)
    {
        DateTime now = _clock.UtcNow;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["uid"] = uid,
            ["playerName"] = playerName ?? string.Empty,
            ["carId"] = carKey,
            ["jobsWithCar"] = Math.Max(0, jobsWithCar),
            ["engineBrokenPercent"] = Math.Clamp(engineBrokenPercent, 0.0, 1.0),
            ["score"] = Math.Max(0.0, score),
            ["enteredAt"] = FirebaseOptions.Timestamp(now),
            ["expiresAt"] = ValuationRules.ComputeExpiryUtc(now),
        };
    }
}
