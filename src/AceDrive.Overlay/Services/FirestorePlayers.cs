using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The player's own Firestore doc: XP, longest zone and best-shift record.
/// One doc per world identity — the normalized display name, exactly like
/// <c>acevo_playerNames</c> — with the owning anonymous uid stored as
/// <c>ownerUid</c> so the rules can guard the write. A reset that starts a
/// new username therefore opens a new board row instead of overwriting the
/// old one. Merge writes keep the three writers from clobbering each other's
/// fields.
/// </summary>
public sealed class FirestorePlayers
{
    private readonly IFirestoreDatabase _db;
    private readonly IClock _clock;
    private readonly ILog _log;

    public FirestorePlayers(IFirestoreDatabase db, IClock clock, ILog log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    /// <summary>
    /// Every player doc, after moving a pre-name-key row off the old
    /// <c>{uid}</c> id (see <see cref="MigrateLegacyRowAsync"/>). The uid is a
    /// callback because the first request is what signs in; evaluating it
    /// before the list would always answer null on a cold boot.
    /// </summary>
    public async Task<IReadOnlyList<FirestoreDocument>?> LoadAsync(
        Func<string?> uid, CancellationToken cancellationToken)
    {
        IReadOnlyList<FirestoreDocument>? documents =
            await _db.ListAsync(FirestoreCollections.Players, cancellationToken)
                .ConfigureAwait(false);
        if (documents is null)
        {
            return null;
        }

        string? ownUid = uid();
        return string.IsNullOrEmpty(ownUid)
            ? documents
            : await MigrateLegacyRowAsync(documents, ownUid, cancellationToken)
                .ConfigureAwait(false);
    }

    public Task<bool> SetXpAsync(
        string uid, double xp, string displayName, CancellationToken cancellationToken) =>
        WriteAsync(
            uid, displayName,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["xp"] = Math.Max(0.0, xp),
            },
            cancellationToken);

    public Task<bool> SetZoneKmAsync(
        string uid, double zoneKm, string displayName, CancellationToken cancellationToken) =>
        WriteAsync(
            uid, displayName,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["longestZoneKm"] = Math.Max(0.0, zoneKm),
            },
            cancellationToken);

    public Task<bool> SetShiftEarningsAsync(
        string uid, double earnings, long firstEarnedTicks, string displayName,
        CancellationToken cancellationToken)
    {
        DateTime firstEarned = firstEarnedTicks > 0 && firstEarnedTicks < DateTime.MaxValue.Ticks
            ? new DateTime(firstEarnedTicks, DateTimeKind.Utc)
            : _clock.UtcNow;
        return WriteAsync(
            uid, displayName,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["highestShiftEarnings"] = Math.Max(0.0, earnings),
                ["highestShiftEarningsFirstEarnedAt"] = FirebaseOptions.Timestamp(firstEarned),
            },
            cancellationToken);
    }

    // Boards written before the name key used the anonymous uid as the doc
    // id. Move that row under its recorded display name once, so the old
    // username survives as its own rival row instead of being hidden forever.
    // The write is skipped when the name row already exists (a newer push
    // must not be replaced by stale legacy numbers); the legacy row is then
    // simply deleted.
    private async Task<IReadOnlyList<FirestoreDocument>?> MigrateLegacyRowAsync(
        IReadOnlyList<FirestoreDocument> documents, string uid,
        CancellationToken cancellationToken)
    {
        FirestoreDocument? legacy = Find(documents, uid);
        if (legacy is null)
        {
            return documents;
        }

        string name = (legacy.Text("displayName") ?? string.Empty).Trim();
        string key = PlayerNameKey.Normalize(name);
        if (key.Length == 0 || string.Equals(key, uid, StringComparison.Ordinal))
        {
            return documents;
        }

        if (Find(documents, key) is null)
        {
            Dictionary<string, object?> fields =
                new(legacy.Fields, StringComparer.Ordinal)
                {
                    ["ownerUid"] = uid,
                    ["displayName"] = name,
                };
            // Create-only: a push may have opened the name row while this
            // boot's board was loading, and that newer row must win.
            FirestoreCreateResult created = await _db.CreateAsync(
                FirestoreCollections.Players + "/" + key, fields, cancellationToken)
                .ConfigureAwait(false);
            if (created == FirestoreCreateResult.Failed)
            {
                return documents;
            }
            if (created == FirestoreCreateResult.Created)
            {
                _log.LogInfo($"firebase: moved player doc {uid} to '{key}'");
            }
        }

        await _db.BatchDeleteAsync(
            [FirestoreCollections.Players + "/" + uid], cancellationToken).ConfigureAwait(false);
        return await _db.ListAsync(FirestoreCollections.Players, cancellationToken)
            .ConfigureAwait(false);
    }

    private static FirestoreDocument? Find(
        IReadOnlyList<FirestoreDocument> documents, string id)
    {
        foreach (FirestoreDocument doc in documents)
        {
            if (string.Equals(doc.Id, id, StringComparison.Ordinal))
            {
                return doc;
            }
        }
        return null;
    }

    // The display name is the board key, so a nameless write (before the
    // opening screen) would create a row nobody can reach or reclaim.
    private Task<bool> WriteAsync(
        string uid, string displayName, Dictionary<string, object?> fields,
        CancellationToken cancellationToken)
    {
        string name = displayName.Trim();
        string key = PlayerNameKey.Normalize(name);
        if (key.Length == 0)
        {
            return Task.FromResult(false);
        }

        fields["ownerUid"] = uid;
        fields["displayName"] = name;
        fields["lastUpdatedAt"] = FirebaseOptions.Timestamp(_clock.UtcNow);
        return _db.SetFieldsAsync(
            FirestoreCollections.Players + "/" + key, fields, cancellationToken);
    }
}
