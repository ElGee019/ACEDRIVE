// Resident persistence boundary (PORTPLAN items 11/45). Lives in Core
// beside ILog so ResidentBook stays dependency-free; the JSON file sink is
// Overlay/Services/JsonResidentStore.
namespace AceDrive.Core;

/// <summary>
/// Loads and banks the resident table. Implementations must never throw:
/// a missing or corrupt file means every address is new again.
/// </summary>
public interface IResidentStore
{
    /// <summary>Every persisted resident; empty when there is no save yet.</summary>
    IReadOnlyList<Resident> Load();

    /// <summary>Replaces the persisted table with the book's current one.</summary>
    void Save(IReadOnlyList<Resident> residents);
}
