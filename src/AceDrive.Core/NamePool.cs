// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11/13/61
// (name pools), port of the Unity namePools.json entry + zone-pool map weight.
namespace AceDrive.Core;

/// <summary>One cultural name pool with its share of the cultural zone it
/// belongs to (Unity NamePool plus the weight from zonePoolMaps.json).</summary>
public sealed record NamePool(
    string Id,
    double Weight,
    IReadOnlyList<string> FirstNames,
    IReadOnlyList<string> Surnames);
