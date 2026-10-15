// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11/13/61.
namespace AceDrive.Core;

/// <summary>
/// Cultural name pools in play plus the passenger companion suffixes
/// (Data/name_pools.json: the Germany zone's pools from Unity
/// zonePoolMaps.json + the exact customerSuffixes.json list). The ACE
/// map covers Germany only, so other zones' pools are deliberately absent.
/// Default is the test-shell fallback when the data file is missing,
/// not the source of truth.
/// </summary>
public sealed record NamePoolTable(IReadOnlyList<NamePool> Pools, IReadOnlyList<string> Suffixes)
{
    public static readonly NamePoolTable Default = new(
    [
        new NamePool("german", 1.0,
            ["Max", "Fritz", "Klaus", "Anna", "Greta", "Leon", "Mila", "Sophie"],
            ["Weber", "Meyer", "Klein", "Wolf", "Neumann"]),
    ],
    ["and some friends", "and some colleagues"]);
}
