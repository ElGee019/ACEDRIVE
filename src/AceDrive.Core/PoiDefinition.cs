// The name and type table behind the POI generator (PORTPLAN 24/27).
// Data/pois.json is the source of truth; PoiCatalog.Default is the fallback.
// Type/Owner/Category are optional so old data and tests keep compiling;
// the arrival handlers only match on Type.
namespace AceDrive.Core;

/// <summary>One business that can receive a random map address.</summary>
/// <param name="Id">Stable POI key, also the shop inventory key.</param>
/// <param name="Name">Display name shown on the map and the HUD.</param>
/// <param name="Type">POI family (see <see cref="PoiTypes"/>).</param>
/// <param name="OwnerName">Owner behind the friendship line, empty for unknown.</param>
/// <param name="Category">Shop category driving the shelf layout and, for a
/// placed store, the hub category that must be owned first. Empty for
/// non-shops.</param>
/// <param name="GuaranteedItems">Story items the shop must always stock.</param>
/// <param name="NamePattern">Display-name template for a generated business
/// ("{0}'s Bank"), filled with the procedural owner's first name. Null for
/// fixed and story POIs.</param>
/// <param name="Spawn">When and how often this business opens as the career
/// grows. Null for fixed and story POIs. Last so old data keeps its
/// defaults.</param>
public sealed record PoiDefinition(
    string Id,
    string Name,
    string Type = PoiTypes.Grocery,
    string OwnerName = "",
    string Category = "",
    IReadOnlyList<string>? GuaranteedItems = null,
    string? NamePattern = null,
    PoiSpawnRule? Spawn = null);
