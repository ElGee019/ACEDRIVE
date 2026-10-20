// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (Unity
// HubCareerCatalog.GetHubOwnerName + the owner name pool).
namespace AceDrive.Core;

/// <summary>
/// Assigns an owner person to a hub from the data's name pool, standing in
/// for the freeroam map's owner assignment (Unity does the same pick at buy
/// time and keys the delivery friendship on it). Deterministic across
/// processes: the pick hashes the hub name, never wall time or draw order,
/// so the freeroam map can later pin its POI to the same person.
/// </summary>
public static class HubOwnerNames
{
    // HOW the pick stays stable: GetHashCode is randomized per process in
    // modern .NET, so a plain string hash would reshuffle owners on every
    // restart. FNV-1a is a fixed tiny hash that needs no seed state.
    private const uint FnvOffset = 2166136261;
    private const uint FnvPrime = 16777619;

    /// <summary>The owner for the named hub, or "" when the pool is empty
    /// (an empty name means "no owner known", which the receipts hide).</summary>
    public static string Pick(string hubName, IReadOnlyList<string> pool)
    {
        if (hubName.Length == 0 || pool.Count == 0)
        {
            return "";
        }
        uint hash = FnvOffset;
        foreach (char c in hubName)
        {
            hash = (hash ^ c) * FnvPrime;
        }
        return pool[(int)(hash % (uint)pool.Count)];
    }
}
