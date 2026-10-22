using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The all-players car-km aggregate: the source of the dynamic rarity rank.
/// Each completed owned-car trip increments one doc atomically.
/// </summary>
public sealed class FirestoreCars
{
    private readonly IFirestoreDatabase _db;
    private readonly IClock _clock;

    public FirestoreCars(IFirestoreDatabase db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CarKm>?> LoadAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<FirestoreDocument>? documents =
            await _db.ListAsync(FirestoreCollections.Cars, cancellationToken)
                .ConfigureAwait(false);
        if (documents is null)
        {
            return null;
        }

        List<CarKm> rows = [];
        foreach (FirestoreDocument doc in documents)
        {
            long km = doc.Whole("totalKmDriven") ?? 0L;
            rows.Add(new CarKm(doc.Id, (int)Math.Clamp(km, 0L, int.MaxValue)));
        }
        return rows;
    }

    public Task<bool> IncrementKmAsync(
        string carKey, int km, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(carKey) || km <= 0)
        {
            return Task.FromResult(true);
        }

        Dictionary<string, object?> fields = new(StringComparer.Ordinal)
        {
            ["lastUpdatedAt"] = FirebaseOptions.Timestamp(_clock.UtcNow),
        };
        Dictionary<string, long> increments = new(StringComparer.Ordinal)
        {
            ["totalKmDriven"] = km,
        };
        return _db.IncrementAsync(
            FirestoreCollections.Cars + "/" + carKey, fields, increments, cancellationToken);
    }
}
