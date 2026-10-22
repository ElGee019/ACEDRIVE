using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Firestore-backed global name reservation. The doc id is the normalized
/// name and the first write is create-only, so two players cannot claim the
/// same key; the same uid may reclaim its own name after a local save wipe.
/// </summary>
public sealed class FirestorePlayerNameRegistry : IPlayerNameRegistry
{
    // A broken network must not hold the first-run page hostage: the claim
    // gives up after this window and lets single-player continue locally.
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromSeconds(10);

    private readonly IFirestoreDatabase _db;
    private readonly Func<CancellationToken, Task<string?>> _uidAsync;
    private readonly IClock _clock;
    private readonly ILog _log;

    public FirestorePlayerNameRegistry(
        IFirestoreDatabase db, Func<CancellationToken, Task<string?>> uidAsync,
        IClock clock, ILog log)
    {
        _db = db;
        _uidAsync = uidAsync;
        _clock = clock;
        _log = log;
    }

    public async Task<NameClaimResult> ClaimAsync(
        string displayName, CancellationToken cancellationToken = default)
    {
        if (!PlayerNameKey.IsValid(displayName))
        {
            return NameClaimResult.Invalid;
        }

        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ClaimTimeout);
        CancellationToken token = timeout.Token;

        string? uid;
        try
        {
            uid = await _uidAsync(token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return NameClaimResult.Unavailable;
        }
        if (string.IsNullOrEmpty(uid))
        {
            return NameClaimResult.Unavailable;
        }

        string key = PlayerNameKey.Normalize(displayName);
        string path = FirestoreCollections.PlayerNames + "/" + key;
        Dictionary<string, object?> fields = new(StringComparer.Ordinal)
        {
            ["ownerUid"] = uid,
            ["displayName"] = displayName.Trim(),
            ["claimedAt"] = FirebaseOptions.Timestamp(_clock.UtcNow),
        };

        FirestoreCreateResult created = await _db.CreateAsync(
            path, fields, token).ConfigureAwait(false);
        if (created == FirestoreCreateResult.Created)
        {
            _log.LogInfo($"firebase: name '{displayName.Trim()}' claimed");
            return NameClaimResult.Claimed;
        }
        if (created == FirestoreCreateResult.Failed)
        {
            return NameClaimResult.Unavailable;
        }

        FirestoreDocument? existing = await _db.GetAsync(path, token)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return NameClaimResult.Unavailable;
        }

        string? owner = existing.Text("ownerUid");
        if (string.Equals(owner, uid, StringComparison.Ordinal))
        {
            return NameClaimResult.Owned;
        }

        _log.LogInfo($"firebase: name '{displayName.Trim()}' already taken");
        return NameClaimResult.Taken;
    }
}
