// The built-in POI name table: the test-shell fallback when Data/pois.json
// cannot be read, and the deterministic fixture for tests.
namespace AceDrive.Core;

/// <summary>Built-in businesses used when the data file is missing or invalid.</summary>
public static class PoiCatalog
{
    /// <summary>Max's grocery store, the first story POI (PORTPLAN items 5/24/27).</summary>
    public static IReadOnlyList<PoiDefinition> Default { get; } =
    [
        new PoiDefinition(
            "max-grocery",
            "Max's Grocery Store",
            PoiTypes.Grocery,
            "Max Silvestri",
            "groceries",
            ["MAPLE SYRUP"]),
    ];
}
