// One car in the EVO roster (PORTPLAN item 28): specs parsed from Data/cars.json,
// itself generated from CARSTATS_EVO.md (base rows = the v2 physics table, variant
// rows = the v3 card table) and VARIANT_RESEARCH.md's kspkg dump. Pure data: no
// behavior, no formatting. Consumers are the road-test ledger (item 49), the dealer
// stock (item 29), the garage (item 30) and the receipt's passenger lookup.
namespace AceDrive.Core;

/// <summary>
/// One car: stable Kunos folder id, optional mechanical preset (showroom variant)
/// and the display/spec/money columns the gameplay reads. Class is the production
/// band (1-21, CARSTATS_EVO.md) the jobs' tier math keys on; variants share the
/// folder's class. Every unit is named after its column so a snapshot or a Lua
/// import cannot mis-scale: TopSpeedKmh, Acceleration = seconds to 60 km/h (the
/// mother game's CarStats unit), TankLitres, FuelPer100Km, TrunkLitres, MassKg.
/// Price is the game's own shop money where the game prices the card; rows the
/// game leaves unpriced carry the researched market estimate (CARSTATS_EVO.md v5).
/// Price &lt;= 0 means "not for sale" — the dealer skips such a row instead of
/// giving the car away. 0/0 pax or trunk = no taxi/delivery jobs for that car.
/// </summary>
public sealed record CarSpec(
    string Id,
    int Year,
    string Brand,
    string Model,
    int Passengers = 1,
    int Class = 0,
    double Price = 0.0,
    int TrunkLitres = 0,
    int Horsepower = 0,
    int EngineCc = 0,
    int MassKg = 0,
    int TopSpeedKmh = 0,
    double Acceleration = 0.0,
    int Gears = 0,
    double TankLitres = 0.0,
    double FuelPer100Km = 0.0,
    double BrakeDistance = 0.0)
{
    /// <summary>Mechanical preset this row models; null = the car's single/default variant.</summary>
    public string? Preset { get; init; }

    /// <summary>
    /// Ledger identity: the folder id, or id#preset for a variant row. '#'
    /// appears in neither half (their alphabet is [a-z0-9_]), so keys stay
    /// unambiguous and old id-only banks still resolve null-preset rows.
    /// </summary>
    public string Key => Preset is { Length: > 0 } ? Id + "#" + Preset : Id;
}
