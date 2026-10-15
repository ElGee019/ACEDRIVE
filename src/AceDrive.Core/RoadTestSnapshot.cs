// What the road-test ledger hands the persistence layer: the running pick
// plus the banked ids (PORTPLAN item 49). Pure data so AceDrive.Core stays
// I/O-free (AGENTS.md); the Overlay's IRoadTestStore owns the file format.
namespace AceDrive.Core;

/// <summary>A road-test ledger as saved between overlay sessions.</summary>
/// <param name="CarId">The car offered now; null when the pool was empty.</param>
/// <param name="TestedIds">Cars banked since the last pool reset.</param>
public sealed record RoadTestSnapshot(string? CarId, IReadOnlyList<string> TestedIds);
