using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the road-test pool from Data/cars.json next to the exe. Missing or
/// invalid file degrades to an empty pool (the Tuesday row then shows the
/// plain "Road test" label) — the overlay must never fail to start over a
/// data file. Rows may carry a mechanical preset (showroom variant,
/// PORTPLAN 62); an empty preset normalizes to null and rows sharing one
/// ledger key drop so the bank can never double-count a car.
/// </summary>
public sealed class JsonCarStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonCarStore(ILog log)
        : this(DataPaths.GameData("cars.json"), log)
    { }

    public JsonCarStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<CarSpec> Load()
    {
        // An empty array is as unusable as a missing file: the row cleaner
        // below still needs something to walk.
        if (!JsonTable.TryLoad(
                _log, _file, "cars.json", "road test runs without a pool",
                cars => cars.Count > 0, out List<CarSpec>? cars))
        {
            return [];
        }

        List<CarSpec> valid = [];
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (CarSpec car in cars)
        {
            CarSpec row = car.Preset is { Length: 0 } ? car with { Preset = null } : car;
            if (IsUsable(row) && keys.Add(row.Key))
            {
                valid.Add(row);
            }
        }

        if (valid.Count == 0)
        {
            _log.LogWarning($"cars.json has no usable rows at {_file}; road test runs without a pool");
            return [];
        }

        if (valid.Count != cars.Count)
        {
            _log.LogWarning($"cars.json dropped {cars.Count - valid.Count} unusable rows at {_file}");
        }

        _log.LogInfo($"cars loaded: {valid.Count} road-test candidates");
        return valid;
    }

    // A row is usable when it can be displayed, road-tested and sold: identity
    // present, year plausible, and no negative spec or money value. A negative
    // price in particular would pay the player on a purchase (the dealer keys
    // its rule on Price), so a corrupt row drops out here instead of reaching
    // the dealer, the garage or the receipt.
    private static bool IsUsable(CarSpec car) =>
        car.Id.Length > 0 && car.Year > 1900 && car.Brand.Length > 0 && car.Model.Length > 0
        && car.Price >= 0.0
        && car.TrunkLitres >= 0
        && car.Horsepower >= 0
        && car.EngineCc >= 0
        && car.MassKg >= 0
        && car.TopSpeedKmh >= 0
        && car.Acceleration >= 0.0
        && car.Gears >= 0
        && car.TankLitres >= 0.0
        && car.FuelPer100Km >= 0.0
        && car.BrakeDistance >= 0.0;
}
