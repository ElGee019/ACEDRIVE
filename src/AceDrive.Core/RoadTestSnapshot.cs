// What the road-test ledger hands the persistence layer: the running pick, the
// banked keys and the settled receipt record (PORTPLAN item 49). Pure data so
// AceDrive.Core stays I/O-free (AGENTS.md); the Overlay's IRoadTestStore owns
// the file format.
// Field names stay CarId/TestedIds: roadtest.json on disk already spells them,
// and renaming the members would silently orphan every saved pick.
namespace AceDrive.Core;

/// <summary>A road-test ledger as saved between overlay sessions.</summary>
/// <param name="CarId">Road-test key of the car offered now (folder id, or
/// id#preset for a variant row); null when the pool was empty.</param>
/// <param name="TestedIds">Keys banked since the last pool reset.</param>
/// <param name="Record">The settled receipt record (Unity's lastRoadTest* /
/// highestRoadTest* readings and the last shoot); null in a file written
/// before the receipt existed, which opens as an empty record.</param>
public sealed record RoadTestSnapshot(
    string? CarId, IReadOnlyList<string> TestedIds, RoadTestRecord? Record = null);
