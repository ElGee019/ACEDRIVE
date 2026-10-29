using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The road-test boards (PORTPLAN item 49, Unity RoadTestStandingsService): one
/// doc per (uid, car) holding the best acceleration time or brake distance, and
/// the all-cars record book read off the same docs (each car's best). Values are
/// lower-is-better and ties keep the earlier stamp.
/// </summary>
public sealed class FirestoreRoadTestStandings : IRoadTestStandings
{
    private const int SchemaVersion = 2;
    private readonly IFirestoreDatabase _db;
    private readonly Func<CancellationToken, Task<string?>> _uidAsync;
    private readonly Func<string?> _uid;
    private readonly IClock _clock;
    private readonly ILog _log;

    public FirestoreRoadTestStandings(
        IFirestoreDatabase db, Func<CancellationToken, Task<string?>> uidAsync,
        Func<string?> uid, IClock clock, ILog log)
    {
        _db = db;
        _uidAsync = uidAsync;
        _uid = uid;
        _clock = clock;
        _log = log;
    }

    public bool IsOnline => true;

    public string? OwnUid => _uid();

    /// <summary>
    /// Best-guarded push: an existing lower value is never overwritten. The
    /// read and the merge are two requests (the REST transport has no
    /// transaction helper yet); the board is dormant, and a concurrent
    /// second session of the same account is the only way to lose a tie.
    /// </summary>
    public async Task<bool> PushAsync(
        RoadTestMetric metric, string carKey, double value,
        string playerName, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(value) || value <= 0.0 || string.IsNullOrWhiteSpace(carKey))
        {
            return false;
        }
        string? uid = await UidAsync(cancellationToken).ConfigureAwait(false);
        if (uid is null) return false;

        string collection = Collection(metric);
        string field = Field(metric);
        string path = collection + "/" + uid + "|" + carKey;
        Dictionary<string, object?> fields = new(StringComparer.Ordinal)
        {
            ["uid"] = uid,
            ["playerName"] = playerName ?? string.Empty,
            ["carId"] = carKey,
            [field] = value,
            ["v"] = SchemaVersion,
            ["lastUpdatedAt"] = FirebaseOptions.Timestamp(_clock.UtcNow),
        };

        for (int attempt = 0; attempt < 2; attempt++)
        {
            FirestoreDocument? existing = await _db.GetAsync(path, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                double? best = existing.Number(field);
                if (best is { } current && current <= value)
                {
                    return true;
                }
                bool saved = await _db.SetFieldsAsync(path, fields, cancellationToken)
                    .ConfigureAwait(false);
                if (!saved)
                {
                    _log.LogWarning($"firebase: road-test push failed for {carKey}");
                }
                return saved;
            }

            FirestoreCreateResult created = await _db.CreateAsync(
                path, fields, cancellationToken).ConfigureAwait(false);
            if (created == FirestoreCreateResult.Created) return true;
            if (created == FirestoreCreateResult.Failed)
            {
                _log.LogWarning($"firebase: road-test push failed for {carKey}");
                return false;
            }
            // A concurrent writer created the doc between Get and Create;
            // loop once to compare against its value.
        }
        return false;
    }

    public async Task<IReadOnlyList<RoadTestEntry>?> TopAsync(
        RoadTestMetric metric, string carKey, int count,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FirestoreDocument>? documents = await _db.QueryEqualsAsync(
            Collection(metric), "carId", carKey, cancellationToken).ConfigureAwait(false);
        if (documents is null)
        {
            return null;
        }

        List<RoadTestEntry> entries = [];
        foreach (FirestoreDocument doc in documents)
        {
            if (Parse(metric, doc) is { } entry)
            {
                entries.Add(entry);
            }
        }
        return RoadTestBoard.Top(entries, count);
    }

    public async Task<IReadOnlyList<RoadTestEntry>?> TopCarsAsync(
        RoadTestMetric metric, CancellationToken cancellationToken = default)
    {
        // The record book reads the whole collection: Firestore cannot group,
        // so the best-per-car reduction runs here on the parsed rows.
        IReadOnlyList<FirestoreDocument>? documents =
            await _db.ListAsync(Collection(metric), cancellationToken).ConfigureAwait(false);
        if (documents is null)
        {
            return null;
        }

        List<RoadTestEntry> entries = [];
        foreach (FirestoreDocument doc in documents)
        {
            if (Parse(metric, doc) is { } entry)
            {
                entries.Add(entry);
            }
        }
        return RoadTestBoard.BestPerCar(entries);
    }

    public async Task<RoadTestEntry?> LoadPlayerBestAsync(
        RoadTestMetric metric, string carKey,
        CancellationToken cancellationToken = default)
    {
        string? uid = await UidAsync(cancellationToken).ConfigureAwait(false);
        if (uid is null) return null;
        FirestoreDocument? doc = await _db.GetAsync(
            Collection(metric) + "/" + uid + "|" + carKey, cancellationToken)
            .ConfigureAwait(false);
        return doc is null ? null : Parse(metric, doc);
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

    private static RoadTestEntry? Parse(RoadTestMetric metric, FirestoreDocument doc)
    {
        double? value = doc.Number(Field(metric));
        if (value is not { } number)
        {
            return null;
        }
        string uid = doc.Text("uid") ?? doc.Id;
        return new RoadTestEntry(
            uid,
            doc.Text("playerName") ?? string.Empty,
            doc.Text("carId") ?? string.Empty,
            number,
            doc.Text("lastUpdatedAt") ?? string.Empty);
    }

    private static string Collection(RoadTestMetric metric) =>
        metric == RoadTestMetric.BrakeDistance
            ? FirestoreCollections.RoadTestBrake
            : FirestoreCollections.RoadTest;

    private static string Field(RoadTestMetric metric) =>
        metric == RoadTestMetric.BrakeDistance ? "brakeMeters" : "time";
}
