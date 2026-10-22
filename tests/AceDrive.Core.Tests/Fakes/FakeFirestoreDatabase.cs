using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// In-memory Firestore for the online-service tests: configured collections
/// answer list/query reads, writes are recorded, and failure switches make
/// the offline-degradation paths testable without a network.
/// </summary>
public sealed class FakeFirestoreDatabase : IFirestoreDatabase
{
    public Dictionary<string, List<FirestoreDocument>> Collections { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, FirestoreDocument> ByPath { get; } =
        new(StringComparer.Ordinal);
    public List<(string Path, IReadOnlyDictionary<string, object?> Fields)> Sets { get; } = [];
    public List<FakeIncrementCall> Increments { get; } = [];
    public List<List<string>> Deleted { get; } = [];
    public List<string> Queries { get; } = [];
    public bool ListFails { get; set; }
    public bool WritesSucceed { get; set; } = true;
    public FirestoreCreateResult NextCreateResult { get; set; } = FirestoreCreateResult.Created;
    public List<FirestoreDocument> QueryRows { get; set; } = [];

    public Task<IReadOnlyList<FirestoreDocument>?> ListAsync(
        string collection, CancellationToken cancellationToken)
    {
        if (ListFails)
        {
            return Task.FromResult<IReadOnlyList<FirestoreDocument>?>(null);
        }
        IReadOnlyList<FirestoreDocument> rows =
            Collections.TryGetValue(collection, out List<FirestoreDocument>? docs)
                ? docs
                : [];
        return Task.FromResult<IReadOnlyList<FirestoreDocument>?>(rows);
    }

    public Task<FirestoreDocument?> GetAsync(
        string documentPath, CancellationToken cancellationToken) =>
        Task.FromResult(ByPath.GetValueOrDefault(documentPath));

    public Task<bool> SetFieldsAsync(
        string documentPath,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken)
    {
        Sets.Add((documentPath, fields));
        return Task.FromResult(WritesSucceed);
    }

    public Task<bool> IncrementAsync(
        string documentPath,
        IReadOnlyDictionary<string, object?> fields,
        IReadOnlyDictionary<string, long> increments,
        CancellationToken cancellationToken)
    {
        Increments.Add(new FakeIncrementCall(documentPath, fields, increments));
        return Task.FromResult(WritesSucceed);
    }

    public Task<FirestoreCreateResult> CreateAsync(
        string documentPath,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken)
    {
        Sets.Add((documentPath, fields));
        return Task.FromResult(NextCreateResult);
    }

    public Task<IReadOnlyList<FirestoreDocument>?> QueryEqualsAsync(
        string collection, string field, string value,
        CancellationToken cancellationToken)
    {
        if (ListFails)
        {
            return Task.FromResult<IReadOnlyList<FirestoreDocument>?>(null);
        }
        Queries.Add($"{collection}.{field}={value}");
        IReadOnlyList<FirestoreDocument> rows = QueryRows.Count > 0
            ? QueryRows
            : Collections.TryGetValue(collection, out List<FirestoreDocument>? docs)
                ? docs
                : [];
        return Task.FromResult<IReadOnlyList<FirestoreDocument>?>(rows);
    }

    public Task<bool> BatchDeleteAsync(
        IReadOnlyList<string> documentPaths, CancellationToken cancellationToken)
    {
        Deleted.Add([.. documentPaths]);
        return Task.FromResult(WritesSucceed);
    }
}
