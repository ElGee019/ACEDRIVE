// Balance table for the car-condition cluster (PORTPLAN items 31/32/39), port
// of Unity Utils/CarMaintenanceService.cs, Utils/EngineHealthService.cs and
// Driving/Services/StandardBreakdownStrategy.cs: the part intervals/prices,
// the class-multiplier step, the engine red-km bands and the breakdown
// envelope, one place to edit. Like every rules record the shipped
// Data/maintenance_rules.json is the source of truth; Default is Unity's
// shipped table and the fallback for tests and a missing/invalid file.
//
// The nested records are DTOs the rules record owns (GarageState.cs precedent),
// one row each: Key/IntervalKm/FullPrice for a part, MinClass/Multiplier
// for the class step, MaxCc/RedKm for an engine-size band.
namespace AceDrive.Core;

public sealed record MaintenanceRules
{
    /// <summary>Parts in mechanic-display order (Unity CarMaintenanceService.AllParts
    /// + CarMechanicScreen.Parts). The list order IS the page order.</summary>
    public IReadOnlyList<MaintenancePart> Parts { get; init; } = [];

    /// <summary>Class → price multiplier step, ordered by MinClass descending
    /// (Unity CarMechanicScreen.ClassMultiplier). The lowest band must cover
    /// class 0 so every catalogue row resolves.</summary>
    public IReadOnlyList<ClassPriceBand> ClassBands { get; init; } = [];

    /// <summary>Engine-size → red-km band, ordered by MaxCc ascending. A band
    /// matches when <c>engineCc &lt; MaxCc</c> (Unity EngineHealthService.GetRedKm).</summary>
    public IReadOnlyList<EngineBand> EngineBands { get; init; } = [];

    /// <summary>Unity CarMaintenanceService.CurveExponent: the accelerating
    /// wear→chance curve (0.10 at full wear, 0.025 at half).</summary>
    public double CurveExponent { get; init; }

    /// <summary>Unity MaxBreakdownChance: hard ceiling on the aggregate chance.</summary>
    public double MaxBreakdownChance { get; init; }

    /// <summary>Unity StandardBreakdownStrategy's random fraction window: a
    /// triggered breakdown fires at 10%–90% of the route.</summary>
    public double BreakdownFractionMin { get; init; }
    public double BreakdownFractionMax { get; init; }

    /// <summary>Unity BreakdownFlow.PickupFee: the tow-in charge.</summary>
    public double PickupFee { get; init; }

    /// <summary>Unity EngineHealthService.RepairCostFraction / NewEngineCostFraction:
    /// repair keeps the odometer, a new engine resets it to 0.</summary>
    public double EngineRepairFraction { get; init; }
    public double NewEngineFraction { get; init; }

    /// <summary>Unity EngineHealthService schedule: weekly every 7 days,
    /// annual every 364-371 (364 + a 0-7 jitter).</summary>
    public int WeeklyIntervalDays { get; init; }
    public int YearIntervalDays { get; init; }
    public int YearJitterDays { get; init; }

    /// <summary>Unity's shipped tables (CarMaintenanceService + EngineHealthService
    /// + CarMechanicScreen + StandardBreakdownStrategy + BreakdownFlow). Fallback
    /// for tests and for a missing or invalid Data/maintenance_rules.json — the
    /// loaded file is the source of truth.</summary>
    public static readonly MaintenanceRules Default = new()
    {
        Parts =
        [
            new MaintenancePart("oil_change", 1200.0, 250.0),
            new MaintenancePart("wipers", 1000.0, 90.0),
            new MaintenancePart("air_filter", 1500.0, 110.0),
            new MaintenancePart("brake_pads", 2700.0, 350.0),
            new MaintenancePart("tires", 2500.0, 900.0),
            new MaintenancePart("brake_fluid", 3000.0, 180.0),
            new MaintenancePart("transmission_fluid", 3000.0, 350.0),
            new MaintenancePart("car_battery", 3600.0, 400.0),
        ],
        ClassBands =
        [
            new ClassPriceBand(16, 1.0),
            new ClassPriceBand(11, 0.8),
            new ClassPriceBand(7, 0.6),
            new ClassPriceBand(0, 0.4),
        ],
        EngineBands =
        [
            new EngineBand(1000, 250000.0),
            new EngineBand(2000, 300000.0),
            new EngineBand(3000, 350000.0),
            new EngineBand(4000, 400000.0),
            new EngineBand(5000, 450000.0),
            new EngineBand(int.MaxValue, 500000.0),
        ],
        CurveExponent = 2.0,
        MaxBreakdownChance = 0.10,
        BreakdownFractionMin = 0.10,
        BreakdownFractionMax = 0.90,
        PickupFee = 100.0,
        EngineRepairFraction = 0.10,
        NewEngineFraction = 0.30,
        WeeklyIntervalDays = 7,
        YearIntervalDays = 364,
        YearJitterDays = 8,
    };

    /// <summary>Unity CarMechanicScreen.ClassMultiplier: the price step a car's
    /// production class falls in. An unknown class (0) rides the lowest band.</summary>
    public double MultiplierFor(int carClass)
    {
        for (int i = 0; i < ClassBands.Count; i++)
        {
            if (carClass >= ClassBands[i].MinClass)
            {
                return ClassBands[i].Multiplier;
            }
        }
        // A malformed table (no band covering class 0) degrades to the cheapest
        // step instead of throwing mid-purchase.
        return ClassBands.Count > 0 ? ClassBands[^1].Multiplier : 1.0;
    }

    /// <summary>Unity EngineHealthService.GetRedKm: the odometer at which the
    /// engine reads 100% broken, by engine size.</summary>
    public double RedKmFor(int engineCc)
    {
        for (int i = 0; i < EngineBands.Count; i++)
        {
            if (engineCc < EngineBands[i].MaxCc)
            {
                return EngineBands[i].RedKm;
            }
        }
        // A malformed table (no band covering the largest engine) keeps the
        // last band's threshold rather than failing the check.
        return EngineBands.Count > 0 ? EngineBands[^1].RedKm : 0.0;
    }
}

/// <summary>One serviceable part: stable key (the mechanic page resolves its copy
/// through resx), service interval in km and the full replacement price the class
/// multiplier scales (Unity CarMechanicScreen.Parts + CarMaintenanceService.AllParts).</summary>
public sealed record MaintenancePart(string Key, double IntervalKm, double FullPrice);

/// <summary>One step of the class → price step function (Unity ClassMultiplier).</summary>
public sealed record ClassPriceBand(int MinClass, double Multiplier);

/// <summary>One engine-size band and the odometer at which it reads fully broken.</summary>
public sealed record EngineBand(int MaxCc, double RedKm);
