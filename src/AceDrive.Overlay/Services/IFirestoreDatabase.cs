namespace AceDrive.Overlay.Services;

/// <summary>
/// The small Firestore surface the online services need. Kept document-
/// oriented (like a database connection) so every service can stay free of
/// HTTP/JSON details and tests can substitute a fake without a network.
/// Implementations never throw: null/false/Failed means "try again later".
/// </summary>
public interface IFirestoreDatabase
{
    /// <summary>Every document in a collection, following pagination.</summary>
    Task<IReadOnlyList<FirestoreDocument>?> ListAsync(
        string collection, CancellationToken cancellationToken);

    /// <summary>One document, or null when it does not exist/unavailable.</summary>
    Task<FirestoreDocument?> GetAsync(
        string documentPath, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the document exists: false when the read answered "missing",
    /// null when it could not be answered at all. The third state exists so a
    /// caller can tell "gone" from "the network blinked" and never deletes on a
    /// failed read.
    /// </summary>
    Task<bool?> ExistsAsync(
        string documentPath, CancellationToken cancellationToken);

    /// <summary>Merges the given fields into the document (creating it if needed).</summary>
    Task<bool> SetFieldsAsync(
        string documentPath,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically increments the given integer fields and merges the scalar
    /// fields in the same write.
    /// </summary>
    Task<bool> IncrementAsync(
        string documentPath,
        IReadOnlyDictionary<string, object?> fields,
        IReadOnlyDictionary<string, long> increments,
        CancellationToken cancellationToken);

    /// <summary>Create-only write: never overwrites an existing document.</summary>
    Task<FirestoreCreateResult> CreateAsync(
        string documentPath,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken);

    /// <summary>Documents whose string field equals the value.</summary>
    Task<IReadOnlyList<FirestoreDocument>?> QueryEqualsAsync(
        string collection, string field, string value,
        CancellationToken cancellationToken);

    /// <summary>
    /// Documents whose string field sorts after the value, used for
    /// "everything since" reads: invariant ISO-8601 timestamps sort
    /// chronologically as text. A document without the field never matches, and
    /// an empty result costs no document reads, which is what makes a poll that
    /// finds nothing free.
    /// </summary>
    Task<IReadOnlyList<FirestoreDocument>?> QueryTextAfterAsync(
        string collection, string field, string value,
        CancellationToken cancellationToken);

    /// <summary>Deletes the given documents in batches; false on any failure.</summary>
    Task<bool> BatchDeleteAsync(
        IReadOnlyList<string> documentPaths, CancellationToken cancellationToken);
}
