namespace AceDrive.Core.Tests;

/// <summary>One recorded <see cref="FakeFirestoreDatabase"/> increment write.</summary>
public sealed record FakeIncrementCall(
    string Path,
    IReadOnlyDictionary<string, object?> Fields,
    IReadOnlyDictionary<string, long> Increments);
