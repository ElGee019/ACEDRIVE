// What a restart must reopen of the garage (PORTPLAN items 30/31/32/34/51): the
// owned cars, which one is current, and each car's per-part wear + engine
// health. Nullable-safe defaults so a save written before a slice loads without
// the new columns instead of failing the deserialize.
namespace AceDrive.Core;

/// <summary>Persisted garage: the current car's key plus every owned car's state.</summary>
public sealed record GarageState(
    string? CurrentKey = null,
    IReadOnlyList<OwnedCarState>? Cars = null)
{
    public static readonly GarageState Empty = new();
}

/// <summary>Persisted per-car state (Unity OwnedCar fields this port keeps).
/// AskedCategories is the car-stat quiz history; a save written before the
/// request slice has none and loads as empty. JobsCompleted is the valuation
/// score's per-car job term (Unity CarJobsService): a save before the market
/// slice loads as zero.</summary>
public sealed record OwnedCarState(
    string Key,
    int PurchaseWeek = 0,
    double Odometer = 0.0,
    bool HasInsurance = false,
    int Filth = 0,
    IReadOnlyList<MaintenancePartState>? Parts = null,
    EngineConditionState? Engine = null,
    IReadOnlyList<string>? AskedCategories = null,
    int JobsCompleted = 0);

/// <summary>One part's baseline (Unity CarMaintenancePart: partKey +
/// lastServiceOdometer).</summary>
public sealed record MaintenancePartState(string Key, double LastServiceKm = 0.0);

/// <summary>Persisted engine health (Unity EngineHealthService fields).</summary>
public sealed record EngineConditionState(
    double BaseOdometer = 0.0,
    bool Broken = false,
    DateTime? WeeklyNext = null,
    DateTime? AnnualNext = null);
