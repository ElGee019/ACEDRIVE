// Employee persistence boundary (PORTPLAN item 54). Lives in Core beside the
// other store seams so the roster stays dependency-free; the JSON file sink is
// Overlay/Services/JsonEmployeeStore.
namespace AceDrive.Core;

/// <summary>
/// Loads and banks the hired crew and its weekly hire roster. Implementations
/// must never throw: a missing or corrupt file means nobody is hired and the
/// next visit rolls a fresh roster.
/// </summary>
public interface IEmployeeStore
{
    /// <summary>The persisted crew and roster; empty when there is no save yet.</summary>
    EmployeeState Load();

    /// <summary>Replaces the persisted state with the roster's current one.</summary>
    void Save(EmployeeState state);
}
