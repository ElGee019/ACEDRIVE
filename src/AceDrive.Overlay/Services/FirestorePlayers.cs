using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The player's own Firestore doc: XP, longest zone and best-shift record.
/// One doc per anonymous uid, written with merge semantics so the three
/// writers never clobber each other's fields.
/// </summary>
public sealed class FirestorePlayers
{
    private readonly IFirestoreDatabase _db;
    private readonly IClock _clock;

    public FirestorePlayers(IFirestoreDatabase db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task<IReadOnlyList<FirestoreDocument>?> LoadAsync(
        CancellationToken cancellationToken) =>
        _db.ListAsync(FirestoreCollections.Players, cancellationToken);

    public Task<bool> SetXpAsync(
        string uid, double xp, string displayName, CancellationToken cancellationToken) =>
        _db.SetFieldsAsync(
            FirestoreCollections.Players + "/" + uid,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["xp"] = Math.Max(0.0, xp),
                ["displayName"] = displayName ?? string.Empty,
                ["lastUpdatedAt"] = FirebaseOptions.Timestamp(_clock.UtcNow),
            },
            cancellationToken);

    public Task<bool> SetZoneKmAsync(
        string uid, double zoneKm, string displayName, CancellationToken cancellationToken) =>
        _db.SetFieldsAsync(
            FirestoreCollections.Players + "/" + uid,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["longestZoneKm"] = Math.Max(0.0, zoneKm),
                ["displayName"] = displayName ?? string.Empty,
                ["lastUpdatedAt"] = FirebaseOptions.Timestamp(_clock.UtcNow),
            },
            cancellationToken);

    public Task<bool> SetShiftEarningsAsync(
        string uid, double earnings, long firstEarnedTicks, string displayName,
        CancellationToken cancellationToken)
    {
        DateTime firstEarned = firstEarnedTicks > 0 && firstEarnedTicks < DateTime.MaxValue.Ticks
            ? new DateTime(firstEarnedTicks, DateTimeKind.Utc)
            : _clock.UtcNow;
        return _db.SetFieldsAsync(
            FirestoreCollections.Players + "/" + uid,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["highestShiftEarnings"] = Math.Max(0.0, earnings),
                ["highestShiftEarningsFirstEarnedAt"] = FirebaseOptions.Timestamp(firstEarned),
                ["displayName"] = displayName ?? string.Empty,
                ["lastUpdatedAt"] = FirebaseOptions.Timestamp(_clock.UtcNow),
            },
            cancellationToken);
    }
}
