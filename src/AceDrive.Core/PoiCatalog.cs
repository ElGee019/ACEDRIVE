// The built-in POI name table: the test-shell fallback when Data/pois.json
// cannot be read, and the deterministic fixture for tests.
namespace AceDrive.Core;

/// <summary>Built-in businesses used when the data file is missing or invalid.</summary>
public static class PoiCatalog
{
    /// <summary>The birthday errand's business id, also Unity's maxsStore row.</summary>
    public const string MaxGroceryId = "max-grocery";

    /// <summary>
    /// The built-in table: Max's story shop plus the generated businesses and
    /// stores. A new career owns none of them yet: the spawn rules carry
    /// Unity's thresholds, so the car wash opens at 6 discovered streets, the
    /// insurance broker at 13, the bank at 15, the dealer after 10 jobs and
    /// the mechanic after 36. Grocery and coffee shops open from an owned hub
    /// (one immediately, then per street interval). Data/pois.json is the
    /// source of truth.
    /// </summary>
    public static IReadOnlyList<PoiDefinition> Default { get; } =
    [
        new PoiDefinition(
            MaxGroceryId,
            "Max's Grocery Store",
            PoiTypes.Grocery,
            "Max Silvestri",
            "groceries",
            ["MAPLE SYRUP"]),
        new PoiDefinition(
            "carwash", "Car Wash", PoiTypes.CarWash,
            NamePattern: "{0}'s Carwash",
            Spawn: new PoiSpawnRule(
                PoiPacing.Streets, FirstStreets: 6, IntervalStreets: 47)),
        new PoiDefinition(
            "insurance", "Insurance Broker", PoiTypes.Insurance,
            NamePattern: "{0}'s Insurance",
            Spawn: new PoiSpawnRule(
                PoiPacing.Streets, FirstStreets: 13, IntervalStreets: 57)),
        new PoiDefinition(
            "bank", "Bank", PoiTypes.Bank,
            NamePattern: "{0}'s Bank",
            Spawn: new PoiSpawnRule(
                PoiPacing.Streets, FirstStreets: 15, IntervalStreets: 51)),
        new PoiDefinition(
            "dealer", "Car Dealer", PoiTypes.Dealer,
            NamePattern: "{0}'s Cars",
            Spawn: new PoiSpawnRule(
                PoiPacing.Either, FirstStreets: 37,
                IntervalStreets: 37, FirstJobs: 10)),
        new PoiDefinition(
            "mechanic", "Mechanic", PoiTypes.Mechanic,
            NamePattern: "{0}'s Mechanic",
            Spawn: new PoiSpawnRule(
                PoiPacing.JobsFirstThenStreets, FirstStreets: 3,
                IntervalStreets: 36, FirstJobs: 36)),
        new PoiDefinition(
            "groceries_store", "Grocery Store", PoiTypes.GroceryStore,
            Category: "groceries",
            NamePattern: "{0}'s Grocery",
            Spawn: new PoiSpawnRule(
                PoiPacing.OwnedCategory, IntervalStreets: 20)),
        new PoiDefinition(
            "coffee_shop", "Coffee Shop", PoiTypes.CoffeeShop,
            Category: "coffee_beans",
            NamePattern: "{0}'s Coffee Shop",
            Spawn: new PoiSpawnRule(
                PoiPacing.OwnedCategory, IntervalStreets: 14)),
    ];
}
